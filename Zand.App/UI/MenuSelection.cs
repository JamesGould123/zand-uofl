namespace Zand.App.UI;

using System;
using System.Linq;
using Zand.App.Config;
using Zand.Core.Ballistics;
using Zand.Core.CellularAutomata;
using Zand.Core.CellularAutomata.Algorithms;
using Zand.Core.CellularAutomata.Chunking;
using Zand.Core.Coupling;
using Zand.Rendering;

// The four options for how to run the simulation are:
// - ByElements (runs a single scenario with a single set of configured options)
// - ByPreconfiguredScenarios (runs various scenarios with different options)
// - CoreScenariosReasonable (runs core scenarios, with one configurable axis per run)
// - HeavyBenchmark (runs the single HeavyScenario, allowing configuration of chunk size, active rectangle,
//   parallelism, rasterization body/cell parallelism, ballistic resolution parallelism, and render parallelism, and running
//   selected options against the reasonable configs)
public enum ScenarioConfigMode
{
    ByPreconfiguredScenarios,
    ByElements,
    CoreScenariosReasonable,
    HeavyBenchmark
}

public record MenuSelection(
    WorldSizeOption[] WorldSizeOptions,
    CaAlgorithmType[] CaAlgorithms,
    RigidBodySimulationType[] RbSimulations,
    CouplingMode[] CouplingModes,
    LiquidPhysicsMode[] LiquidPhysicsModes,
    GravityMode[] GravityModes,
    TempBodyLifetime[] TempBodyLifetimes,
    ChunkSize[] ChunkSizes,
    ActiveRectangle[] ActiveRectangleModes,
    ParallelismMode[] ParallelismModes,
    LiquidPressureMode[] LiquidPressureModes,
    SameTickMoveGuard[] SameTickMoveGuards,
    RowSweepOrder[] RowSweepOrders,
    CellRenderSmoothing[] CellRenderSmoothings,
    BallisticParticleMode[] BallisticParticleModes,
    EjectionResistanceMode[] EjectionResistanceModes,
    ParticleCollisionMode[] ParticleCollisionModes,
    LandingSearchMode[] LandingSearchModes,
    EjectionImpulseTargetMode[] EjectionImpulseTargetModes,
    EjectionDeflectionMode[] EjectionDeflectionModes,
    ReattachmentMode[] ReattachmentModes,
    RasterizationBodyParallelism[] RasterizationBodyParallelisms,
    RasterizationCellParallelism[] RasterizationCellParallelisms,
    BallisticResolutionParallelism[] BallisticResolutionParallelisms,
    RenderParallelism[] RenderParallelisms,
    ScenarioConfigMode Mode,
    string[] ScenarioNames,
    string[] ElementNames,
    bool ShowActiveChunkDebug,
    bool ShowActiveRectangleDebug,
    int RepeatCount = 1,
    BenchmarkAxis ReasonableTestAxis = BenchmarkAxis.CaAlgorithm,
    string FilePrefix = "",
    BenchmarkAxis[]? PinnedAxes = null,
    int RepeatIndexStart = 0,
    BenchmarkAxis[]? RestrictedAxes = null,
    TempBodyMerging[]? TempBodyMergings = null,
    bool RealTimePlayback = false,
    bool UseHeavyScenario = false,
    bool UseBodyStressScenario = false)
{
    // Added late, this helps with compatibility with earlier configs
    public TempBodyMerging[] TempBodyMergingChoices => TempBodyMergings ?? [TempBodyMerging.NoMerging];

    // For each axis in RestrictedAxes,
    // only the reasonable configs whose value for that axis is among the selected values are run,
    // so an axis can be tested on just the configs where it has an effect.
    public bool IncludesReasonableConfig(SampleConfig config)
    {
        foreach (var axis in RestrictedAxes ?? [])
        {
            bool included = axis switch
            {
                BenchmarkAxis.WorldSize => WorldSizeOptions.Contains(config.WorldSize),
                BenchmarkAxis.CaAlgorithm => CaAlgorithms.Contains(config.CaAlgorithm),
                BenchmarkAxis.RbSimulation => RbSimulations.Contains(config.RbSimulation),
                BenchmarkAxis.CouplingMode => CouplingModes.Contains(config.Coupling),
                BenchmarkAxis.LiquidPhysics => LiquidPhysicsModes.Contains(config.LiquidPhysics),
                BenchmarkAxis.Gravity => GravityModes.Contains(config.Gravity),
                BenchmarkAxis.TempBodyLifetime => TempBodyLifetimes.Contains(config.TempBodyLifetime),
                BenchmarkAxis.TempBodyMerging => TempBodyMergingChoices.Contains(config.TempBodyMerging),
                BenchmarkAxis.ChunkSize => ChunkSizes.Contains(config.ChunkSize),
                BenchmarkAxis.ActiveRectangle => ActiveRectangleModes.Contains(config.ActiveRectangle),
                BenchmarkAxis.Parallelism => ParallelismModes.Contains(config.Parallelism),
                BenchmarkAxis.LiquidPressure => LiquidPressureModes.Contains(config.LiquidPressure),
                BenchmarkAxis.SameTickMoveGuard => SameTickMoveGuards.Contains(config.SameTickMoveGuard),
                BenchmarkAxis.RowSweepOrder => RowSweepOrders.Contains(config.RowSweepOrder),
                BenchmarkAxis.CellRenderSmoothing => CellRenderSmoothings.Contains(config.CellRenderSmoothing),
                BenchmarkAxis.BallisticParticles => BallisticParticleModes.Contains(config.BallisticParticles),
                BenchmarkAxis.EjectionResistance => EjectionResistanceModes.Contains(config.EjectionResistance),
                BenchmarkAxis.ParticleCollision => ParticleCollisionModes.Contains(config.ParticleCollision),
                BenchmarkAxis.LandingSearch => LandingSearchModes.Contains(config.LandingSearch),
                BenchmarkAxis.EjectionImpulseTarget => EjectionImpulseTargetModes.Contains(config.EjectionImpulseTarget),
                BenchmarkAxis.EjectionDeflection => EjectionDeflectionModes.Contains(config.EjectionDeflection),
                BenchmarkAxis.Reattachment => ReattachmentModes.Contains(config.Reattachment),
                BenchmarkAxis.RasterizationBodyParallelism => RasterizationBodyParallelisms.Contains(config.RasterizationBodyParallelism),
                BenchmarkAxis.RasterizationCellParallelism => RasterizationCellParallelisms.Contains(config.RasterizationCellParallelism),
                BenchmarkAxis.BallisticResolutionParallelism => BallisticResolutionParallelisms.Contains(config.BallisticResolutionParallelism),
                BenchmarkAxis.RenderParallelism => RenderParallelisms.Contains(config.RenderParallelism),
                _ => throw new ArgumentOutOfRangeException(nameof(RestrictedAxes))
            };

            if (!included)
            {
                return false;
            }
        }

        return true;
    }

    // UseHeavyScenario (CoreScenariosReasonable mode only) runs the heavy benchmark scenario instead of the
    // core scenarios, so an axis can be tested at a much larger particle and body count.
    // UseBodyStressScenario does the same with the body stress scenario, for a much larger body count
    // without the heavy scenario's particle count. At most one of the two can be set.
    //
    // RepeatIndexStart numbers this run's repeats from that value instead of 0,
    // so several separate runs of one comparison can each do a single repeat and still line up by repeat number.
    //
    // Each axis listed in PinnedAxes takes its single selected value in place of the reasonable configs' value,
    // for axes whose valid values depend on the tested axis.
    public SampleConfig ApplyPinnedAxes(SampleConfig config)
    {
        foreach (var axis in PinnedAxes ?? [])
        {
            config = axis switch
            {
                BenchmarkAxis.WorldSize => config with { WorldSize = First(WorldSizeOptions, axis) },
                BenchmarkAxis.CaAlgorithm => config with { CaAlgorithm = First(CaAlgorithms, axis) },
                BenchmarkAxis.RbSimulation => config with { RbSimulation = First(RbSimulations, axis) },
                BenchmarkAxis.CouplingMode => config with { Coupling = First(CouplingModes, axis) },
                BenchmarkAxis.LiquidPhysics => config with { LiquidPhysics = First(LiquidPhysicsModes, axis) },
                BenchmarkAxis.Gravity => config with { Gravity = First(GravityModes, axis) },
                BenchmarkAxis.TempBodyLifetime => config with { TempBodyLifetime = First(TempBodyLifetimes, axis) },
                BenchmarkAxis.TempBodyMerging => config with { TempBodyMerging = First(TempBodyMergingChoices, axis) },
                BenchmarkAxis.ChunkSize => config with { ChunkSize = First(ChunkSizes, axis) },
                BenchmarkAxis.ActiveRectangle => config with { ActiveRectangle = First(ActiveRectangleModes, axis) },
                BenchmarkAxis.Parallelism => config with { Parallelism = First(ParallelismModes, axis) },
                BenchmarkAxis.LiquidPressure => config with { LiquidPressure = First(LiquidPressureModes, axis) },
                BenchmarkAxis.SameTickMoveGuard => config with { SameTickMoveGuard = First(SameTickMoveGuards, axis) },
                BenchmarkAxis.RowSweepOrder => config with { RowSweepOrder = First(RowSweepOrders, axis) },
                BenchmarkAxis.CellRenderSmoothing => config with { CellRenderSmoothing = First(CellRenderSmoothings, axis) },
                BenchmarkAxis.BallisticParticles => config with { BallisticParticles = First(BallisticParticleModes, axis) },
                BenchmarkAxis.EjectionResistance => config with { EjectionResistance = First(EjectionResistanceModes, axis) },
                BenchmarkAxis.ParticleCollision => config with { ParticleCollision = First(ParticleCollisionModes, axis) },
                BenchmarkAxis.LandingSearch => config with { LandingSearch = First(LandingSearchModes, axis) },
                BenchmarkAxis.EjectionImpulseTarget => config with { EjectionImpulseTarget = First(EjectionImpulseTargetModes, axis) },
                BenchmarkAxis.EjectionDeflection => config with { EjectionDeflection = First(EjectionDeflectionModes, axis) },
                BenchmarkAxis.Reattachment => config with { Reattachment = First(ReattachmentModes, axis) },
                BenchmarkAxis.RasterizationBodyParallelism => config with { RasterizationBodyParallelism = First(RasterizationBodyParallelisms, axis) },
                BenchmarkAxis.RasterizationCellParallelism => config with { RasterizationCellParallelism = First(RasterizationCellParallelisms, axis) },
                BenchmarkAxis.BallisticResolutionParallelism => config with { BallisticResolutionParallelism = First(BallisticResolutionParallelisms, axis) },
                BenchmarkAxis.RenderParallelism => config with { RenderParallelism = First(RenderParallelisms, axis) },
                _ => throw new ArgumentOutOfRangeException(nameof(PinnedAxes))
            };
        }

        return config;
    }

    private static T First<T>(T[] values, BenchmarkAxis axis) =>
        values.Length > 0
            ? values[0]
            : throw new InvalidOperationException($"PinnedAxes includes {axis}, but no value is selected for it.");
}
