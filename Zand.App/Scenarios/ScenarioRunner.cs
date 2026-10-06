namespace Zand.App.Scenarios;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Zand.App.Config;
using Zand.Core;
using Zand.Core.Ballistics;
using Zand.Core.CellularAutomata;
using Zand.Core.CellularAutomata.Algorithms;
using Zand.Core.CellularAutomata.Chunking;
using Zand.Core.Coupling;
using Zand.Core.RigidBody;
using Zand.Core.RigidBody.Box2D;
using Zand.Core.RigidBody.Chipmunk;
using Zand.Rendering;

public class ScenarioRunner
{
    private readonly WorldConfig _worldConfigTemplate;
    private readonly WorldSizeOption[] _worldSizeOptions;
    private readonly CaAlgorithmType[] _caAlgorithms;
    private readonly RigidBodySimulationType[] _rbSimulations;
    private readonly CouplingMode[] _couplingModes;
    private readonly LiquidPhysicsMode[] _liquidPhysicsModes;
    private readonly GravityMode[] _gravityModes;
    private readonly TempBodyLifetime[] _tempBodyLifetimes;
    private readonly TempBodyMerging[] _tempBodyMergings;
    private readonly ChunkSize[] _chunkSizes;
    private readonly ActiveRectangle[] _activeRectangleModes;
    private readonly ParallelismMode[] _parallelismModes;
    private readonly LiquidPressureMode[] _liquidPressureModes;
    private readonly SameTickMoveGuard[] _sameTickMoveGuards;
    private readonly RowSweepOrder[] _rowSweepOrders;
    private readonly CellRenderSmoothing[] _cellRenderSmoothings;
    private readonly BallisticParticleMode[] _ballisticParticleModes;
    private readonly EjectionResistanceMode[] _ejectionResistanceModes;
    private readonly ParticleCollisionMode[] _particleCollisionModes;
    private readonly LandingSearchMode[] _landingSearchModes;
    private readonly EjectionImpulseTargetMode[] _ejectionImpulseTargetModes;
    private readonly EjectionDeflectionMode[] _ejectionDeflectionModes;
    private readonly ReattachmentMode[] _reattachmentModes;
    private readonly RasterizationBodyParallelism[] _rasterizationBodyParallelisms;
    private readonly RasterizationCellParallelism[] _rasterizationCellParallelisms;
    private readonly BallisticResolutionParallelism[] _ballisticResolutionParallelisms;
    private readonly RenderParallelism[] _renderParallelisms;
    private readonly int _repeatCount;
    private readonly Func<SampleConfig, SampleConfig>? _pinReasonableConfig;
    private readonly int _repeatIndexStart;
    private readonly Func<SampleConfig, bool>? _includeReasonableConfig;

    private List<ScenarioEntry> _queue = [];
    private int _currentIndex;
    private int _currentFrame;
    private bool _reasonableUsesHeavyScenario;
    private bool _reasonableUsesBodyStressScenario;

    public ScenarioRunner(
        WorldConfig worldConfig,
        WorldSizeOption[] worldSizeOptions,
        CaAlgorithmType[] caAlgorithms,
        RigidBodySimulationType[] rbSimulations,
        CouplingMode[] couplingModes,
        LiquidPhysicsMode[] liquidPhysicsModes,
        GravityMode[] gravityModes,
        TempBodyLifetime[] tempBodyLifetimes,
        TempBodyMerging[] tempBodyMergings,
        ChunkSize[] chunkSizes,
        ActiveRectangle[] activeRectangleModes,
        ParallelismMode[] parallelismModes,
        LiquidPressureMode[] liquidPressureModes,
        SameTickMoveGuard[] sameTickMoveGuards,
        RowSweepOrder[] rowSweepOrders,
        CellRenderSmoothing[] cellRenderSmoothings,
        BallisticParticleMode[] ballisticParticleModes,
        EjectionResistanceMode[] ejectionResistanceModes,
        ParticleCollisionMode[] particleCollisionModes,
        LandingSearchMode[] landingSearchModes,
        EjectionImpulseTargetMode[] ejectionImpulseTargetModes,
        EjectionDeflectionMode[] ejectionDeflectionModes,
        ReattachmentMode[] reattachmentModes,
        RasterizationBodyParallelism[] rasterizationBodyParallelisms,
        RasterizationCellParallelism[] rasterizationCellParallelisms,
        BallisticResolutionParallelism[] ballisticResolutionParallelisms,
        RenderParallelism[] renderParallelisms,
        int repeatCount = 1,
        Func<SampleConfig, SampleConfig>? pinReasonableConfig = null,
        int repeatIndexStart = 0,
        Func<SampleConfig, bool>? includeReasonableConfig = null)
    {
        _repeatCount = Math.Max(1, repeatCount);
        _pinReasonableConfig = pinReasonableConfig;
        _repeatIndexStart = Math.Max(0, repeatIndexStart);
        _includeReasonableConfig = includeReasonableConfig;
        _worldConfigTemplate = worldConfig;
        _worldSizeOptions = worldSizeOptions;
        _caAlgorithms = caAlgorithms;
        _rbSimulations = rbSimulations;
        _couplingModes = couplingModes;
        _liquidPhysicsModes = liquidPhysicsModes;
        _gravityModes = gravityModes;
        _tempBodyLifetimes = tempBodyLifetimes;
        _tempBodyMergings = tempBodyMergings;
        _chunkSizes = chunkSizes;
        _activeRectangleModes = activeRectangleModes;
        _parallelismModes = parallelismModes;
        _liquidPressureModes = liquidPressureModes;
        _sameTickMoveGuards = sameTickMoveGuards;
        _rowSweepOrders = rowSweepOrders;
        _cellRenderSmoothings = cellRenderSmoothings;
        _ballisticParticleModes = ballisticParticleModes;
        _ejectionResistanceModes = ejectionResistanceModes;
        _particleCollisionModes = particleCollisionModes;
        _landingSearchModes = landingSearchModes;
        _ejectionImpulseTargetModes = ejectionImpulseTargetModes;
        _ejectionDeflectionModes = ejectionDeflectionModes;
        _reattachmentModes = reattachmentModes;
        _rasterizationBodyParallelisms = rasterizationBodyParallelisms;
        _rasterizationCellParallelisms = rasterizationCellParallelisms;
        _ballisticResolutionParallelisms = ballisticResolutionParallelisms;
        _renderParallelisms = renderParallelisms;
    }

