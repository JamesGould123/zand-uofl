namespace Zand.Core.Ballistics;

using System.Collections.Concurrent;
using System.Numerics;
using System.Threading.Tasks;
using Zand.Core.CellularAutomata;
using Zand.Core.Coupling;
using Zand.Core.RigidBody;

// CA particles are limited in the interactions that they can have with rigid bodies -
// when a heavy body collides at speed with particle(s),
// we naturally expect those particles to be "thrown" by the momentum.

// This system attempts to address that by removing impacted CA cells from the CA grid,
// converting them temporarily into ballistic particles,
// then reattaching them when they come to rest
// (in the current implementation: when they touch a non-empty cell in the CA grid again).

// This system, when enabled, runs as its own phase alongside the coupling system rather than
// as part of a specific ICouplingStrategy hook.
// This allows it to behave identically regardless of order (CA-first or RB-first),
// and it allows it to have its own timing bucket for metrics collection.

// Two systems are used to implement this feature, selected by BallisticParticleMode:
// - PointSwarm: ballistic particles are represented as plain structs, a custom integration is used to for
//      their collisions and movement.
// - TempRigidBody: impacted particles become a tiny dynamic body in the selected IRigidBodySimulation
//      therefore the pre-built physics engine handles their collision and movement.

// Credit: The general system of deattaching and reattaching CA particles on impact and converting them to ballistic particles,
// along with the rasterization techniques and Active Rectangle,
// were inspired by Nolla Games (2020). Noita [Video game].

// Note: TempRigidBody performs terribly (as expected).
// This actually provides a great demonstration of part of the value proposition of this project.
// That is, it proves the previously assumed superiority of using a CA to simulate particles over a rigid body system
// in terms of optimization, highlighting *why* a hybrid physics engine combining these separate simulation strategies is valuable.

// Several configurations have been applied to improve visual realism, avoid particles "disappearing," and improve optimization:
// EjectionResistanceMode & EjectionImpulseTargetMode: Affect how force is applied to the rigid body by the new ballistic particle.
//      See the method ApplyEjectionResistance for further documentation.
// ParticleCollisionMode: Enabled/Disabled: If particles have collision with other particles.
// LandingSearchMode: When attempting to reintegrate with the CA grid,
//      should particles search in a wide area or a narrow area around their current position?
// EjectionDeflectionMode: When set to FaceNormal, when a particle is impacted by a slanted surface,
//      it should take some of its velocity from the direction the surface is pointing
//      (e.g. a vertical wall moving sideways should push particles straight,
//      where the same wall at a 45 degree backwards slant would push particles up at 45 degrees).
//      However, this could potentially be somewhat expensive to calculate.
// ReattachmentMode: whether a particle reattaches to the grid when it touches a
//      solid cell (OnCollision) or only once its speed has dropped below the settle threshold
//      (VelocitySettling).

// DetectAndEject and AdvanceTempBodies are not parallelized,
// as they call into the active IRigidBodySimulation backend,
// which complicates the implementation, likely requiring deferring each of the engine calls
// out of the parallel phase and into a sequential commit step afterwards.
// This is a candidate for future improvement, and out of scope for now.
public class BallisticParticleSystem
{
    // Since this implementation is specifically about "rapidly moving" rigid bodies,
    // a margin is necessary to detect movement before impact occurs
    // (before rigid bodies can be pushed back upon, preventing this system from triggering).
    // In testing, a margin of 1 was too small to reliably detect contacts,
    // and a margin of 3 caused too many cells to be converted into ballistic particles,
    // allowing heavy bodies to "fall through" sand in early implementations.
    private const int EjectionMarginCells = 2;

    private const int WideSearchMaxRadius = 4;

    private const int SeparationColorGroupCount = 3;

    // Ballistic particles prefer landing straight down, then landing in their own cells,
    // then the remaining neighbors in arbitrary order.
    private static readonly (int Dx, int Dy)[] LandingOffsets = [(0, 1), (0, 0), (-1, 1), (1, 1), (-1, 0), (1, 0)];

    // "Forward" half of a bin's 8 neighbors (east, north, northeast, southeast) - see SeparateOverlappingParticles
    private static readonly (int Dx, int Dy)[] ForwardNeighborOffsets = [(1, 0), (0, 1), (1, 1), (1, -1)];

