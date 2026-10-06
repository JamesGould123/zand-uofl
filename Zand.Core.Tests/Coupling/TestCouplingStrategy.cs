namespace Zand.Core.Tests.Coupling;

using Zand.Core.CellularAutomata;
using Zand.Core.Coupling;
using Zand.Core.RigidBody;

// A test helper exposing WeakCouplingStrategyBase's protected methods directly
internal sealed class TestCouplingStrategy : WeakCouplingStrategyBase
{
    public TestCouplingStrategy(
        float cellsPerMeter,
        int worldHeightCells,
        LiquidPhysicsMode liquidPhysicsMode = LiquidPhysicsMode.None,
        float gravityMagnitude = 10f,
        TempBodyLifetime tempBodyLifetime = TempBodyLifetime.Ephemeral,
        RasterizationBodyParallelism bodyParallelism = RasterizationBodyParallelism.Sequential,
        RasterizationCellParallelism cellParallelism = RasterizationCellParallelism.Sequential,
        TempBodyMerging tempBodyMerging = TempBodyMerging.NoMerging)
        : base(cellsPerMeter, worldHeightCells, liquidPhysicsMode, gravityMagnitude, tempBodyLifetime, bodyParallelism, cellParallelism, tempBodyMerging)
    {
    }

    public override bool PhysicsFirst => false;

    public override void BeforeCaUpdate(CaGrid grid, IRigidBodySimulation physics)
    {
    }

    public override void AfterCaUpdate(CaGrid grid, IRigidBodySimulation physics)
    {
    }

    public override void BeforeRbUpdate(CaGrid grid, IRigidBodySimulation physics)
    {
    }

    public override void AfterRbUpdate(CaGrid grid, IRigidBodySimulation physics)
    {
    }

    public void ExposedStampBodies(CaGrid grid, IRigidBodySimulation physics) => StampBodies(grid, physics);

    public void ExposedClearStamps(CaGrid grid) => ClearStamps(grid);

    public void ExposedDispatchScanAndSync(CaGrid grid, IRigidBodySimulation physics) => DispatchScanAndSync(grid, physics);

    public void ExposedDispatchRemove(IRigidBodySimulation physics) => DispatchRemove(physics);

    public void ExposedComputeLiquidInteraction(CaGrid grid, IRigidBodySimulation physics) => ComputeLiquidInteraction(grid, physics);

    public void ExposedApplyDamping(CaGrid grid, IRigidBodySimulation physics) => ApplyDamping(grid, physics);
}
