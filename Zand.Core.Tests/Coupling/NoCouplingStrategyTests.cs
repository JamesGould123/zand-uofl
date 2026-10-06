namespace Zand.Core.Tests.Coupling;

using Xunit;
using Zand.Core.CellularAutomata;
using Zand.Core.CellularAutomata.Algorithms;
using Zand.Core.Coupling;
using Zand.Core.Tests.Mocks;

public class NoCouplingStrategyTests
{
    [Fact]
    public void AllFourHooks_AreNoOps()
    {
        var grid = new CaGrid(20, 20, new PerCellAlgorithm(1));
        grid.SetCell(10, 15, new Cell { Type = CellType.Sand });
        var physics = new FakeRigidBodySimulation();
        physics.AddDynamicBox(10.5f, 4.5f, 0.4f, 0.4f);
        var strategy = new NoCouplingStrategy();

        strategy.BeforeCaUpdate(grid, physics);
        strategy.AfterCaUpdate(grid, physics);
        strategy.BeforeRbUpdate(grid, physics);
        strategy.AfterRbUpdate(grid, physics);

        Assert.Equal(CellType.Sand, grid.GetCell(10, 15).Type);
        Assert.Single(physics.GetBodies());
        Assert.Empty(physics.AppliedForces);
        Assert.Empty(physics.AppliedImpulses);
        Assert.Empty(physics.SetLinearVelocityCalls);
        Assert.Empty(physics.SetAngularVelocityCalls);
        Assert.Empty(physics.RemovedBodies);
    }
}