    private readonly float _cellsPerMeter;
    private readonly int _worldHeightCells;
    private readonly float _gravityY;
    private readonly BallisticParticleMode _mode;
    private readonly EjectionResistanceMode _resistanceMode;
    private readonly ParticleCollisionMode _particleCollisionMode;
    private readonly LandingSearchMode _landingSearchMode;
    private readonly EjectionImpulseTargetMode _impulseTargetMode;
    private readonly EjectionDeflectionMode _deflectionMode;
    private readonly ReattachmentMode _reattachmentMode;
    private readonly BallisticResolutionParallelism _resolutionParallelism;
    private readonly float _ejectionVelocityThreshold;
    private readonly float _settleSpeedThreshold;
    private readonly float _restitution;
    private readonly float _friction;
    private readonly float _scatterSpeed;
    private readonly float _velocityDampingFraction;
    private readonly float _deflectionFactor;
    private readonly ShapeRasterizer _rasterizer;
    private readonly Random _rng;

    private readonly List<AirborneParticle> _airborne = [];
    private readonly List<TempBodyParticle> _tempBodies = [];

    // The velocity each non-static body was carrying *before* its most recent physics step -
    // that is, before the CA particles have had a chance to already impact its velocity.
    private readonly Dictionary<int, (Vector2 Linear, float Angular)> _lastBodyVelocity = new();

    public BallisticParticleSystem(
        float cellsPerMeter,
        int worldHeightCells,
        float gravityY,
        BallisticParticleMode mode,
        EjectionResistanceMode resistanceMode = EjectionResistanceMode.Impulse,
        ParticleCollisionMode particleCollisionMode = ParticleCollisionMode.Disabled,
        LandingSearchMode landingSearchMode = LandingSearchMode.Narrow,
        EjectionImpulseTargetMode impulseTargetMode = EjectionImpulseTargetMode.CenterOfMass,
        EjectionDeflectionMode deflectionMode = EjectionDeflectionMode.Disabled,
        ReattachmentMode reattachmentMode = ReattachmentMode.VelocitySettling,
        RasterizationCellParallelism cellParallelism = RasterizationCellParallelism.Sequential,
        BallisticResolutionParallelism resolutionParallelism = BallisticResolutionParallelism.Sequential,
        float ejectionVelocityThreshold = 6f,
        float settleSpeedThreshold = 0.5f,
        float restitution = 0.3f,
        float friction = 0.4f,
        float scatterSpeed = 1f,
        float velocityDampingFraction = 0.01f,
        float deflectionFactor = 0.5f,
        int rngSeed = 1337)
    {
        _cellsPerMeter = cellsPerMeter;
        _worldHeightCells = worldHeightCells;
        _gravityY = gravityY;
        _mode = mode;
        _resistanceMode = resistanceMode;
        _particleCollisionMode = particleCollisionMode;
        _landingSearchMode = landingSearchMode;
        _impulseTargetMode = impulseTargetMode;
        _deflectionMode = deflectionMode;
        _reattachmentMode = reattachmentMode;
        _resolutionParallelism = resolutionParallelism;
        _ejectionVelocityThreshold = ejectionVelocityThreshold;
        _settleSpeedThreshold = settleSpeedThreshold;
        _restitution = restitution;
        _friction = friction;
        _scatterSpeed = scatterSpeed;
        _velocityDampingFraction = velocityDampingFraction;
        _deflectionFactor = deflectionFactor;
        _rasterizer = new ShapeRasterizer(cellsPerMeter, worldHeightCells, cellParallelism);
        _rng = new Random(rngSeed);
    }

    // Used for rendering with PointSwarm.
    // TempRigidBody particles are "ordinary" rigid bodies drawn by RigidBodyRenderer, and thus not tracked here.
    public IReadOnlyList<AirborneParticle> AirborneParticles => _airborne;

    public void Advance(CaGrid grid, IRigidBodySimulation physics, float deltaTime)
    {
        if (_mode == BallisticParticleMode.None)
        {
            return;
        }

        DetectAndEject(grid, physics);

        if (_mode == BallisticParticleMode.PointSwarm)
        {
            AdvancePointSwarm(grid, physics, deltaTime);
        }
        else
        {
            AdvanceTempBodies(grid, physics);
        }
    }

    private static float BodyRadius(BodyState body) => body.Shape switch
    {
        BoxShape box => MathF.Sqrt((box.HalfWidth * box.HalfWidth) + (box.HalfHeight * box.HalfHeight)),
        CircleShape circle => circle.Radius,
        PolygonShape polygon => polygon.LocalVertices.Max(v => v.Length()),
        _ => 0f
    };

