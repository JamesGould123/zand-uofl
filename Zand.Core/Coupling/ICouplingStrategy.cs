namespace Zand.Core.Coupling;

using Zand.Core.CellularAutomata;
using Zand.Core.RigidBody;

public interface ICouplingStrategy
{
    bool PhysicsFirst => false;

    void BeforeCaUpdate(CaGrid grid, IRigidBodySimulation physics);

    void AfterCaUpdate(CaGrid grid, IRigidBodySimulation physics);

    void BeforeRbUpdate(CaGrid grid, IRigidBodySimulation physics);

    void AfterRbUpdate(CaGrid grid, IRigidBodySimulation physics);
}
