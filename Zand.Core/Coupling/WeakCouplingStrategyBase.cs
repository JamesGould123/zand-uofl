namespace Zand.Core.Coupling;

using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Zand.Core.CellularAutomata;
using Zand.Core.RigidBody;

// Coupling strategy reference: Kellomäki, T. (2014). Rigid body interaction for large-scale real-time water
// simulation. International Journal of Computer Games Technology, 2014, 580154.
// https://doi.org/10.1155/2014/580154

// Further credit: The methodology used by StampBodies was inspired by Nolla Games (2020). Noita [Video game],
// specifically Purho, P. (2019). Exploring the tech and design of Noita [Conference talk]. Game Developers
// Conference. https://www.youtube.com/watch?v=prXuyMCgbTc
public abstract class WeakCouplingStrategyBase : ICouplingStrategy
{
    private const int SandScanMargin = 3;

    private readonly float _cellsPerMeter;
    private readonly int _worldHeightCells;
    private readonly float _halfCellMeters;
    private readonly float _gravityMagnitude;
    private readonly ShapeRasterizer _rasterizer;
    private readonly LiquidPhysicsMode _liquidPhysicsMode;
    private readonly TempBodyLifetime _tempBodyLifetime;
    private readonly TempBodyMerging _tempBodyMerging;
    private readonly RasterizationBodyParallelism _bodyParallelism;
    private readonly int _rngSeed;

    private readonly List<(int X, int Y)> _stampedCells = [];

    // Used for Ephemeral mode
    private readonly List<BodyHandle> _ephemeralSandBodies = [];

    // All cells that need a temporary body this tick (from margins around all rigid bodies), before any merging.
    private readonly HashSet<(int X, int Y)> _desiredCells = [];

    // Used for Persistent mode
    // _desired: the rectangles of CA cells that should have a temporary static physics body
    // (a single cell each when there is no merging)
    private readonly HashSet<CellRect> _desired = [];
    private readonly List<CellRect> _toRemove = [];
    private readonly Dictionary<CellRect, BodyHandle> _persistentSandBodies = new();

    private readonly Dictionary<int, List<(int X, int Y)>> _desiredCellsByBody = new();
    private readonly Dictionary<int, (Vector2 Position, float Angle)> _lastBodyTransforms = new();

    // Populated by ComputeLiquidInteraction, consumed by ApplyDamping
    private readonly Dictionary<int, (float DampingSum, int Total)> _liquidStatsCache = new();

    protected WeakCouplingStrategyBase(float cellsPerMeter, int worldHeightCells,
        LiquidPhysicsMode liquidPhysicsMode = LiquidPhysicsMode.None,
        float gravityMagnitude = 10f,
        TempBodyLifetime tempBodyLifetime = TempBodyLifetime.Ephemeral,
        RasterizationBodyParallelism bodyParallelism = RasterizationBodyParallelism.Sequential,
        RasterizationCellParallelism cellParallelism = RasterizationCellParallelism.Sequential,
        TempBodyMerging tempBodyMerging = TempBodyMerging.NoMerging)
    {
        _tempBodyMerging = tempBodyMerging;
        _cellsPerMeter = cellsPerMeter;
        _worldHeightCells = worldHeightCells;
        _halfCellMeters = 0.5f / cellsPerMeter;
        _gravityMagnitude = gravityMagnitude;
        _rasterizer = new ShapeRasterizer(cellsPerMeter, worldHeightCells, cellParallelism);
        _liquidPhysicsMode = liquidPhysicsMode;
        _tempBodyLifetime = tempBodyLifetime;
        _bodyParallelism = bodyParallelism;
        _rngSeed = 42;
    }

    public abstract bool PhysicsFirst { get; }

    public abstract void BeforeCaUpdate(CaGrid grid, IRigidBodySimulation physics);

    public abstract void AfterCaUpdate(CaGrid grid, IRigidBodySimulation physics);

    public abstract void BeforeRbUpdate(CaGrid grid, IRigidBodySimulation physics);

    public abstract void AfterRbUpdate(CaGrid grid, IRigidBodySimulation physics);

    // Stamps the footprint of each rigid body into the CA grid as static particles,
    // preventing particles from flowing through rigid bodies. Only empty cells are stamped,
    // particles already inside of bodies are left as is (no expulsion involved here).
    protected void StampBodies(CaGrid grid, IRigidBodySimulation physics)
    {
        var bodies = physics.GetBodies().ToList();
        var candidates = new List<(int X, int Y)>[bodies.Count];

        RunPerBody(bodies.Count, i =>
        {
            var body = bodies[i];
            var cells = new List<(int, int)>();
            _rasterizer.Iterate(body, 0, (cx, cy) =>
            {
                if (grid.InBounds(cx, cy) && grid.GetCell(cx, cy).Type == CellType.Empty)
                {
                    cells.Add((cx, cy));
                }
            });
            candidates[i] = cells;
        });

        foreach (var cells in candidates)
        {
            foreach (var (cx, cy) in cells)
            {
                if (grid.GetCell(cx, cy).Type == CellType.Empty)
                {
                    grid.SetCell(cx, cy, new Cell { Type = CellType.Static });
                    _stampedCells.Add((cx, cy));
                }
            }
        }
    }