    private static Vector2 SafeNormalize(Vector2 v, Vector2 fallback) =>
        v.LengthSquared() > 1e-8f ? Vector2.Normalize(v) : fallback;

    // Used by SeparateOverlappingParticles' spatial hash to bucket a particle's location into an integer bin coordinate.
    private static (int Bx, int By) BinOf(Vector2 position, float binSize) =>
        ((int)MathF.Floor(position.X / binSize), (int)MathF.Floor(position.Y / binSize));

    // Sorts bin coordinates into SeparationColorGroupCount^2 groups by (bx mod n, by mod n)
    private static List<(int Bx, int By)>[] GroupBinsByColor(IEnumerable<(int Bx, int By)> keys)
    {
        var groups = new List<(int Bx, int By)>[SeparationColorGroupCount * SeparationColorGroupCount];
        for (int i = 0; i < groups.Length; i++)
        {
            groups[i] = [];
        }

        foreach (var key in keys)
        {
            int colorX = Mod(key.Bx, SeparationColorGroupCount);
            int colorY = Mod(key.By, SeparationColorGroupCount);
            groups[(colorX * SeparationColorGroupCount) + colorY].Add(key);
        }

        return groups;
    }

    private static int Mod(int value, int modulus) => ((value % modulus) + modulus) % modulus;

    private static bool IsValidLandingCell(CaGrid grid, int x, int y, CellType type)
    {
        if (!grid.InBounds(x, y) || grid.GetCell(x, y).Type != CellType.Empty)
        {
            return false;
        }

        int by = y + 1;
        if (!grid.InBounds(x, by))
        {
            return true;
        }

        return ElementProperties.GetDensity(grid.GetCell(x, by).Type) >= ElementProperties.GetDensity(type);
    }

    // v' = v - (1 + e)(v.n)n,
    // then the tangential component is scaled by (1 - friction).
    // Derived from techniques described here: Steeneken, P. (n.d.). Introductory dynamics: 2D kinematics and
    // kinetics of point masses and rigid bodies. LibreTexts Engineering. Sec. 8.5 (Collisions).
    // https://eng.libretexts.org/Bookshelves/Mechanical_Engineering/Introductory_Dynamics%3A_2D_Kinematics_and_Kinetics_of_Point_Masses_and_Rigid_Bodies_%28Steeneken%29/02%3A_Dynamics_of_Point_Masses/08%3A_Impulse_and_Momentum/8.05%3A_Collisions
    private static Vector2 Reflect(Vector2 velocity, Vector2 normal, float restitution, float friction)
    {
        float normalSpeed = Vector2.Dot(velocity, normal);
        var reflected = velocity - ((1f + restitution) * normalSpeed * normal);
        var normalComponent = Vector2.Dot(reflected, normal) * normal;
        var tangentComponent = (reflected - normalComponent) * (1f - friction);
        return normalComponent + tangentComponent;
    }

