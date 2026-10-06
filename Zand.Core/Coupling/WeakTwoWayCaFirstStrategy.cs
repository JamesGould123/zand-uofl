namespace Zand.Core.Coupling;

using Zand.Core.CellularAutomata;
using Zand.Core.RigidBody;

public class WeakTwoWayCaFirstStrategy : WeakCouplingStrategyBase
{
    public WeakTwoWayCaFirstStrategy(float cellsPerMeter, int worldHeightCells,
        LiquidPhysicsMode liquidPhysicsMode = LiquidPhysicsMode.None,
        float gravityMagnitude = 10f,
        TempBodyLifetime tempBodyLifetime = TempBodyLifetime.Ephemeral,
        RasterizationBodyParallelism bodyParallelism = RasterizationBodyParallelism.Sequential,
        RasterizationCellParallelism cellParallelism = RasterizationCellParallelism.Sequential,
        TempBodyMerging tempBodyMerging = TempBodyMerging.NoMerging)
        : base(cellsPerMeter, worldHeightCells, liquidPhysicsMode, gravityMagnitude, tempBodyLifetime,
            bodyParallelism, cellParallelism, tempBodyMerging)
    {
    }

    public override bool PhysicsFirst => false;

    public override void BeforeCaUpdate(CaGrid grid, IRigidBodySimulation physics)
        => StampBodies(grid, physics);

    public override void AfterCaUpdate(CaGrid grid, IRigidBodySimulation physics)
    {
        DispatchScanAndSync(grid, physics);
        ClearStamps(grid);
    }

    public override void BeforeRbUpdate(CaGrid grid, IRigidBodySimulation physics)
        => ComputeLiquidInteraction(grid, physics);

    public override void AfterRbUpdate(CaGrid grid, IRigidBodySimulation physics)
    {
        DispatchRemove(physics);
        ApplyDamping(grid, physics);
    }
}