    public static IEnumerable<(string Name, string[] Tags)> AvailableScenarios =>
        DiscoverMethods().Select(m => (m.Name, m.GetCustomAttribute<ScenarioAttribute>()!.Tags));

    public static IReadOnlyDictionary<string, int> ScenarioFrameBudgets =>
        DiscoverMethods().ToDictionary(m => m.Name, m => m.GetCustomAttribute<ScenarioAttribute>()!.FrameBudget);

    public static IEnumerable<string> AvailableElements =>
        DiscoverElements().Select(m => m.Name);

    public static IEnumerable<string> AvailableCoreScenarios =>
        DiscoverCoreScenarios().Select(m => m.Name);

    public static int CoreScenarioFrameBudgetTotal =>
        DiscoverCoreScenarios().Sum(m => m.GetCustomAttribute<ScenarioAttribute>()!.FrameBudget);

    public static IEnumerable<string> AvailableHeavyScenarios =>
        DiscoverHeavyScenarios().Select(m => m.Name);

    public static int HeavyScenarioFrameBudget =>
        DiscoverHeavyScenarios().Single().GetCustomAttribute<ScenarioAttribute>()!.FrameBudget;

    public static int BodyStressScenarioFrameBudget =>
        DiscoverBodyStressScenarios().Single().GetCustomAttribute<ScenarioAttribute>()!.FrameBudget;

    public SimulationWorld Sim { get; private set; } = null!;

    public Zand.Core.CellularAutomata.CaGrid Grid => Sim.Grid;

    public IRigidBodySimulation Physics => Sim.Physics;

    public CameraPanAttribute? CurrentCameraPan { get; private set; }

    public bool AllScenariosCompleted => _currentIndex >= _queue.Count;

    public int CurrentRunNumber => Math.Min(_currentIndex + 1, _queue.Count);

    public int TotalRunCount => _queue.Count;

    public int CurrentFrame => _currentFrame;

    public int? CurrentFrameBudget => AllScenariosCompleted ? null : _queue[_currentIndex].FrameBudget;

    public ChunkSize CurrentChunkSize => AllScenariosCompleted ? ChunkSize.Disabled : _queue[_currentIndex].ChunkSize;

    public ActiveRectangle CurrentActiveRectangleMode =>
        AllScenariosCompleted ? ActiveRectangle.Disabled : _queue[_currentIndex].ActiveRectangleMode;

    public CellRenderSmoothing CurrentCellRenderSmoothing =>
        AllScenariosCompleted ? CellRenderSmoothing.Disabled : _queue[_currentIndex].CellRenderSmoothing;

    public RenderParallelism CurrentRenderParallelism =>
        AllScenariosCompleted ? RenderParallelism.Sequential : _queue[_currentIndex].RenderParallelism;

    public int CurrentDefaultCameraX =>
        AllScenariosCompleted ? 0 : (_queue[_currentIndex].WorldConfig.Width - SimConstants.CameraWidthCells) / 2;

    public int CurrentDefaultCameraY =>
        AllScenariosCompleted ? 0 : _queue[_currentIndex].WorldConfig.Height - SimConstants.CameraHeightCells;

    public RunConfig? CurrentRunConfig => AllScenariosCompleted
        ? null
        : new RunConfig(
            _queue[_currentIndex].Name,
            _queue[_currentIndex].RepeatIndex,
            _queue[_currentIndex].WorldConfig.Width,
            _queue[_currentIndex].WorldConfig.Height,
            _queue[_currentIndex].CaAlgorithm,
            _queue[_currentIndex].RbSimulation,
            _queue[_currentIndex].Coupling,
            _queue[_currentIndex].LiquidPhysics,
            _queue[_currentIndex].Gravity,
            _queue[_currentIndex].TempBodyLifetime,
            _queue[_currentIndex].TempBodyMerging,
            _queue[_currentIndex].ChunkSize,
            _queue[_currentIndex].ActiveRectangleMode,
            _queue[_currentIndex].ParallelismMode,
            _queue[_currentIndex].LiquidPressureMode,
            _queue[_currentIndex].SameTickMoveGuard,
            _queue[_currentIndex].RowSweepOrder,
            _queue[_currentIndex].CellRenderSmoothing,
            _queue[_currentIndex].BallisticParticleMode,
            _queue[_currentIndex].EjectionResistanceMode,
            _queue[_currentIndex].ParticleCollisionMode,
            _queue[_currentIndex].LandingSearchMode,
            _queue[_currentIndex].EjectionImpulseTargetMode,
            _queue[_currentIndex].EjectionDeflectionMode,
            _queue[_currentIndex].ReattachmentMode,
            _queue[_currentIndex].RasterizationBodyParallelism,
            _queue[_currentIndex].RasterizationCellParallelism,
            _queue[_currentIndex].BallisticResolutionParallelism,
            _queue[_currentIndex].RenderParallelism);