    // A CA cell is an ejection candidate if the rigid body impacting it is moving fast enough,
    // relative to that cell, along the (approximate) surface normal -
    // the standard 2D rigid-body point-velocity formula, v_point = v_linear + omega x r.
    // Citation: Steeneken (n.d.). Sec. 9.3 (Velocities in a rigid body).
    // https://eng.libretexts.org/Bookshelves/Mechanical_Engineering/Introductory_Dynamics%3A_2D_Kinematics_and_Kinetics_of_Point_Masses_and_Rigid_Bodies_(Steeneken)/03%3A_Rigid_Body_Dynamics/09%3A_Kinematics_of_Rigid_Bodies/9.03%3A_Velocities_in_a_rigid_body
    private void DetectAndEject(CaGrid grid, IRigidBodySimulation physics)
    {
        // In TempRigidBody mode the ejected particles are considered rigid bodies handled by the RB engine,
        // but they should not be able to eject other cells, as that can lead to lag spikes as ejections cascade.
        HashSet<int>? particleIds = _tempBodies.Count > 0
            ? _tempBodies.Select(particle => particle.Handle.Id).ToHashSet()
            : null;

        foreach (var body in physics.GetBodies())
        {
            if (body.IsStatic || (particleIds?.Contains(body.Handle.Id) ?? false))
            {
                continue;
            }

            var currentLinear = physics.GetLinearVelocity(body.Handle);
            float currentAngular = physics.GetAngularVelocity(body.Handle);

            // Rigid body systems resolve contacts/collision inside their own step, which occurs before this step.
            // Therefore we use the most recent velocity, not the current velocity.
            var (linear, angular) = _lastBodyVelocity.TryGetValue(body.Handle.Id, out var approach)
                ? approach
                : (Vector2.Zero, 0f);
            _lastBodyVelocity[body.Handle.Id] = (currentLinear, currentAngular);
            float bodyRadius = BodyRadius(body);

            _rasterizer.Iterate(body, marginCells: EjectionMarginCells, (cx, cy) =>
            {
                if (!grid.InBounds(cx, cy))
                {
                    return;
                }

                var cellType = grid.GetCell(cx, cy).Type;
                var category = ElementProperties.GetCategory(cellType);
                if (category != ParticleCategory.Powder && category != ParticleCategory.Liquid)
                {
                    return;
                }

                var vectorFromBodyToParticle = CellToWorld(cx, cy) - body.Position;

                // If the cell is directly at center of rigid body, skip, edge case (could eject randomly).
                if (vectorFromBodyToParticle == Vector2.Zero)
                {
                    return;
                }

                // The max distance for purposes of calculating rotational velocity is the radius of the object.
                var rotationArm = vectorFromBodyToParticle.Length() > bodyRadius ?
                    vectorFromBodyToParticle * (bodyRadius / vectorFromBodyToParticle.Length()) :
                    vectorFromBodyToParticle;

                // Formula: v_point = v_linear + omega x r.
                var pointVelocity = linear + (angular * new Vector2(-rotationArm.Y, rotationArm.X));
                var normal = Vector2.Normalize(vectorFromBodyToParticle);
                if (MathF.Abs(Vector2.Dot(pointVelocity, normal)) <= _ejectionVelocityThreshold)
                {
                    return;
                }

                Eject(grid, physics, body, cx, cy, cellType, pointVelocity);
            });
        }
    }

    private void Eject(CaGrid grid, IRigidBodySimulation physics, BodyState causingBody, int cx, int cy, CellType cellType, Vector2 pointVelocity)
    {
        var position = CellToWorld(cx, cy);
        grid.SetCell(cx, cy, new Cell { Type = CellType.Empty });
        ApplyEjectionResistance(physics, causingBody, position, cellType, pointVelocity);

        var scatter = new Vector2((float)((_rng.NextDouble() * 2) - 1), (float)((_rng.NextDouble() * 2) - 1)) * _scatterSpeed;
        var velocity = pointVelocity + scatter;

        // | >>>> · - a vertical wall moving right hits particle, it should go right.
        // \ >>>> · - a backwards slanted wall moving right hits particle, it should go up and right.
        // That is to say, the face normal should be taken into consideration when calculating the direction in which
        // particles are ejected (when EjectionDeflectionMode.FaceNormal is set)
        if (_deflectionMode == EjectionDeflectionMode.FaceNormal
            && _rasterizer.NearestFaceNormal(causingBody, position) is { } faceNormal)
        {
            velocity += faceNormal * pointVelocity.Length() * _deflectionFactor;
        }

        if (_mode == BallisticParticleMode.PointSwarm)
        {
            _airborne.Add(new AirborneParticle { Position = position, Velocity = velocity, CellType = cellType });
        }
        else
        {
            float halfCell = 0.5f / _cellsPerMeter;
            var handle = physics.AddDynamicBox(position.X, position.Y, halfCell, halfCell);
            physics.SetLinearVelocity(handle, velocity);
            _tempBodies.Add(new TempBodyParticle { Handle = handle, CellType = cellType, LastSpeed = velocity.Length() });
        }
    }

