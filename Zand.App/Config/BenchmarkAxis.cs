namespace Zand.App.Config;

// Used by "Core scenarios with reasonable configs" to identify the axis being benchmarked, runs
// the selected options on the given axis with each of the reasonable configs.
public enum BenchmarkAxis
{
    WorldSize,
    CaAlgorithm,
    RbSimulation,
    CouplingMode,
    LiquidPhysics,
    Gravity,
    TempBodyLifetime,
    TempBodyMerging,
    ChunkSize,
    ActiveRectangle,
    Parallelism,
    LiquidPressure,
    SameTickMoveGuard,
    RowSweepOrder,
    CellRenderSmoothing,
    BallisticParticles,
    EjectionResistance,
    ParticleCollision,
    LandingSearch,
    EjectionImpulseTarget,
    EjectionDeflection,
    Reattachment,
    RasterizationBodyParallelism,
    RasterizationCellParallelism,
    BallisticResolutionParallelism,
    RenderParallelism
}
