namespace Zand.App.Scenarios;

using System;
using System.Collections.Generic;
using System.Linq;
using Zand.App.Config;
using Zand.App.UI;
using Zand.Core.Ballistics;
using Zand.Core.CellularAutomata;
using Zand.Core.CellularAutomata.Algorithms;
using Zand.Core.CellularAutomata.Chunking;
using Zand.Core.Coupling;
using Zand.Rendering;

// Time estimation for a run,
// used by MenuScreen's "Estimated run time" text
// and the CLI's --dry-run flag.
public static class BenchmarkEstimator
{
    public const double AssumedFramesPerSecond = 60.0;

    public static EstimateResult Estimate(MenuSelection selection) => selection.Mode switch
    {
        ScenarioConfigMode.CoreScenariosReasonable => EstimateReasonable(selection),
        ScenarioConfigMode.HeavyBenchmark => EstimateHeavy(selection),
        ScenarioConfigMode.ByPreconfiguredScenarios => EstimateByPreconfigured(selection),
        ScenarioConfigMode.ByElements => EstimateByElements(selection),
        _ => throw new ArgumentOutOfRangeException(nameof(selection))
    };

    private static EstimateResult EstimateReasonable(MenuSelection s)
    {
        int Count<T>(T[] values, Func<SampleConfig, T, SampleConfig> withAxis)
            where T : notnull =>
            ScenarioRunner.ValidReasonableCombinations(values, withAxis, s.ApplyPinnedAxes, s.IncludesReasonableConfig).Count();

        int combinations = s.ReasonableTestAxis switch
        {
            BenchmarkAxis.WorldSize => Count(s.WorldSizeOptions, (c, v) => c with { WorldSize = v }),
            BenchmarkAxis.CaAlgorithm => Count(s.CaAlgorithms, (c, v) => c with { CaAlgorithm = v }),
            BenchmarkAxis.RbSimulation => Count(s.RbSimulations, (c, v) => c with { RbSimulation = v }),
            BenchmarkAxis.CouplingMode => Count(s.CouplingModes, (c, v) => c with { Coupling = v }),
            BenchmarkAxis.LiquidPhysics => Count(s.LiquidPhysicsModes, (c, v) => c with { LiquidPhysics = v }),
            BenchmarkAxis.Gravity => Count(s.GravityModes, (c, v) => c with { Gravity = v }),
            BenchmarkAxis.TempBodyLifetime => Count(s.TempBodyLifetimes, (c, v) => c with { TempBodyLifetime = v }),
            BenchmarkAxis.TempBodyMerging => Count(s.TempBodyMergingChoices, (c, v) => c with { TempBodyMerging = v }),
            BenchmarkAxis.ChunkSize => Count(s.ChunkSizes, (c, v) => c with { ChunkSize = v }),
            BenchmarkAxis.ActiveRectangle => Count(s.ActiveRectangleModes, (c, v) => c with { ActiveRectangle = v }),
            BenchmarkAxis.Parallelism => Count(s.ParallelismModes, (c, v) => c with { Parallelism = v }),
            BenchmarkAxis.LiquidPressure => Count(s.LiquidPressureModes, (c, v) => c with { LiquidPressure = v }),
            BenchmarkAxis.SameTickMoveGuard => Count(s.SameTickMoveGuards, (c, v) => c with { SameTickMoveGuard = v }),
            BenchmarkAxis.RowSweepOrder => Count(s.RowSweepOrders, (c, v) => c with { RowSweepOrder = v }),
            BenchmarkAxis.CellRenderSmoothing => Count(s.CellRenderSmoothings, (c, v) => c with { CellRenderSmoothing = v }),
            BenchmarkAxis.BallisticParticles => Count(s.BallisticParticleModes, (c, v) => c with { BallisticParticles = v }),
            BenchmarkAxis.EjectionResistance => Count(s.EjectionResistanceModes, (c, v) => c with { EjectionResistance = v }),
            BenchmarkAxis.ParticleCollision => Count(s.ParticleCollisionModes, (c, v) => c with { ParticleCollision = v }),
            BenchmarkAxis.LandingSearch => Count(s.LandingSearchModes, (c, v) => c with { LandingSearch = v }),
            BenchmarkAxis.EjectionImpulseTarget => Count(s.EjectionImpulseTargetModes, (c, v) => c with { EjectionImpulseTarget = v }),
            BenchmarkAxis.EjectionDeflection => Count(s.EjectionDeflectionModes, (c, v) => c with { EjectionDeflection = v }),
            BenchmarkAxis.Reattachment => Count(s.ReattachmentModes, (c, v) => c with { Reattachment = v }),
            BenchmarkAxis.RasterizationBodyParallelism => Count(s.RasterizationBodyParallelisms, (c, v) => c with { RasterizationBodyParallelism = v }),
            BenchmarkAxis.RasterizationCellParallelism => Count(s.RasterizationCellParallelisms, (c, v) => c with { RasterizationCellParallelism = v }),
            BenchmarkAxis.BallisticResolutionParallelism => Count(s.BallisticResolutionParallelisms, (c, v) => c with { BallisticResolutionParallelism = v }),
            BenchmarkAxis.RenderParallelism => Count(s.RenderParallelisms, (c, v) => c with { RenderParallelism = v }),
            _ => 0
        };

        int framesPerCombination = s.UseHeavyScenario
            ? ScenarioRunner.HeavyScenarioFrameBudget
            : s.UseBodyStressScenario
                ? ScenarioRunner.BodyStressScenarioFrameBudget
                : ScenarioRunner.CoreScenarioFrameBudgetTotal;
        long totalFrames = (long)combinations * framesPerCombination * s.RepeatCount;
        return new EstimateResult(combinations, totalFrames, totalFrames / AssumedFramesPerSecond, Note: null);
    }