    // The new ballistic particle should apply force on the rigid body impacting it (Newton's Third Law).
    // Impulse mode conserves momentum against an assumed cell mass (the same density used by buoyancy calculations);
    // Impulse mode has two variations: Simply altering the rigid bodies speed (applying the impact to the linear velocity),
    // or applying the impact to a specific point on the rigid body, which can impact rotational velocity.
    // Damping mode instead removes a fixed fraction off the body's speed per cell.
    private void ApplyEjectionResistance(IRigidBodySimulation physics, BodyState causingBody, Vector2 contactPoint, CellType cellType, Vector2 pointVelocity)
    {
        if (_resistanceMode == EjectionResistanceMode.Impulse)
        {
            float bodyMass = physics.GetMass(causingBody.Handle);
            if (bodyMass <= 0f)
            {
                return;
            }

            float cellArea = 1f / (_cellsPerMeter * _cellsPerMeter);
            float cellMass = PhysicalProperties.DensityKgM3(cellType) * cellArea;
            var impulse = -cellMass * pointVelocity;
            if (_impulseTargetMode == EjectionImpulseTargetMode.ContactPoint)
            {
                physics.ApplyLinearImpulseAtPoint(causingBody.Handle, impulse, contactPoint);
            }
            else
            {
                var bodyVelocity = physics.GetLinearVelocity(causingBody.Handle);
                physics.SetLinearVelocity(causingBody.Handle, bodyVelocity + (impulse / bodyMass));
            }
        }
        else
        {
            var bodyVelocity = physics.GetLinearVelocity(causingBody.Handle);
            physics.SetLinearVelocity(causingBody.Handle, bodyVelocity * (1f - _velocityDampingFraction));
        }
    }

    // Semi-implicit Euler AKA Symplectic Euler Method (update velocity before position)
    // Citation: Stanford University. (2022). CS248B lecture 3: Particles [Lecture slides].
    // https://web.stanford.edu/class/cs248b/cgi-bin/autumn22content/lectures/03_particles/03_particles_slides.pdf
    //
    // When BallisticResolutionParallelism is enabled, runs as a two-phase compute-then-commit.
    // Unlike CA chunks, airborne particles have continuous positions, so these is no way to pre-partition them
    // into non-conflicting groups. Instead, RunPerParticle simultaneously computes each particle's new state,
    // then for any that are ready to settle, a landing proposal is formulated. Then CommitLandingProposals re-validates
    // and applies those proposals.
    private void AdvancePointSwarm(CaGrid grid, IRigidBodySimulation physics, float deltaTime)
    {
        if (_particleCollisionMode == ParticleCollisionMode.Enabled)
        {
            SeparateOverlappingParticles();
        }

        if (_airborne.Count == 0)
        {
            return;
        }

        var gravity = new Vector2(0f, _gravityY);
        var bodies = physics.GetBodies();
        var proposals = new ConcurrentBag<LandingProposal>();

        RunPerParticle(_airborne.Count, i =>
            _airborne[i] = AdvanceParticle(grid, bodies, gravity, deltaTime, i, proposals));

        CommitLandingProposals(grid, proposals);
    }

    // Computes a particle's (at index in _airborne) new state for this tick, but doesn't commit the change
    private AirborneParticle AdvanceParticle(
        CaGrid grid, IReadOnlyList<BodyState> bodies, Vector2 gravity, float deltaTime,
        int index, ConcurrentBag<LandingProposal> proposals)
    {
        var p = _airborne[index];
        p.Velocity += gravity * deltaTime;
        var nextPos = p.Position + (p.Velocity * deltaTime);

        var (cx, cy) = WorldToCell(nextPos);
        bool hitGrid = grid.InBounds(cx, cy) && grid.GetCell(cx, cy).Type != CellType.Empty;

        if (hitGrid)
        {
            bool readyToSettle = _reattachmentMode == ReattachmentMode.OnCollision
                || p.Velocity.Length() < _settleSpeedThreshold;

            if (readyToSettle && TryFindLandingCell(grid, p.Position, p.CellType, out int lx, out int ly))
            {
                proposals.Add(new LandingProposal(index, lx, ly, p.CellType));
            }

            // if it can't settle, bounce off of what it hit
            var normal = SafeNormalize(p.Position - CellToWorld(cx, cy), Vector2.UnitY);
            p.Velocity = Reflect(p.Velocity, normal, _restitution, _friction);
        }
        else if (FindCollidingBody(bodies, nextPos) is { } hitBody)
        {
            // Calculate the collision normal: the unit vector pointing away from
            // the center of the body into the current particle.
            var normal = SafeNormalize(nextPos - hitBody.Position, Vector2.UnitY);
            p.Velocity = Reflect(p.Velocity, normal, _restitution, _friction);
        }
        else
        {
            p.Position = nextPos;
        }

        return p;
    }