    // NoCoupling / WeakOneWayRbToCa are not valid with liquid physics modes;
    // As CA cannot cause effects in Rigid Body in either.
    public static bool IsValidCombination(CouplingMode coupling, LiquidPhysicsMode liquidPhysics)
    {
        bool liquidPhysicsInert = coupling is CouplingMode.None or CouplingMode.WeakOneWayRbToCa;
        return !liquidPhysicsInert || liquidPhysics == LiquidPhysicsMode.None;
    }

    // Only couplings that spawn temp bodies (everything except None and WeakOneWayRbToCa) use TempBodyLifetime.
    // In other couplings both values behave identically, so only
    // Persistent is kept to avoid running duplicate configs.
    public static bool IsValidTempBodyLifetime(CouplingMode coupling, TempBodyLifetime tempBodyLifetime)
    {
        bool tempBodiesUnused = coupling is CouplingMode.None or CouplingMode.WeakOneWayRbToCa;
        return !tempBodiesUnused || tempBodyLifetime == TempBodyLifetime.Persistent;
    }

    // ProbabilisticCollision works by turning random liquid cells into temp bodies,
    // but Persistent temp bodies never include liquid cells,
    // so with Persistent it would silently behave like None.
    public static bool IsValidLiquidTempBodyLifetime(LiquidPhysicsMode liquidPhysics, TempBodyLifetime tempBodyLifetime)
    {
        return liquidPhysics != LiquidPhysicsMode.ProbabilisticCollision || tempBodyLifetime == TempBodyLifetime.Ephemeral;
    }

    // Merging only groups the temp sand bodies, so like TempBodyLifetime it has no effect on
    // couplings that never create temp bodies.
    // Only NoMerging is kept there to avoid running duplicate configs.
    public static bool IsValidTempBodyMerging(CouplingMode coupling, TempBodyMerging tempBodyMerging)
    {
        bool tempBodiesUnused = coupling is CouplingMode.None or CouplingMode.WeakOneWayRbToCa;
        return !tempBodiesUnused || tempBodyMerging == TempBodyMerging.NoMerging;
    }

    // Parallel mode processes chunks in parallel, is not valid if chunking is disabled.
    public static bool IsValidParallelism(ParallelismMode parallelism, ChunkSize chunkSize)
    {
        return parallelism == ParallelismMode.Sequential || chunkSize != ChunkSize.Disabled;
    }

    // Ballistics-tuning axes are only valid when ballistics is enabled (BallisticParticleMode != None).
    // Ballistics-tuning axes: EjectionResistanceMode, ParticleCollisionMode, LandingSearchMode,
    // EjectionImpulseTargetMode, EjectionDeflectionMode, ReattachmentMode
    // ParticleCollisionMode and ReattachmentMode are themselves only valid when
    // BallisticParticleMode.PointSwarm is active - both only affect AdvancePointSwarm
    // (RigidBody mode collision/settling is handled by the Rigid Body Engine instead).
    // EjectionImpulseTargetMode is only valid with EjectionResistanceMode.Impulse.
    public static bool IsValidBallisticCombination(
        BallisticParticleMode mode, EjectionResistanceMode resistance,
        ParticleCollisionMode particleCollision, LandingSearchMode landingSearch,
        EjectionImpulseTargetMode impulseTarget, EjectionDeflectionMode deflection,
        ReattachmentMode reattachment)
    {
        bool ballisticsActive = mode != BallisticParticleMode.None;
        bool pointSwarmActive = mode == BallisticParticleMode.PointSwarm;

        bool baselineWhenInactive = ballisticsActive
            || (resistance == EjectionResistanceMode.Impulse
                && particleCollision == ParticleCollisionMode.Disabled
                && landingSearch == LandingSearchMode.Narrow
                && impulseTarget == EjectionImpulseTargetMode.CenterOfMass
                && deflection == EjectionDeflectionMode.Disabled
                && reattachment == ReattachmentMode.VelocitySettling);

        bool pointSwarmOnlyAxesValid = pointSwarmActive
            || (particleCollision == ParticleCollisionMode.Disabled
                && reattachment == ReattachmentMode.VelocitySettling);

        bool impulseTargetValid = resistance == EjectionResistanceMode.Impulse
            || impulseTarget == EjectionImpulseTargetMode.CenterOfMass;

        return baselineWhenInactive && pointSwarmOnlyAxesValid && impulseTargetValid;
    }

    // Only PointSwarm resolution loop is parallelized.
    // TempRigidBody particles go through the
    // rigid body engine, which is not.
    public static bool IsValidBallisticResolutionParallelism(
        BallisticParticleMode mode, BallisticResolutionParallelism resolutionParallelism)
    {
        return resolutionParallelism == BallisticResolutionParallelism.Sequential || mode == BallisticParticleMode.PointSwarm;
    }

