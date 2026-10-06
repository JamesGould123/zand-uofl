namespace Zand.Core.Coupling;

using Zand.Core.CellularAutomata;
using Zand.Core.RigidBody;

public class WeakOneWayRbToCaStrategy : WeakCouplingStrategyBase
{
    public WeakOneWayRbToCaStrategy(float cellsPerMeter, int worldHeightCells,
        RasterizationBodyParallelism bodyParallelism = RasterizationBodyParallelism.Sequential,
        RasterizationCellParallelism cellParallelism = RasterizationCellParallelism.Sequential)
        : base(cellsPerMeter, worldHeightCells,
            bodyParallelism: bodyParallelism, cellParallelism: cellParallelism)
    {
    }

    public override bool PhysicsFirst => false;

    public override void BeforeCaUpdate(CaGrid grid, IRigidBodySimulation physics)
        => StampBodies(grid, physics);

    public override void AfterCaUpdate(CaGrid grid, IRigidBodySimulation physics)
        => ClearStamps(grid);

    public override void BeforeRbUpdate(CaGrid grid, IRigidBodySimulation physics)
    {
    }

    public override void AfterRbUpdate(CaGrid grid, IRigidBodySimulation physics)
    {
    }
}