    // Two airborne particles could propose the same landing cell in the same tick.
    // Only the first proposal committed for a given cell can land there.
    // This can cause a small amount of nondeterminism, but it should be an edge case
    // that shouldn't affect benchmarking outcomes.
    private void CommitLandingProposals(CaGrid grid, ConcurrentBag<LandingProposal> proposals)
    {
        if (proposals.IsEmpty)
        {
            return;
        }

        var settledIndices = new List<int>();
        foreach (var proposal in proposals)
        {
            if (!IsValidLandingCell(grid, proposal.Cx, proposal.Cy, proposal.CellType))
            {
                continue;
            }

            grid.SetCell(proposal.Cx, proposal.Cy, new Cell { Type = proposal.CellType });
            settledIndices.Add(proposal.Index);
        }

        // Removed highest-index-first to avoid shifting other entries to remove in _airborne.
        settledIndices.Sort();
        for (int i = settledIndices.Count - 1; i >= 0; i--)
        {
            _airborne.RemoveAt(settledIndices[i]);
        }
    }

    private void RunPerParticle(int count, Action<int> compute)
    {
        if (_resolutionParallelism == BallisticResolutionParallelism.Sequential || count <= 1)
        {
            for (int i = 0; i < count; i++)
            {
                compute(i);
            }

            return;
        }

        Parallel.For(0, count, compute);
    }

    // Broad-phase spatial hash: particles are binned so only nearby (within minSeparation)
    // particles are ever pairwise-checked at all (near-O(n) is expected).
    // Each bin checks pairs within itself plus its "forward" neighbors (ForwardNeighborOffsets - east, north, northeast, southeast)
    // This means that for each adjacent pair of bins, only one of them ever includes the
    // other in its "forward" set, so every pair of nearby particles still gets checked exactly once
    // overall, just split across bins instead of one flat loop.
    //
    // Inspiration (but not copied from): Ericson, C. (2005). Real-time collision detection. Morgan Kaufmann. ISBN 978-1-55860-732-3.
    //
    // Under BallisticResolutionParallelism.Parallel, bins are processed one "color" group at a
    // time, each group's bins running concurrently via RunPerBin. Two bins sharing a color are always at
    // least SeparationColorGroupCount bins apart in x or y, so their neighbor checks (which only
    // ever reach 1 bin away) can never touch the same third bin at the same time.
    private void SeparateOverlappingParticles()
    {
        if (_airborne.Count < 2)
        {
            return;
        }

        float minSeparation = 0.5f / _cellsPerMeter;
        var bins = BuildSeparationBins(minSeparation);
        var colorGroups = GroupBinsByColor(bins.Keys);

        foreach (var colorBins in colorGroups)
        {
            if (colorBins.Count == 0)
            {
                continue;
            }

            RunPerBin(colorBins, bin => SeparateBinNeighborhood(bin, bins, minSeparation));
        }
    }

    private Dictionary<(int Bx, int By), List<int>> BuildSeparationBins(float binSize)
    {
        var bins = new Dictionary<(int Bx, int By), List<int>>();
        for (int i = 0; i < _airborne.Count; i++)
        {
            var key = BinOf(_airborne[i].Position, binSize);
            if (!bins.TryGetValue(key, out var indices))
            {
                indices = [];
                bins[key] = indices;
            }

            indices.Add(i);
        }

        return bins;
    }

    private void SeparateBinNeighborhood(
        (int Bx, int By) bin, Dictionary<(int Bx, int By), List<int>> bins, float minSeparation)
    {
        var self = bins[bin];
        SeparatePairsWithinBin(self, minSeparation);

        foreach (var (dx, dy) in ForwardNeighborOffsets)
        {
            if (bins.TryGetValue((bin.Bx + dx, bin.By + dy), out var neighbor))
            {
                SeparatePairsBetweenBins(self, neighbor, minSeparation);
            }
        }
    }

    private void SeparatePairsWithinBin(List<int> indices, float minSeparation)
    {
        for (int i = 0; i < indices.Count; i++)
        {
            for (int j = i + 1; j < indices.Count; j++)
            {
                SeparatePair(indices[i], indices[j], minSeparation);
            }
        }
    }

    private void SeparatePairsBetweenBins(List<int> a, List<int> b, float minSeparation)
    {
        foreach (int i in a)
        {
            foreach (int j in b)
            {
                SeparatePair(i, j, minSeparation);
            }
        }
    }

