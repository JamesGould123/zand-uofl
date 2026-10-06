namespace Zand.App.Scenarios;

using Zand.Core.CellularAutomata;
using Zand.Core.RigidBody;

public class ScenarioContext
{
    public ScenarioContext(CaGrid grid, IRigidBodySimulation physics, ScenarioGeometry geometry)
    {
        Grid = grid;
        Physics = physics;
        Geometry = geometry;
    }

    public CaGrid Grid { get; }

    public IRigidBodySimulation Physics { get; }

    public ScenarioGeometry Geometry { get; }

    public float InitialFallVelocity { get; init; }
}
