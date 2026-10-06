namespace Zand.Core.Coupling;

using Zand.Core.CellularAutomata;
using Zand.Core.RigidBody;

public class NoCouplingStrategy : ICouplingStrategy
{
    public void BeforeCaUpdate(CaGrid grid, IRigidBodySimulation physics)
    {
    }

    public void AfterCaUpdate(CaGrid grid, IRigidBodySimulation physics)
    {
    }

    public void BeforeRbUpdate(CaGrid grid, IRigidBodySimulation physics)
    {
    }

    public void AfterRbUpdate(CaGrid grid, IRigidBodySimulation physics)
    {
    }
}