    // parameter notes:
    // pinReasonableConfig, when given, adjusts each reasonable config before the test axis value is applied.
    // This lets a run hold axes at specific values based on logic instead of the reasonable configs' own.
    // includeReasonableConfig, when given, skips reasonable configs it returns false for (checked before pinning).
    public static IEnumerable<SampleConfig> ValidReasonableCombinations<T>(
        T[] testAxisValues, Func<SampleConfig, T, SampleConfig> withAxis,
        Func<SampleConfig, SampleConfig>? pinReasonableConfig = null,
        Func<SampleConfig, bool>? includeReasonableConfig = null)
    {
        return
            from reasonableConfig in ReasonableConfigs.All
            where includeReasonableConfig?.Invoke(reasonableConfig) ?? true
            let baseConfig = pinReasonableConfig?.Invoke(reasonableConfig) ?? reasonableConfig
            from testValue in testAxisValues
            let config = withAxis(baseConfig, testValue)
            where IsValidCombination(config.Coupling, config.LiquidPhysics)
                && IsValidTempBodyLifetime(config.Coupling, config.TempBodyLifetime)
                && IsValidLiquidTempBodyLifetime(config.LiquidPhysics, config.TempBodyLifetime)
                && IsValidTempBodyMerging(config.Coupling, config.TempBodyMerging)
                && IsValidParallelism(config.Parallelism, config.ChunkSize)
                && IsValidBallisticCombination(config.BallisticParticles, config.EjectionResistance,
                    config.ParticleCollision, config.LandingSearch, config.EjectionImpulseTarget, config.EjectionDeflection,
                    config.Reattachment)
                && IsValidBallisticResolutionParallelism(config.BallisticParticles, config.BallisticResolutionParallelism)
            select config;
    }

    public static IEnumerable<SampleConfig> ValidHeavyCombinations(
        WorldSizeOption[] worldSizeOptions, ChunkSize[] chunkSizes, ActiveRectangle[] activeRectangleModes, ParallelismMode[] parallelismModes,
        RasterizationBodyParallelism[] rasterizationBodyParallelisms, RasterizationCellParallelism[] rasterizationCellParallelisms,
        BallisticResolutionParallelism[] ballisticResolutionParallelisms, RenderParallelism[] renderParallelisms)
    {
        return
            from reasonableConfig in ReasonableConfigs.All
            from worldSize in worldSizeOptions
            from chunkSize in chunkSizes
            from activeRectangle in activeRectangleModes
            from parallelism in parallelismModes
            from rasterizationBodyParallelism in rasterizationBodyParallelisms
            from rasterizationCellParallelism in rasterizationCellParallelisms
            from ballisticResolutionParallelism in ballisticResolutionParallelisms
            from renderParallelism in renderParallelisms
            let config = reasonableConfig with
            {
                WorldSize = worldSize,
                ChunkSize = chunkSize,
                ActiveRectangle = activeRectangle,
                Parallelism = parallelism,
                RasterizationBodyParallelism = rasterizationBodyParallelism,
                RasterizationCellParallelism = rasterizationCellParallelism,
                BallisticResolutionParallelism = ballisticResolutionParallelism,
                RenderParallelism = renderParallelism
            }
            where IsValidParallelism(config.Parallelism, config.ChunkSize)
                && IsValidBallisticResolutionParallelism(config.BallisticParticles, config.BallisticResolutionParallelism)
            select config;
    }

    public void RunHeavyBenchmark()
    {
        var heavyMethod = DiscoverHeavyScenarios().Single();

        var pass = (
            from config in ValidHeavyCombinations(
                _worldSizeOptions, _chunkSizes, _activeRectangleModes, _parallelismModes,
                _rasterizationBodyParallelisms, _rasterizationCellParallelisms, _ballisticResolutionParallelisms,
                _renderParallelisms)
            select new ScenarioEntry(
                heavyMethod.Name,
                ctx => heavyMethod.Invoke(null, [ctx]),
                heavyMethod.GetCustomAttribute<CameraPanAttribute>(),
                heavyMethod.GetCustomAttribute<ScenarioAttribute>()!.FrameBudget,
                BuildWorldConfig(config.WorldSize),
                config.CaAlgorithm, config.RbSimulation, config.Coupling, config.LiquidPhysics, config.Gravity,
                config.TempBodyLifetime, config.TempBodyMerging, config.ChunkSize, config.ActiveRectangle, config.Parallelism, config.LiquidPressure,
                config.SameTickMoveGuard, config.RowSweepOrder, config.CellRenderSmoothing, config.BallisticParticles, config.EjectionResistance,
                config.ParticleCollision, config.LandingSearch, config.EjectionImpulseTarget, config.EjectionDeflection,
                config.Reattachment, config.RasterizationBodyParallelism, config.RasterizationCellParallelism,
                config.BallisticResolutionParallelism, config.RenderParallelism)).ToList();

        StartAlternatingByConfig(pass, scenariosPerConfig: 1);
    }

    public void RunAll() => Start(BuildEntriesFromMethods(DiscoverMethods()));

    public void RunFiltered(params string[] tags) => Start(BuildEntriesFromMethods(
        DiscoverMethods().Where(m => m.GetCustomAttribute<ScenarioAttribute>()!.Tags.Intersect(tags).Any())));

    public void RunSingle(string name) =>
        Start(BuildEntriesFromMethods(DiscoverMethods().Where(m => m.Name == name)));

    public void RunSelected(IEnumerable<string> names) =>
        Start(BuildEntriesFromMethods(DiscoverMethods().Where(m => names.Contains(m.Name))));

