namespace Zand.Core.Tests.Coupling;

using Xunit;
using Zand.Core.CellularAutomata;
using Zand.Core.CellularAutomata.Algorithms;
using Zand.Core.Coupling;
using Zand.Core.RigidBody;
using Zand.Core.Tests.Mocks;

public class WeakOneWayRbToCaStrategyTests
{
    private const int WorldSize = 20;

    [Fact]
    public void PhysicsFirst_IsFalse()
    {
        var strategy = new WeakOneWayRbToCaStrategy(1f, WorldSize);

        Assert.False(strategy.PhysicsFirst);
    }

    [Fact]
    public void AllFourHooks_OnlyStampAndClear_NeverTouchPhysics()
    {
        var grid = CreateGrid();
        grid.SetCell(10, 15, new Cell { Type = CellType.Water });
        var physics = new FakeRigidBodySimulation();

        // Wide enough to cover both cell (9,15) [empty] and cell (10,15) [Water].
        physics.AddDynamicBox(10.0f, 4.5f, 0.6f, 0.4f);
        var strategy = new WeakOneWayRbToCaStrategy(1f, WorldSize);

        strategy.BeforeCaUpdate(grid, physics);
        Assert.Equal(CellType.Static, grid.GetCell(9, 15).Type);

        strategy.AfterCaUpdate(grid, physics);
        Assert.Equal(CellType.Empty, grid.GetCell(9, 15).Type);

        strategy.BeforeRbUpdate(grid, physics);
        strategy.AfterRbUpdate(grid, physics);

        // Never scans for sand/liquid bodies or applies liquid physics, regardless of the
        // Water cell sitting in the body's footprint.
        Assert.Single(physics.GetBodies());
        Assert.Empty(physics.AppliedForces);
        Assert.Empty(physics.SetLinearVelocityCalls);
        Assert.Empty(physics.RemovedBodies);
    }

    private static CaGrid CreateGrid(int width = WorldSize, int height = WorldSize) =>
        new(width, height, new PerCellAlgorithm(1));
}
