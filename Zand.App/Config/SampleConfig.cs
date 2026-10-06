namespace Zand.App.Config;

using Zand.Core.Ballistics;
using Zand.Core.CellularAutomata;
using Zand.Core.CellularAutomata.Algorithms;
using Zand.Core.CellularAutomata.Chunking;
using Zand.Core.Coupling;
using Zand.Rendering;

public sealed record SampleConfig(
    WorldSizeOption WorldSize,
    CaAlgorithmType CaAlgorithm,
    RigidBodySimulationType RbSimulation,
    CouplingMode Coupling,
    LiquidPhysicsMode LiquidPhysics,
    GravityMode Gravity,
    TempBodyLifetime TempBodyLifetime,
    TempBodyMerging TempBodyMerging,
    ChunkSize ChunkSize,
    ActiveRectangle ActiveRectangle,
    ParallelismMode Parallelism,
    LiquidPressureMode LiquidPressure,
    SameTickMoveGuard SameTickMoveGuard,
    RowSweepOrder RowSweepOrder,
    CellRenderSmoothing CellRenderSmoothing,
    BallisticParticleMode BallisticParticles,
    EjectionResistanceMode EjectionResistance,
    ParticleCollisionMode ParticleCollision,
    LandingSearchMode LandingSearch,
    EjectionImpulseTargetMode EjectionImpulseTarget,
    EjectionDeflectionMode EjectionDeflection,
    ReattachmentMode Reattachment,
    RasterizationBodyParallelism RasterizationBodyParallelism,
    RasterizationCellParallelism RasterizationCellParallelism,
    BallisticResolutionParallelism BallisticResolutionParallelism,
    RenderParallelism RenderParallelism);