    protected void ClearStamps(CaGrid grid)
    {
        foreach (var (x, y) in _stampedCells)
        {
            grid.SetCell(x, y, new Cell { Type = CellType.Empty });
        }

        _stampedCells.Clear();
    }

    protected void DispatchScanAndSync(CaGrid grid, IRigidBodySimulation physics)
    {
        if (_tempBodyLifetime == TempBodyLifetime.Persistent)
        {
            SyncTempBodies(grid, physics);
        }
        else
        {
            ScanAndAddTempBodies(grid, physics);
        }
    }

    protected void DispatchRemove(IRigidBodySimulation physics)
    {
        // If Persistent mode then removal is handled inside SyncTempBodies on the next scan
        if (_tempBodyLifetime == TempBodyLifetime.Ephemeral)
        {
            RemoveTempBodies(physics);
        }
    }

    // Inspiration for buoyancy and damping: Kellomäki (2014).
    protected void ComputeLiquidInteraction(CaGrid grid, IRigidBodySimulation physics)
    {
        _liquidStatsCache.Clear();

        bool needsDamping = _liquidPhysicsMode is LiquidPhysicsMode.VelocityDamping or LiquidPhysicsMode.BuoyancyAndDamping;
        if (!needsDamping)
        {
            return;
        }

        bool applyBuoyancy = _liquidPhysicsMode == LiquidPhysicsMode.BuoyancyAndDamping;
        var bodies = physics.GetBodies().Where(b => !b.IsStatic).ToList();
        var results = new (float Buoyancy, float DampingSum, int Total)[bodies.Count];

        RunPerBody(bodies.Count, i => results[i] = ComputeBodyLiquidStats(grid, bodies[i]));

        for (int i = 0; i < bodies.Count; i++)
        {
            var (buoyancy, dampingSum, total) = results[i];
            _liquidStatsCache[bodies[i].Handle.Id] = (dampingSum, total);
            if (applyBuoyancy && buoyancy > 0f)
            {
                physics.ApplyForce(bodies[i].Handle, 0f, buoyancy);
            }
        }
    }

    // Inspiration for buoyancy and damping: Kellomäki (2014).
    protected void ApplyDamping(CaGrid grid, IRigidBodySimulation physics)
    {
        if (_liquidPhysicsMode != LiquidPhysicsMode.VelocityDamping &&
            _liquidPhysicsMode != LiquidPhysicsMode.BuoyancyAndDamping)
        {
            return;
        }

        foreach (var body in physics.GetBodies())
        {
            if (body.IsStatic || !_liquidStatsCache.TryGetValue(body.Handle.Id, out var stats))
            {
                continue;
            }

            var (dampingSum, total) = stats;
            if (dampingSum == 0f || total == 0)
            {
                continue;
            }

            float scale = 1f - (dampingSum / total);
            physics.SetLinearVelocity(body.Handle, physics.GetLinearVelocity(body.Handle) * scale);
            physics.SetAngularVelocity(body.Handle, physics.GetAngularVelocity(body.Handle) * scale);
        }
    }

    // Adds temporary static rigid bodies for each solid cell adjacent to rigid bodies,
    // preventing rigid bodies from floating through sand particles. When ProbabilisticCollision is enabled,
    // liquid cells may also get temp bodies depending on RNG.
    private void ScanAndAddTempBodies(CaGrid grid, IRigidBodySimulation physics)
    {
        var bodies = physics.GetBodies().Where(b => !b.IsStatic).ToList();
        var results = new List<(int X, int Y)>[bodies.Count];

        RunPerBody(bodies.Count, i => results[i] = ComputeDesiredCells(grid, bodies[i], includeLiquid: true));

        if (_tempBodyMerging == TempBodyMerging.NoMerging)
        {
            for (int i = 0; i < bodies.Count; i++)
            {
                CommitDesiredCells(bodies[i], results[i]);
                foreach (var (cx, cy) in results[i])
                {
                    _ephemeralSandBodies.Add(AddTempBody(physics, new CellRect(cx, cy, 1, 1)));
                }
            }

            return;
        }

        _desiredCells.Clear();
        for (int i = 0; i < bodies.Count; i++)
        {
            CommitDesiredCells(bodies[i], results[i]);
            foreach (var cell in results[i])
            {
                _desiredCells.Add(cell);
            }
        }

        foreach (var rect in TempBodyRects.Build(grid, _desiredCells, _tempBodyMerging))
        {
            _ephemeralSandBodies.Add(AddTempBody(physics, rect));
        }
    }

    private void RemoveTempBodies(IRigidBodySimulation physics)
    {
        foreach (var handle in _ephemeralSandBodies)
        {
            physics.RemoveBody(handle);
        }

        _ephemeralSandBodies.Clear();
    }

