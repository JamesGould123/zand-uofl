namespace Zand.Core.Metrics;

public enum SimPhase
{
    GridUpdate,
    PhysicsStep,
    CouplingBeforeCa,
    CouplingAfterCa,
    CouplingBeforeRb,
    CouplingAfterRb,
    BallisticParticles,
    Render
}
