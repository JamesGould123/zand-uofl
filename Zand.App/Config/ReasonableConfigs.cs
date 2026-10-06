namespace Zand.App.Config;

using System;
using System.Collections.Generic;
using System.Linq;
using Zand.Core.Ballistics;
using Zand.Core.CellularAutomata;
using Zand.Core.CellularAutomata.Algorithms;
using Zand.Core.CellularAutomata.Chunking;
using Zand.Core.Coupling;
using Zand.Rendering;

public static class ReasonableConfigs
{
    // Four full configurations, spanning most options considered suitable candidates for
    // continued investigation. Used to provide a baseline to benchmark individual axes.
    public static readonly SampleConfig[] All =
    {
        // WeakOneWayRbToCa never creates temporary sand bodies, so merging has no effect on this config
        // and NoMerging is the only merging value valid for it (see ScenarioRunner.IsValidTempBodyMerging).
        new(
            SharedWorldSize, CaAlgorithmType.PerCellDirectional, RigidBodySimulationType.Box2D,
            CouplingMode.WeakOneWayRbToCa, LiquidPhysicsMode.None, GravityMode.Normal, SharedTempBodyLifetime, TempBodyMerging.NoMerging,
            SharedChunkSize, SharedActiveRectangle, SharedParallelism, SharedLiquidPressure, SharedSameTickMoveGuard, SharedRowSweepOrder, SharedCellRenderSmoothing,
            SharedBallisticParticles, SharedEjectionResistance, SharedParticleCollision, SharedLandingSearch,
            SharedEjectionImpulseTarget, SharedEjectionDeflection, ReattachmentMode.OnCollision,
            SharedRasterizationBodyParallelism, SharedRasterizationCellParallelism, SharedBallisticResolutionParallelism,
            SharedRenderParallelism),
        new(
            SharedWorldSize, CaAlgorithmType.PerCellDirectional, RigidBodySimulationType.Chipmunk,
            CouplingMode.WeakOneWayCaToRb, LiquidPhysicsMode.BuoyancyAndDamping, GravityMode.High, SharedTempBodyLifetime, SharedTempBodyMerging,
            SharedChunkSize, SharedActiveRectangle, SharedParallelism, SharedLiquidPressure, SharedSameTickMoveGuard, SharedRowSweepOrder, SharedCellRenderSmoothing,
            SharedBallisticParticles, SharedEjectionResistance, SharedParticleCollision, SharedLandingSearch,
            SharedEjectionImpulseTarget, SharedEjectionDeflection, ReattachmentMode.OnCollision,
            SharedRasterizationBodyParallelism, SharedRasterizationCellParallelism, SharedBallisticResolutionParallelism,
            SharedRenderParallelism),
        new(
            SharedWorldSize, CaAlgorithmType.MargolusGuardedRandomized, RigidBodySimulationType.Box2D,
            CouplingMode.WeakTwoWayCaFirst, LiquidPhysicsMode.BuoyancyAndDamping, GravityMode.Normal, SharedTempBodyLifetime, SharedTempBodyMerging,
            SharedChunkSize, SharedActiveRectangle, SharedParallelism, SharedLiquidPressure, SharedSameTickMoveGuard, SharedRowSweepOrder, SharedCellRenderSmoothing,
            SharedBallisticParticles, SharedEjectionResistance, SharedParticleCollision, SharedLandingSearch,
            SharedEjectionImpulseTarget, SharedEjectionDeflection, ReattachmentMode.VelocitySettling,
            SharedRasterizationBodyParallelism, SharedRasterizationCellParallelism, SharedBallisticResolutionParallelism,
            SharedRenderParallelism),
        new(
            SharedWorldSize, CaAlgorithmType.MargolusGuardedRandomized, RigidBodySimulationType.Chipmunk,
            CouplingMode.WeakTwoWayRbFirst, LiquidPhysicsMode.BuoyancyAndDamping, GravityMode.High, SharedTempBodyLifetime, SharedTempBodyMerging,
            SharedChunkSize, SharedActiveRectangle, SharedParallelism, SharedLiquidPressure, SharedSameTickMoveGuard, SharedRowSweepOrder, SharedCellRenderSmoothing,
            SharedBallisticParticles, SharedEjectionResistance, SharedParticleCollision, SharedLandingSearch,
            SharedEjectionImpulseTarget, SharedEjectionDeflection, ReattachmentMode.VelocitySettling,
            SharedRasterizationBodyParallelism, SharedRasterizationCellParallelism, SharedBallisticResolutionParallelism,
            SharedRenderParallelism)
    };

    private const WorldSizeOption SharedWorldSize = WorldSizeOption.Small512;
    private const ChunkSize SharedChunkSize = ChunkSize.ThirtyTwo;
    private const ActiveRectangle SharedActiveRectangle = ActiveRectangle.Enabled;
    private const TempBodyLifetime SharedTempBodyLifetime = TempBodyLifetime.Persistent;
    private const TempBodyMerging SharedTempBodyMerging = TempBodyMerging.HorizontalMerging;
    private const ParallelismMode SharedParallelism = ParallelismMode.ParallelLargestFirst;
    private const LiquidPressureMode SharedLiquidPressure = LiquidPressureMode.Disabled;
    private const SameTickMoveGuard SharedSameTickMoveGuard = SameTickMoveGuard.Enabled;
    private const RowSweepOrder SharedRowSweepOrder = RowSweepOrder.Oscillating;
    private const CellRenderSmoothing SharedCellRenderSmoothing = CellRenderSmoothing.Smoothing;
    private const BallisticParticleMode SharedBallisticParticles = BallisticParticleMode.PointSwarm;
    private const EjectionResistanceMode SharedEjectionResistance = EjectionResistanceMode.Impulse;
    private const ParticleCollisionMode SharedParticleCollision = ParticleCollisionMode.Enabled;
    private const LandingSearchMode SharedLandingSearch = LandingSearchMode.Wide;
    private const EjectionImpulseTargetMode SharedEjectionImpulseTarget = EjectionImpulseTargetMode.ContactPoint;
    private const EjectionDeflectionMode SharedEjectionDeflection = EjectionDeflectionMode.FaceNormal;
    private const RasterizationBodyParallelism SharedRasterizationBodyParallelism = RasterizationBodyParallelism.Parallel;
    private const RasterizationCellParallelism SharedRasterizationCellParallelism = RasterizationCellParallelism.Parallel;
    private const BallisticResolutionParallelism SharedBallisticResolutionParallelism = BallisticResolutionParallelism.Parallel;
    private const RenderParallelism SharedRenderParallelism = RenderParallelism.Parallel;

    public static HashSet<T> Union<T>(Func<SampleConfig, T> selector)
        where T : notnull
    {
        return All.Select(selector).ToHashSet();
    }
}