    public void RunElements(IEnumerable<string> elementNames)
    {
        var selected = new HashSet<string>(elementNames);
        var methods = DiscoverElements().Where(m => selected.Contains(m.Name)).ToList();
        string name = $"Custom[{string.Join("+", methods.Select(m => m.Name))}]";
        Action<ScenarioContext> run = ctx =>
        {
            foreach (var method in methods)
            {
                method.Invoke(null, [ctx]);
            }
        };

        Start(
            from worldSize in _worldSizeOptions
            from ca in _caAlgorithms
            from rb in _rbSimulations
            from coupling in _couplingModes
            from liquidPhysics in _liquidPhysicsModes
            from gravity in _gravityModes
            from tempBodyLifetime in _tempBodyLifetimes
            from tempBodyMerging in _tempBodyMergings
            from chunkSize in _chunkSizes
            from activeRectangleMode in _activeRectangleModes
            from parallelismMode in _parallelismModes
            from liquidPressureMode in _liquidPressureModes
            from sameTickMoveGuard in _sameTickMoveGuards
            from rowSweepOrder in _rowSweepOrders
            from cellRenderSmoothing in _cellRenderSmoothings
            from ballisticParticleMode in _ballisticParticleModes
            from ejectionResistanceMode in _ejectionResistanceModes
            from particleCollisionMode in _particleCollisionModes
            from landingSearchMode in _landingSearchModes
            from ejectionImpulseTargetMode in _ejectionImpulseTargetModes
            from ejectionDeflectionMode in _ejectionDeflectionModes
            from reattachmentMode in _reattachmentModes
            from rasterizationBodyParallelism in _rasterizationBodyParallelisms
            from rasterizationCellParallelism in _rasterizationCellParallelisms
            from ballisticResolutionParallelism in _ballisticResolutionParallelisms
            from renderParallelism in _renderParallelisms
            where IsValidCombination(coupling, liquidPhysics) && IsValidTempBodyLifetime(coupling, tempBodyLifetime)
                && IsValidLiquidTempBodyLifetime(liquidPhysics, tempBodyLifetime)
                && IsValidTempBodyMerging(coupling, tempBodyMerging)
                && IsValidParallelism(parallelismMode, chunkSize)
                && IsValidBallisticCombination(ballisticParticleMode, ejectionResistanceMode,
                    particleCollisionMode, landingSearchMode, ejectionImpulseTargetMode, ejectionDeflectionMode,
                    reattachmentMode)
                && IsValidBallisticResolutionParallelism(ballisticParticleMode, ballisticResolutionParallelism)
            select new ScenarioEntry(
                name, run, null, null, BuildWorldConfig(worldSize),
                ca, rb, coupling, liquidPhysics, gravity, tempBodyLifetime, tempBodyMerging, chunkSize, activeRectangleMode, parallelismMode, liquidPressureMode,
                sameTickMoveGuard, rowSweepOrder, cellRenderSmoothing, ballisticParticleMode, ejectionResistanceMode,
                particleCollisionMode, landingSearchMode, ejectionImpulseTargetMode, ejectionDeflectionMode, reattachmentMode,
                rasterizationBodyParallelism, rasterizationCellParallelism, ballisticResolutionParallelism,
                renderParallelism));
    }