    private static EstimateResult EstimateHeavy(MenuSelection s)
    {
        int combinations = ScenarioRunner.ValidHeavyCombinations(
            s.WorldSizeOptions, s.ChunkSizes, s.ActiveRectangleModes, s.ParallelismModes,
            s.RasterizationBodyParallelisms, s.RasterizationCellParallelisms, s.BallisticResolutionParallelisms,
            s.RenderParallelisms).Count();
        long totalFrames = (long)combinations * ScenarioRunner.HeavyScenarioFrameBudget * s.RepeatCount;
        return new EstimateResult(combinations, totalFrames, totalFrames / AssumedFramesPerSecond, Note: null);
    }

    // ByPreconfiguredScenarios (RunSelected) runs a cross-product of all selected options for all axes
    // against every selected named scenario - each of which carries its
    // own FrameBudget from its [Scenario] attribute.
    private static EstimateResult EstimateByPreconfigured(MenuSelection s)
    {
        if (s.ScenarioNames.Length == 0)
        {
            return new EstimateResult(0, 0, 0, Note: null);
        }

        int crossProduct = CountFullCrossProduct(s);
        int combinations = crossProduct * s.ScenarioNames.Length;

        var budgets = ScenarioRunner.ScenarioFrameBudgets;
        int framesPerCombination = s.ScenarioNames.Sum(name => budgets.GetValueOrDefault(name, 0));
        long totalFrames = (long)crossProduct * framesPerCombination * s.RepeatCount;
        return new EstimateResult(combinations, totalFrames, totalFrames / AssumedFramesPerSecond, Note: null);
    }

    // ByElements (RunElements) runs until manually ended, a time estimate would not be meaningful.
    private static EstimateResult EstimateByElements(MenuSelection s)
    {
        int combinations = CountFullCrossProduct(s) * s.RepeatCount;
        return new EstimateResult(
            combinations,
            TotalFrames: null,
            EstimatedSeconds: null,
            Note: "ByElements scenarios have no frame budget (they run until manually ended) - only a combination count can be estimated, not a run time.");
    }

    private static int CountFullCrossProduct(MenuSelection s)
    {
        return (
            from ca in s.CaAlgorithms
            from rb in s.RbSimulations
            from coupling in s.CouplingModes
            from liquidPhysics in s.LiquidPhysicsModes
            from gravity in s.GravityModes
            from tempBodyLifetime in s.TempBodyLifetimes
            from tempBodyMerging in s.TempBodyMergingChoices
            from chunkSize in s.ChunkSizes
            from activeRectangleMode in s.ActiveRectangleModes
            from parallelismMode in s.ParallelismModes
            from liquidPressureMode in s.LiquidPressureModes
            from sameTickMoveGuard in s.SameTickMoveGuards
            from rowSweepOrder in s.RowSweepOrders
            from cellRenderSmoothing in s.CellRenderSmoothings
            from ballisticParticleMode in s.BallisticParticleModes
            from ejectionResistanceMode in s.EjectionResistanceModes
            from particleCollisionMode in s.ParticleCollisionModes
            from landingSearchMode in s.LandingSearchModes
            from ejectionImpulseTargetMode in s.EjectionImpulseTargetModes
            from ejectionDeflectionMode in s.EjectionDeflectionModes
            from reattachmentMode in s.ReattachmentModes
            from rasterizationBodyParallelism in s.RasterizationBodyParallelisms
            from rasterizationCellParallelism in s.RasterizationCellParallelisms
            from ballisticResolutionParallelism in s.BallisticResolutionParallelisms
            from renderParallelism in s.RenderParallelisms
            from worldSize in s.WorldSizeOptions
            where ScenarioRunner.IsValidCombination(coupling, liquidPhysics)
                && ScenarioRunner.IsValidTempBodyLifetime(coupling, tempBodyLifetime)
                && ScenarioRunner.IsValidLiquidTempBodyLifetime(liquidPhysics, tempBodyLifetime)
                && ScenarioRunner.IsValidTempBodyMerging(coupling, tempBodyMerging)
                && ScenarioRunner.IsValidParallelism(parallelismMode, chunkSize)
                && ScenarioRunner.IsValidBallisticCombination(
                    ballisticParticleMode, ejectionResistanceMode, particleCollisionMode,
                    landingSearchMode, ejectionImpulseTargetMode, ejectionDeflectionMode, reattachmentMode)
                && ScenarioRunner.IsValidBallisticResolutionParallelism(ballisticParticleMode, ballisticResolutionParallelism)
            select 1).Count();
    }

    // TotalFrames/EstimatedSeconds are null only for ByElements.
    // which don't have a frame budget, they run until manually stopped.
    public readonly record struct EstimateResult(int Combinations, long? TotalFrames, double? EstimatedSeconds, string? Note);
}
