namespace Zand.Core.Tests.Coupling;

using Xunit;
using Zand.Core.CellularAutomata;
using Zand.Core.CellularAutomata.Algorithms;
using Zand.Core.Coupling;
using Zand.Core.RigidBody;
using Zand.Core.Tests.Mocks;

public class WeakTwoWayRbFirstStrategyTests
{
    private const int WorldSize = 20;

    [Fact]
    public void PhysicsFirst_IsTrue()
    {
        var strategy = new WeakTwoWayRbFirstStrategy(1f, WorldSize);

        Assert.True(strategy.PhysicsFirst);
    }

    [Fact]
    public void FullTick_TempBodyOnlyAppearsAfterBeforeRbUpdate_UnlikeCaFirst()
    {
        var grid = CreateGrid();
        grid.SetCell(10, 15, new Cell { Type = CellType.Sand });
        var physics = new FakeRigidBodySimulation();

        // Footprint (margin 0) covers empty cell (10,12) for stamping; the wider sand-scan margin (3 cells)
        // reaches cell (10,15) too, so one body exercises both behaviors.
        physics.AddDynamicBox(10.5f, 7.5f, 0.4f, 0.4f);
        var strategy = new WeakTwoWayRbFirstStrategy(1f, WorldSize);

        strategy.BeforeCaUpdate(grid, physics);
        Assert.Equal(CellType.Static, grid.GetCell(10, 12).Type);

        // Unlike WeakTwoWayCaFirstStrategy, AfterCaUpdate here only calls ClearStamps -
        // temp bodies for sand cells aren't added until BeforeRbUpdate.
        strategy.AfterCaUpdate(grid, physics);
        Assert.Equal(CellType.Empty, grid.GetCell(10, 12).Type);
        Assert.DoesNotContain(physics.GetBodies(), b => b.IsStatic);

        strategy.BeforeRbUpdate(grid, physics);
        Assert.Single(physics.GetBodies(), b => b.IsStatic);

        strategy.AfterRbUpdate(grid, physics);
        Assert.DoesNotContain(physics.GetBodies(), b => b.IsStatic);
    }

    private static CaGrid CreateGrid(int width = WorldSize, int height = WorldSize) =>
        new(width, height, new PerCellAlgorithm(1));
}