    public void RunCoreScenariosReasonable(BenchmarkAxis testAxis, bool useHeavyScenario = false, bool useBodyStressScenario = false)
    {
        if (useHeavyScenario && useBodyStressScenario)
        {
            throw new ArgumentException("Only one of the heavy and body stress scenarios can replace the core scenarios.");
        }

        _reasonableUsesHeavyScenario = useHeavyScenario;
        _reasonableUsesBodyStressScenario = useBodyStressScenario;
        switch (testAxis)
        {
            case BenchmarkAxis.WorldSize:
                RunCoreScenariosReasonable(_worldSizeOptions, (c, v) => c with { WorldSize = v });
                break;
            case BenchmarkAxis.CaAlgorithm:
                RunCoreScenariosReasonable(_caAlgorithms, (c, v) => c with { CaAlgorithm = v });
                break;
            case BenchmarkAxis.RbSimulation:
                RunCoreScenariosReasonable(_rbSimulations, (c, v) => c with { RbSimulation = v });
                break;
            case BenchmarkAxis.CouplingMode:
                RunCoreScenariosReasonable(_couplingModes, (c, v) => c with { Coupling = v });
                break;
            case BenchmarkAxis.LiquidPhysics:
                RunCoreScenariosReasonable(_liquidPhysicsModes, (c, v) => c with { LiquidPhysics = v });
                break;
            case BenchmarkAxis.Gravity:
                RunCoreScenariosReasonable(_gravityModes, (c, v) => c with { Gravity = v });
                break;
            case BenchmarkAxis.TempBodyLifetime:
                RunCoreScenariosReasonable(_tempBodyLifetimes, (c, v) => c with { TempBodyLifetime = v });
                break;
            case BenchmarkAxis.TempBodyMerging:
                RunCoreScenariosReasonable(_tempBodyMergings, (c, v) => c with { TempBodyMerging = v });
                break;
            case BenchmarkAxis.ChunkSize:
                RunCoreScenariosReasonable(_chunkSizes, (c, v) => c with { ChunkSize = v });
                break;
            case BenchmarkAxis.ActiveRectangle:
                RunCoreScenariosReasonable(_activeRectangleModes, (c, v) => c with { ActiveRectangle = v });
                break;
            case BenchmarkAxis.Parallelism:
                RunCoreScenariosReasonable(_parallelismModes, (c, v) => c with { Parallelism = v });
                break;
            case BenchmarkAxis.LiquidPressure:
                RunCoreScenariosReasonable(_liquidPressureModes, (c, v) => c with { LiquidPressure = v });
                break;
            case BenchmarkAxis.SameTickMoveGuard:
                RunCoreScenariosReasonable(_sameTickMoveGuards, (c, v) => c with { SameTickMoveGuard = v });
                break;
            case BenchmarkAxis.RowSweepOrder:
                RunCoreScenariosReasonable(_rowSweepOrders, (c, v) => c with { RowSweepOrder = v });
                break;
            case BenchmarkAxis.CellRenderSmoothing:
                RunCoreScenariosReasonable(_cellRenderSmoothings, (c, v) => c with { CellRenderSmoothing = v });
                break;
            case BenchmarkAxis.BallisticParticles:
                RunCoreScenariosReasonable(_ballisticParticleModes, (c, v) => c with { BallisticParticles = v });
                break;
            case BenchmarkAxis.EjectionResistance:
                RunCoreScenariosReasonable(_ejectionResistanceModes, (c, v) => c with { EjectionResistance = v });
                break;
            case BenchmarkAxis.ParticleCollision:
                RunCoreScenariosReasonable(_particleCollisionModes, (c, v) => c with { ParticleCollision = v });
                break;
            case BenchmarkAxis.LandingSearch:
                RunCoreScenariosReasonable(_landingSearchModes, (c, v) => c with { LandingSearch = v });
                break;
            case BenchmarkAxis.EjectionImpulseTarget:
                RunCoreScenariosReasonable(_ejectionImpulseTargetModes, (c, v) => c with { EjectionImpulseTarget = v });
                break;
            case BenchmarkAxis.EjectionDeflection:
                RunCoreScenariosReasonable(_ejectionDeflectionModes, (c, v) => c with { EjectionDeflection = v });
                break;
            case BenchmarkAxis.Reattachment:
                RunCoreScenariosReasonable(_reattachmentModes, (c, v) => c with { Reattachment = v });
                break;
            case BenchmarkAxis.RasterizationBodyParallelism:
                RunCoreScenariosReasonable(_rasterizationBodyParallelisms, (c, v) => c with { RasterizationBodyParallelism = v });
                break;
            case BenchmarkAxis.RasterizationCellParallelism:
                RunCoreScenariosReasonable(_rasterizationCellParallelisms, (c, v) => c with { RasterizationCellParallelism = v });
                break;
            case BenchmarkAxis.RenderParallelism:
                RunCoreScenariosReasonable(_renderParallelisms, (c, v) => c with { RenderParallelism = v });
                break;
            case BenchmarkAxis.BallisticResolutionParallelism:
                RunCoreScenariosReasonable(_ballisticResolutionParallelisms, (c, v) => c with { BallisticResolutionParallelism = v });
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(testAxis));
        }
    }

    public bool Tick()
    {
        if (AllScenariosCompleted)
        {
            return false;
        }

        _currentFrame++;
        int? budget = _queue[_currentIndex].FrameBudget;
        if (budget == null || _currentFrame < budget.Value)
        {
            return false;
        }

        return AdvanceQueue();
    }

    public bool EndCurrentScenario()
    {
        if (AllScenariosCompleted)
        {
            return false;
        }

        return AdvanceQueue();
    }

    private static IEnumerable<MethodInfo> DiscoverMethods()
    {
        return Assembly.GetExecutingAssembly()
            .GetTypes()
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static))
            .Where(m => m.GetCustomAttribute<ScenarioAttribute>() != null);
    }

    private static IEnumerable<MethodInfo> DiscoverElements()
    {
        return Assembly.GetExecutingAssembly()
            .GetTypes()
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static))
            .Where(m => m.GetCustomAttribute<ScenarioElementAttribute>() != null)
            .OrderBy(m => m.MetadataToken);
    }

    private static IEnumerable<MethodInfo> DiscoverCoreScenarios()
    {
        return DiscoverMethods().Where(m => m.GetCustomAttribute<CoreScenarioAttribute>() != null);
    }

    private static IEnumerable<MethodInfo> DiscoverHeavyScenarios()
    {
        return DiscoverMethods().Where(m => m.GetCustomAttribute<HeavyScenarioAttribute>() != null);
    }

    private static IEnumerable<MethodInfo> DiscoverBodyStressScenarios()
    {
        return DiscoverMethods().Where(m => m.GetCustomAttribute<BodyStressScenarioAttribute>() != null);
    }

    // Configs being compared should invert in order each repeated run,
    // that way any memory leaks or thermal throttling or similar issues that build over time are spread evenly
    // instead of impacting some configs more than others
    private static IEnumerable<ScenarioEntry> ReversedByConfig(List<ScenarioEntry> pass, int scenariosPerConfig)
    {
        for (int configStart = pass.Count - scenariosPerConfig; configStart >= 0; configStart -= scenariosPerConfig)
        {
            for (int i = 0; i < scenariosPerConfig; i++)
            {
                yield return pass[configStart + i];
            }
        }
    }

    private void RunCoreScenariosReasonable<T>(T[] testAxisValues, Func<SampleConfig, T, SampleConfig> withAxis)
    {
        var coreMethods = (_reasonableUsesHeavyScenario
            ? DiscoverHeavyScenarios()
            : _reasonableUsesBodyStressScenario
                ? DiscoverBodyStressScenarios()
                : DiscoverCoreScenarios()).ToList();

        var pass = (
            from config in ValidReasonableCombinations(testAxisValues, withAxis, _pinReasonableConfig, _includeReasonableConfig)
            from method in coreMethods
            select new ScenarioEntry(
                method.Name,
                ctx => method.Invoke(null, [ctx]),
                method.GetCustomAttribute<CameraPanAttribute>(),
                method.GetCustomAttribute<ScenarioAttribute>()!.FrameBudget,
                BuildWorldConfig(config.WorldSize),
                config.CaAlgorithm, config.RbSimulation, config.Coupling, config.LiquidPhysics, config.Gravity,
                config.TempBodyLifetime, config.TempBodyMerging, config.ChunkSize, config.ActiveRectangle, config.Parallelism, config.LiquidPressure,
                config.SameTickMoveGuard, config.RowSweepOrder, config.CellRenderSmoothing, config.BallisticParticles, config.EjectionResistance,
                config.ParticleCollision, config.LandingSearch, config.EjectionImpulseTarget, config.EjectionDeflection,
                config.Reattachment, config.RasterizationBodyParallelism, config.RasterizationCellParallelism,
                config.BallisticResolutionParallelism, config.RenderParallelism)).ToList();

        StartAlternatingByConfig(pass, coreMethods.Count);
    }

    // Reverses the order that configs run in on every other repeat run: 1st repeat forward, 2nd reversed, 3rd forward, etc.
    private void StartAlternatingByConfig(List<ScenarioEntry> pass, int scenariosPerConfig)
    {
        var queue = new List<ScenarioEntry>(pass.Count * _repeatCount);
        for (int repeat = _repeatIndexStart; repeat < _repeatIndexStart + _repeatCount; repeat++)
        {
            IEnumerable<ScenarioEntry> ordered = repeat % 2 == 0 ? pass : ReversedByConfig(pass, scenariosPerConfig);
            queue.AddRange(ordered.Select(e => e with { RepeatIndex = repeat }));
        }

        StartQueue(queue);
    }

    private WorldConfig BuildWorldConfig(WorldSizeOption option)
    {
        var (width, height) = option.Dimensions();
        return new WorldConfig
        {
            Width = width,
            Height = height,
            CellsPerMeter = _worldConfigTemplate.CellsPerMeter,
            DebugCaStepsPerSecond = _worldConfigTemplate.DebugCaStepsPerSecond
        };
    }

    private IEnumerable<ScenarioEntry> BuildEntriesFromMethods(IEnumerable<MethodInfo> methods)
    {
        return
            from m in methods
            let attr = m.GetCustomAttribute<ScenarioAttribute>()!
            let cameraPan = m.GetCustomAttribute<CameraPanAttribute>()
            from worldSize in _worldSizeOptions
            from ca in _caAlgorithms
            from rb in _rbSimulations
            from coupling in _couplingModes
            from liquidPhysics in _liquidPhysicsModes
            from gravity in _gravityModes
            from tempBodyLifetime in _tempBodyLifetimes
            from tempBodyMerging in _tempBodyMergings
            from chunkSize in _chunkSizes
            from activeRectangleMode in _activeRectangleModes
            from parallelismMode in _parallelismModes
            from liquidPressureMode in _liquidPressureModes
            from sameTickMoveGuard in _sameTickMoveGuards
            from rowSweepOrder in _rowSweepOrders
            from cellRenderSmoothing in _cellRenderSmoothings
            from ballisticParticleMode in _ballisticParticleModes
            from ejectionResistanceMode in _ejectionResistanceModes
            from particleCollisionMode in _particleCollisionModes
            from landingSearchMode in _landingSearchModes
            from ejectionImpulseTargetMode in _ejectionImpulseTargetModes
            from ejectionDeflectionMode in _ejectionDeflectionModes
            from reattachmentMode in _reattachmentModes
            from rasterizationBodyParallelism in _rasterizationBodyParallelisms
            from rasterizationCellParallelism in _rasterizationCellParallelisms
            from ballisticResolutionParallelism in _ballisticResolutionParallelisms
            from renderParallelism in _renderParallelisms
            where IsValidCombination(coupling, liquidPhysics) && IsValidTempBodyLifetime(coupling, tempBodyLifetime)
                && IsValidLiquidTempBodyLifetime(liquidPhysics, tempBodyLifetime)
                && IsValidTempBodyMerging(coupling, tempBodyMerging)
                && IsValidParallelism(parallelismMode, chunkSize)
                && IsValidBallisticCombination(ballisticParticleMode, ejectionResistanceMode,
                    particleCollisionMode, landingSearchMode, ejectionImpulseTargetMode, ejectionDeflectionMode,
                    reattachmentMode)
                && IsValidBallisticResolutionParallelism(ballisticParticleMode, ballisticResolutionParallelism)
            select new ScenarioEntry(
                m.Name,
                ctx => m.Invoke(null, [ctx]),
                cameraPan,
                attr.FrameBudget,
                BuildWorldConfig(worldSize),
                ca, rb, coupling, liquidPhysics, gravity, tempBodyLifetime, tempBodyMerging, chunkSize, activeRectangleMode, parallelismMode, liquidPressureMode,
                sameTickMoveGuard, rowSweepOrder, cellRenderSmoothing, ballisticParticleMode, ejectionResistanceMode,
                particleCollisionMode, landingSearchMode, ejectionImpulseTargetMode, ejectionDeflectionMode, reattachmentMode,
                rasterizationBodyParallelism, rasterizationCellParallelism, ballisticResolutionParallelism,
                renderParallelism);
    }

    private void Start(IEnumerable<ScenarioEntry> entries)
    {
        var pass = entries.ToList();
        StartQueue(
            Enumerable.Range(_repeatIndexStart, _repeatCount)
                .SelectMany(repeat => pass.Select(e => e with { RepeatIndex = repeat }))
                .ToList());
    }

    private void StartQueue(List<ScenarioEntry> queue)
    {
        _queue = queue;
        _currentIndex = 0;
        _currentFrame = 0;
        if (_queue.Count > 0)
        {
            SetupScenario(0);
        }
    }

    private void SetupScenario(int index)
    {
        var entry = _queue[index];
        CurrentCameraPan = entry.CameraPan;

        ICaAlgorithm algorithm = entry.CaAlgorithm switch
        {
            CaAlgorithmType.Margolus => new MargolusAlgorithm(),
            CaAlgorithmType.MargolusGuarded => MargolusAlgorithm.Guarded(),
            CaAlgorithmType.MargolusRandomized => MargolusAlgorithm.Randomized(),
            CaAlgorithmType.MargolusGuardedRandomized => MargolusAlgorithm.GuardedRandomized(),
            CaAlgorithmType.PerCell => new PerCellAlgorithm(seed: 42, entry.RowSweepOrder),
            CaAlgorithmType.PerCellRandomized => new PerCellRandomizedAlgorithm(seed: 42, entry.RowSweepOrder),
            CaAlgorithmType.PerCellDirectional => new PerCellDirectionalAlgorithm(
                entry.WorldConfig.Width, entry.WorldConfig.Height, seed: 42, entry.RowSweepOrder),
            _ => throw new ArgumentOutOfRangeException()
        };

        float gravityY = entry.Gravity switch
        {
            GravityMode.Normal => -10f,
            GravityMode.High => -80f,
            _ => throw new ArgumentOutOfRangeException()
        };

        float initialFallVelocity = entry.Gravity switch
        {
            GravityMode.Normal => 0f,
            GravityMode.High => -8f,
            _ => throw new ArgumentOutOfRangeException()
        };

        IRigidBodySimulation physics = entry.RbSimulation switch
        {
            RigidBodySimulationType.Box2D => new Box2DSimulation(gravityY: gravityY),
            RigidBodySimulationType.Chipmunk => new ChipmunkSimulation(gravityY: gravityY),
            _ => throw new ArgumentOutOfRangeException()
        };

        ICouplingStrategy coupling = entry.Coupling switch
        {
            CouplingMode.None => new NoCouplingStrategy(),
            CouplingMode.WeakOneWayRbToCa => new WeakOneWayRbToCaStrategy(
                entry.WorldConfig.CellsPerMeter, entry.WorldConfig.Height,
                entry.RasterizationBodyParallelism, entry.RasterizationCellParallelism),
            CouplingMode.WeakOneWayCaToRb => new WeakOneWayCaToRbStrategy(
                entry.WorldConfig.CellsPerMeter, entry.WorldConfig.Height, entry.LiquidPhysics, MathF.Abs(gravityY), entry.TempBodyLifetime,
                entry.RasterizationBodyParallelism, entry.RasterizationCellParallelism, entry.TempBodyMerging),
            CouplingMode.WeakTwoWayCaFirst => new WeakTwoWayCaFirstStrategy(
                entry.WorldConfig.CellsPerMeter, entry.WorldConfig.Height, entry.LiquidPhysics, MathF.Abs(gravityY), entry.TempBodyLifetime,
                entry.RasterizationBodyParallelism, entry.RasterizationCellParallelism, entry.TempBodyMerging),
            CouplingMode.WeakTwoWayRbFirst => new WeakTwoWayRbFirstStrategy(
                entry.WorldConfig.CellsPerMeter, entry.WorldConfig.Height, entry.LiquidPhysics, MathF.Abs(gravityY), entry.TempBodyLifetime,
                entry.RasterizationBodyParallelism, entry.RasterizationCellParallelism, entry.TempBodyMerging),
            _ => throw new ArgumentOutOfRangeException()
        };

        var ballistics = new BallisticParticleSystem(
            entry.WorldConfig.CellsPerMeter, entry.WorldConfig.Height, gravityY,
            entry.BallisticParticleMode, entry.EjectionResistanceMode,
            entry.ParticleCollisionMode, entry.LandingSearchMode,
            entry.EjectionImpulseTargetMode, entry.EjectionDeflectionMode, entry.ReattachmentMode,
            entry.RasterizationCellParallelism, entry.BallisticResolutionParallelism);

        Sim = new SimulationWorld(
            entry.WorldConfig, algorithm, physics, coupling,
            entry.ChunkSize, entry.ActiveRectangleMode, entry.ParallelismMode, entry.LiquidPressureMode,
            entry.SameTickMoveGuard, ballistics);
        var context = new ScenarioContext(Sim.Grid, Sim.Physics, ScenarioHelpers.BuildGeometry(entry.WorldConfig))
        {
            InitialFallVelocity = initialFallVelocity
        };
        ScenarioHelpers.AddFloor(context);
        entry.Run(context);
    }

    private bool AdvanceQueue()
    {
        _currentIndex++;
        _currentFrame = 0;
        if (!AllScenariosCompleted)
        {
            SetupScenario(_currentIndex);
        }

        return true;
    }

    private record ScenarioEntry(
        string Name,
        Action<ScenarioContext> Run,
        CameraPanAttribute? CameraPan,
        int? FrameBudget,
        WorldConfig WorldConfig,
        CaAlgorithmType CaAlgorithm,
        RigidBodySimulationType RbSimulation,
        CouplingMode Coupling,
        LiquidPhysicsMode LiquidPhysics,
        GravityMode Gravity,
        TempBodyLifetime TempBodyLifetime,
        TempBodyMerging TempBodyMerging,
        ChunkSize ChunkSize,
        ActiveRectangle ActiveRectangleMode,
        ParallelismMode ParallelismMode,
        LiquidPressureMode LiquidPressureMode,
        SameTickMoveGuard SameTickMoveGuard,
        RowSweepOrder RowSweepOrder,
        CellRenderSmoothing CellRenderSmoothing,
        BallisticParticleMode BallisticParticleMode,
        EjectionResistanceMode EjectionResistanceMode,
        ParticleCollisionMode ParticleCollisionMode,
        LandingSearchMode LandingSearchMode,
        EjectionImpulseTargetMode EjectionImpulseTargetMode,
        EjectionDeflectionMode EjectionDeflectionMode,
        ReattachmentMode ReattachmentMode,
        RasterizationBodyParallelism RasterizationBodyParallelism,
        RasterizationCellParallelism RasterizationCellParallelism,
        BallisticResolutionParallelism BallisticResolutionParallelism,
        RenderParallelism RenderParallelism,
        int RepeatIndex = 0);
}
