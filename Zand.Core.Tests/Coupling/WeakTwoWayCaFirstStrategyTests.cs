namespace Zand.Core.Tests.Coupling;

using Xunit;
using Zand.Core.CellularAutomata;
using Zand.Core.CellularAutomata.Algorithms;
using Zand.Core.Coupling;
using Zand.Core.RigidBody;
using Zand.Core.Tests.Mocks;

public class WeakTwoWayCaFirstStrategyTests
{
    private const int WorldSize = 20;

    [Fact]
    public void PhysicsFirst_IsFalse()
    {
        var strategy = new WeakTwoWayCaFirstStrategy(1f, WorldSize);

        Assert.False(strategy.PhysicsFirst);
    }

    [Fact]
    public void FullTick_StampsDuringCaPhaseAndAddsTempBodyRightAfterCaUpdate()
    {
        var grid = CreateGrid();
        grid.SetCell(10, 15, new Cell { Type = CellType.Sand });
        var physics = new FakeRigidBodySimulation();

        // Own footprint (margin 0) covers empty cell (10,12) for stamping; the wider sand-scan
        // margin (3 cells) reaches cell (10,15) too, so one body exercises both behaviors.
        physics.AddDynamicBox(10.5f, 7.5f, 0.4f, 0.4f);
        var strategy = new WeakTwoWayCaFirstStrategy(1f, WorldSize);

        strategy.BeforeCaUpdate(grid, physics);
        Assert.Equal(CellType.Static, grid.GetCell(10, 12).Type);

        // AfterCaUpdate both clears the stamp and adds the sand temp body, in that order.
        strategy.AfterCaUpdate(grid, physics);
        Assert.Equal(CellType.Empty, grid.GetCell(10, 12).Type);
        Assert.Single(physics.GetBodies(), b => b.IsStatic);

        strategy.BeforeRbUpdate(grid, physics);
        strategy.AfterRbUpdate(grid, physics);

        Assert.DoesNotContain(physics.GetBodies(), b => b.IsStatic);
    }

    private static CaGrid CreateGrid(int width = WorldSize, int height = WorldSize) =>
        new(width, height, new PerCellAlgorithm(1));
}
