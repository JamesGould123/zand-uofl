namespace Zand.Core.Tests.Coupling;

using Xunit;
using Zand.Core.CellularAutomata;
using Zand.Core.CellularAutomata.Algorithms;
using Zand.Core.Coupling;
using Zand.Core.RigidBody;
using Zand.Core.Tests.Mocks;

public class WeakOneWayCaToRbStrategyTests
{
    private const int WorldSize = 20;

    [Fact]
    public void PhysicsFirst_IsFalse()
    {
        var strategy = new WeakOneWayCaToRbStrategy(1f, WorldSize);

        Assert.False(strategy.PhysicsFirst);
    }

    [Fact]
    public void BeforeCaUpdate_NeverStampsBodyFootprint()
    {
        var grid = CreateGrid();
        var physics = new FakeRigidBodySimulation();
        physics.AddDynamicBox(10.5f, 9.5f, 0.4f, 0.4f);
        var strategy = new WeakOneWayCaToRbStrategy(1f, WorldSize);

        strategy.BeforeCaUpdate(grid, physics);

        Assert.Equal(CellType.Empty, grid.GetCell(10, 10).Type);
    }

    [Fact]
    public void AfterCaUpdateThenAfterRbUpdate_ScansAndRemovesTempBodyForSandCell()
    {
        var grid = CreateGrid();
        grid.SetCell(10, 15, new Cell { Type = CellType.Sand });
        var physics = new FakeRigidBodySimulation();
        physics.AddDynamicBox(10.5f, 4.5f, 0.4f, 0.4f);
        var strategy = new WeakOneWayCaToRbStrategy(1f, WorldSize);

        strategy.AfterCaUpdate(grid, physics);
        Assert.Single(physics.GetBodies(), b => b.IsStatic);

        strategy.BeforeRbUpdate(grid, physics);
        strategy.AfterRbUpdate(grid, physics);

        Assert.DoesNotContain(physics.GetBodies(), b => b.IsStatic);
    }

    private static CaGrid CreateGrid(int width = WorldSize, int height = WorldSize) =>
        new(width, height, new PerCellAlgorithm(1));
}