    // When mode is Persistent - Updates the existing sand-cell bodies, removing outdated bodies and adding new ones.
    // Liquid cells are never added here, so ProbabilisticCollision's random liquid bodies only exist with Ephemeral lifetime
    // (ScenarioRunner rejects ProbabilisticCollision with Persistent for that reason).
    private void SyncTempBodies(CaGrid grid, IRigidBodySimulation physics)
    {
        var bodies = physics.GetBodies().Where(b => !b.IsStatic).ToList();
        var results = new List<(int X, int Y)>[bodies.Count];

        RunPerBody(bodies.Count, i => results[i] = ComputeDesiredCells(grid, bodies[i], includeLiquid: false));

        _desiredCells.Clear();
        for (int i = 0; i < bodies.Count; i++)
        {
            CommitDesiredCells(bodies[i], results[i]);
            foreach (var cell in results[i])
            {
                _desiredCells.Add(cell);
            }
        }

        _desired.Clear();
        foreach (var rect in TempBodyRects.Build(grid, _desiredCells, _tempBodyMerging))
        {
            _desired.Add(rect);
        }

        foreach (var rect in _persistentSandBodies.Keys)
        {
            if (!_desired.Contains(rect))
            {
                _toRemove.Add(rect);
            }
        }

        foreach (var rect in _toRemove)
        {
            physics.RemoveBody(_persistentSandBodies[rect]);
            _persistentSandBodies.Remove(rect);
        }

        _toRemove.Clear();

        foreach (var rect in _desired)
        {
            if (!_persistentSandBodies.ContainsKey(rect))
            {
                _persistentSandBodies[rect] = AddTempBody(physics, rect);
            }
        }
    }

    private List<(int X, int Y)> ComputeDesiredCells(CaGrid grid, BodyState body, bool includeLiquid)
    {
        int id = body.Handle.Id;
        bool moved = !_lastBodyTransforms.TryGetValue(id, out var last)
            || last.Position != body.Position
            || last.Angle != body.Angle;

        var bounds = _rasterizer.GetBounds(body, SandScanMargin);
        if (!moved && !grid.IsRegionActive(bounds) && _desiredCellsByBody.TryGetValue(id, out var cached))
        {
            return cached;
        }

        // A shared Random isn't safe to call concurrently for ProbabilisticCollision
        var rng = new Random(HashCode.Combine(_rngSeed, id));
        var cells = new List<(int, int)>();
        _rasterizer.Iterate(body, SandScanMargin, (cx, cy) =>
        {
            if (!grid.InBounds(cx, cy))
            {
                return;
            }

            var cellType = grid.GetCell(cx, cy).Type;
            bool isCandidate = cellType == CellType.Sand ||
                (includeLiquid &&
                 ElementProperties.GetCategory(cellType) == ParticleCategory.Liquid &&
                 _liquidPhysicsMode == LiquidPhysicsMode.ProbabilisticCollision &&
                 rng.NextDouble() < PhysicalProperties.CollisionProbability(cellType));
            if (isCandidate)
            {
                cells.Add((cx, cy));
            }
        });
        return cells;
    }

    // Commits the transform/candidate-cell cache for a single body
    private void CommitDesiredCells(BodyState body, List<(int X, int Y)> cells)
    {
        _lastBodyTransforms[body.Handle.Id] = (body.Position, body.Angle);
        _desiredCellsByBody[body.Handle.Id] = cells;
    }

    private BodyHandle AddTempBody(IRigidBodySimulation physics, CellRect rect)
    {
        float physX = (rect.X + (rect.Width / 2f)) / _cellsPerMeter;
        float physY = (_worldHeightCells - rect.Y - (rect.Height / 2f)) / _cellsPerMeter;
        return physics.AddStaticBox(physX, physY, rect.Width * _halfCellMeters, rect.Height * _halfCellMeters);
    }

    // Buoyancy: Archimedes' principle, F = rho * V * g, discretized per liquid cell as
    // density * cellArea * gravity.
    private (float Buoyancy, float DampingSum, int Total) ComputeBodyLiquidStats(CaGrid grid, BodyState body)
    {
        float cellArea = 1f / (_cellsPerMeter * _cellsPerMeter);
        float buoyancy = 0f;
        float dampingSum = 0f;
        int total = 0;
        _rasterizer.Iterate(body, 0, (cx, cy) =>
        {
            total++;
            if (!grid.InBounds(cx, cy))
            {
                return;
            }

            var cellType = grid.GetCell(cx, cy).Type;
            if (ElementProperties.GetCategory(cellType) == ParticleCategory.Liquid)
            {
                buoyancy += PhysicalProperties.DensityKgM3(cellType) * cellArea * _gravityMagnitude;
                dampingSum += PhysicalProperties.DampingStrength(cellType);
            }
        });
        return (buoyancy, dampingSum, total);
    }

    private void RunPerBody(int count, Action<int> compute)
    {
        if (_bodyParallelism == RasterizationBodyParallelism.Sequential || count <= 1)
        {
            for (int i = 0; i < count; i++)
            {
                compute(i);
            }

            return;
        }

        Parallel.For(0, count, compute);
    }
}
