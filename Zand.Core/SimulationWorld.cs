namespace Zand.Core;

using Zand.Core.Ballistics;
using Zand.Core.CellularAutomata;
using Zand.Core.CellularAutomata.Algorithms;
using Zand.Core.CellularAutomata.Chunking;
using Zand.Core.Coupling;
using Zand.Core.Metrics;
using Zand.Core.RigidBody;

public class SimulationWorld
{
    private readonly WorldConfig _config;
    private readonly ICouplingStrategy _coupling;
    private float _debugCaAccumulator;

    public SimulationWorld(WorldConfig config, ICaAlgorithm caAlgorithm,
        IRigidBodySimulation physics, ICouplingStrategy? coupling = null,
        ChunkSize chunkSize = ChunkSize.Disabled,
        ActiveRectangle activeRectangleMode = ActiveRectangle.Disabled,
        ParallelismMode parallelismMode = ParallelismMode.Sequential,
        LiquidPressureMode liquidPressureMode = LiquidPressureMode.Disabled,
        SameTickMoveGuard sameTickMoveGuard = SameTickMoveGuard.Disabled,
        BallisticParticleSystem? ballistics = null)
    {
        _config = config;
        Grid = new CaGrid(
            config.Width, config.Height, caAlgorithm, chunkSize, activeRectangleMode, parallelismMode,
            liquidPressureMode, sameTickMoveGuard);
        Physics = physics;
        _coupling = coupling ?? new NoCouplingStrategy();
        Ballistics = ballistics ?? new BallisticParticleSystem(
            config.CellsPerMeter, config.Height, gravityY: 0f, BallisticParticleMode.None);
    }

    public CaGrid Grid { get; }

    public IRigidBodySimulation Physics { get; }

    public BallisticParticleSystem Ballistics { get; }

    public void Step(float deltaTime)
    {
        bool stepCa = ShouldStepCa(deltaTime);

        if (_coupling.PhysicsFirst)
        {
            PhaseTimer.Begin(SimPhase.CouplingBeforeRb);
            _coupling.BeforeRbUpdate(Grid, Physics);
            PhaseTimer.End(SimPhase.CouplingBeforeRb);

            PhaseTimer.Begin(SimPhase.PhysicsStep);
            Physics.Step(deltaTime);
            PhaseTimer.End(SimPhase.PhysicsStep);

            PhaseTimer.Begin(SimPhase.CouplingAfterRb);
            _coupling.AfterRbUpdate(Grid, Physics);
            PhaseTimer.End(SimPhase.CouplingAfterRb);

            if (stepCa)
            {
                PhaseTimer.Begin(SimPhase.CouplingBeforeCa);
                _coupling.BeforeCaUpdate(Grid, Physics);
                PhaseTimer.End(SimPhase.CouplingBeforeCa);

                PhaseTimer.Begin(SimPhase.GridUpdate);
                Grid.Update();
                PhaseTimer.End(SimPhase.GridUpdate);

                PhaseTimer.Begin(SimPhase.CouplingAfterCa);
                _coupling.AfterCaUpdate(Grid, Physics);
                PhaseTimer.End(SimPhase.CouplingAfterCa);
            }
        }
        else
        {
            if (stepCa)
            {
                PhaseTimer.Begin(SimPhase.CouplingBeforeCa);
                _coupling.BeforeCaUpdate(Grid, Physics);
                PhaseTimer.End(SimPhase.CouplingBeforeCa);

                PhaseTimer.Begin(SimPhase.GridUpdate);
                Grid.Update();
                PhaseTimer.End(SimPhase.GridUpdate);

                PhaseTimer.Begin(SimPhase.CouplingAfterCa);
                _coupling.AfterCaUpdate(Grid, Physics);
                PhaseTimer.End(SimPhase.CouplingAfterCa);
            }

            PhaseTimer.Begin(SimPhase.CouplingBeforeRb);
            _coupling.BeforeRbUpdate(Grid, Physics);
            PhaseTimer.End(SimPhase.CouplingBeforeRb);

            PhaseTimer.Begin(SimPhase.PhysicsStep);
            Physics.Step(deltaTime);
            PhaseTimer.End(SimPhase.PhysicsStep);

            PhaseTimer.Begin(SimPhase.CouplingAfterRb);
            _coupling.AfterRbUpdate(Grid, Physics);
            PhaseTimer.End(SimPhase.CouplingAfterRb);
        }

        PhaseTimer.Begin(SimPhase.BallisticParticles);
        Ballistics.Advance(Grid, Physics, deltaTime);
        PhaseTimer.End(SimPhase.BallisticParticles);
    }

    private bool ShouldStepCa(float deltaTime)
    {
        if (!_config.DebugCaStepsPerSecond.HasValue)
        {
            return true;
        }

        _debugCaAccumulator += deltaTime;
        float interval = 1f / _config.DebugCaStepsPerSecond.Value;
        if (_debugCaAccumulator < interval)
        {
            return false;
        }

        _debugCaAccumulator -= interval;
        return true;
    }
}