    // If distance is 0, separate laterally. Push is vector of separation.
    private void SeparatePair(int i, int j, float minSeparation)
    {
        var a = _airborne[i];
        var b = _airborne[j];
        var delta = b.Position - a.Position;
        float distance = delta.Length();
        if (distance >= minSeparation)
        {
            return;
        }

        var push = SafeNormalize(delta, Vector2.UnitX);
        float correction = (minSeparation - distance) * 0.5f;
        a.Position -= push * correction;
        b.Position += push * correction;
        _airborne[i] = a;
        _airborne[j] = b;
    }

    private void RunPerBin(List<(int Bx, int By)> colorBins, Action<(int Bx, int By)> compute)
    {
        if (_resolutionParallelism == BallisticResolutionParallelism.Sequential || colorBins.Count <= 1)
        {
            foreach (var bin in colorBins)
            {
                compute(bin);
            }

            return;
        }

        Parallel.ForEach(colorBins, compute);
    }

    private void AdvanceTempBodies(CaGrid grid, IRigidBodySimulation physics)
    {
        var bodiesById = physics.GetBodies().ToDictionary(b => b.Handle.Id);

        for (int i = _tempBodies.Count - 1; i >= 0; i--)
        {
            var particle = _tempBodies[i];
            var body = bodiesById[particle.Handle.Id];
            float speed = physics.GetLinearVelocity(particle.Handle).Length();

            bool settled = speed < _settleSpeedThreshold;
            bool impacted = particle.LastSpeed - speed > _ejectionVelocityThreshold;

            if ((settled || impacted) && TryReattach(grid, body.Position, particle.CellType))
            {
                physics.RemoveBody(particle.Handle);
                _tempBodies.RemoveAt(i);
                continue;
            }

            particle.LastSpeed = speed;
            _tempBodies[i] = particle;
        }
    }

    private BodyState? FindCollidingBody(IReadOnlyList<BodyState> bodies, Vector2 point)
    {
        foreach (var body in bodies)
        {
            if (_rasterizer.Contains(body, point))
            {
                return body;
            }
        }

        return null;
    }

    private bool TryReattach(CaGrid grid, Vector2 position, CellType type)
    {
        if (!TryFindLandingCell(grid, position, type, out int lx, out int ly))
        {
            return false;
        }

        grid.SetCell(lx, ly, new Cell { Type = type });
        return true;
    }

    private bool TryFindLandingCell(CaGrid grid, Vector2 position, CellType type, out int lx, out int ly)
    {
        var (cx, cy) = WorldToCell(position);
        foreach (var (dx, dy) in LandingOffsets)
        {
            int x = cx + dx, y = cy + dy;
            if (IsValidLandingCell(grid, x, y, type))
            {
                lx = x;
                ly = y;
                return true;
            }
        }

        // searches for a landing position in a wider radius around the current location.
        if (_landingSearchMode == LandingSearchMode.Wide)
        {
            for (int radius = 2; radius <= WideSearchMaxRadius; radius++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    for (int dx = -radius; dx <= radius; dx++)
                    {
                        if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != radius)
                        {
                            continue;
                        }

                        int x = cx + dx;
                        int y = cy + dy;
                        if (IsValidLandingCell(grid, x, y, type))
                        {
                            lx = x;
                            ly = y;
                            return true;
                        }
                    }
                }
            }
        }

        // Fallback if no landing cell is identified nearby: Attempts to find the first empty cell upwards from it.
        for (int y = cy - 1; y >= 0; y--)
        {
            if (IsValidLandingCell(grid, cx, y, type))
            {
                lx = cx;
                ly = y;
                return true;
            }
        }

        lx = cx;
        ly = cy;
        return false;
    }

    private Vector2 CellToWorld(int cx, int cy) => new(
        (cx + 0.5f) / _cellsPerMeter,
        (_worldHeightCells - cy - 0.5f) / _cellsPerMeter);

    private (int Cx, int Cy) WorldToCell(Vector2 pos) => (
        (int)MathF.Floor(pos.X * _cellsPerMeter),
        _worldHeightCells - 1 - (int)MathF.Floor(pos.Y * _cellsPerMeter));

    private struct TempBodyParticle
    {
        public BodyHandle Handle;
        public CellType CellType;
        public float LastSpeed;
    }

    private readonly struct LandingProposal
    {
        public LandingProposal(int index, int cx, int cy, CellType cellType)
        {
            Index = index;
            Cx = cx;
            Cy = cy;
            CellType = cellType;
        }

        public int Index { get; }

        public int Cx { get; }

        public int Cy { get; }

        public CellType CellType { get; }
    }
}
