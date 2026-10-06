namespace Zand.Core.Tests.Coupling;

using System.Numerics;
using Xunit;
using Zand.Core.CellularAutomata;
using Zand.Core.CellularAutomata.Algorithms;
using Zand.Core.CellularAutomata.Chunking;
using Zand.Core.Coupling;
using Zand.Core.RigidBody;
using Zand.Core.Tests.Mocks;

// Uses TestCouplingStrategy, a wrapper that exposes
// WeakCouplingStrategyBase's implemented methods for testing as Exposed[MethodName]
public class WeakCouplingStrategyBaseTests
{
    private const int WorldSize = 20;

    // ----- StampBodies / ClearStamps -----
    [Fact]
    public void StampBodies_StampsEmptyCellsInBodyFootprintAsStatic()
    {
        var grid = CreateGrid();
        var physics = new FakeRigidBodySimulation();
        physics.AddDynamicBox(10.5f, 9.5f, 0.4f, 0.4f);
        var strategy = new TestCouplingStrategy(1f, WorldSize);

        strategy.ExposedStampBodies(grid, physics);

        Assert.Equal(CellType.Static, grid.GetCell(10, 10).Type);
    }

    [Fact]
    public void StampBodies_DoesNotOverwriteCellsAlreadyOccupiedByParticles()
    {
        var grid = CreateGrid();
        grid.SetCell(10, 10, new Cell { Type = CellType.Sand });
        var physics = new FakeRigidBodySimulation();
        physics.AddDynamicBox(10.5f, 9.5f, 0.4f, 0.4f);
        var strategy = new TestCouplingStrategy(1f, WorldSize);

        strategy.ExposedStampBodies(grid, physics);

        Assert.Equal(CellType.Sand, grid.GetCell(10, 10).Type);
    }

    [Fact]
    public void ClearStamps_RevertsOnlyTheCellsItStamped()
    {
        var grid = CreateGrid();
        grid.SetCell(10, 10, new Cell { Type = CellType.Sand });
        var physics = new FakeRigidBodySimulation();

        // Wide enough to cover both cell (10,10) [already Sand] and (11,10) [empty].
        physics.AddDynamicBox(11.0f, 9.5f, 0.6f, 0.4f);
        var strategy = new TestCouplingStrategy(1f, WorldSize);

        strategy.ExposedStampBodies(grid, physics);
        Assert.Equal(CellType.Static, grid.GetCell(11, 10).Type);

        strategy.ExposedClearStamps(grid);

        Assert.Equal(CellType.Empty, grid.GetCell(11, 10).Type);
        Assert.Equal(CellType.Sand, grid.GetCell(10, 10).Type);
    }

    [Fact]
    public void StampBodies_OverlappingBodyFootprints_StampAndClearWithoutError()
    {
        var grid = CreateGrid();
        var physics = new FakeRigidBodySimulation();
        physics.AddDynamicBox(10.5f, 9.5f, 0.4f, 0.4f);
        physics.AddDynamicBox(10.5f, 9.5f, 0.4f, 0.4f);
        var strategy = new TestCouplingStrategy(1f, WorldSize);

        strategy.ExposedStampBodies(grid, physics);
        Assert.Equal(CellType.Static, grid.GetCell(10, 10).Type);

        strategy.ExposedClearStamps(grid);

        Assert.Equal(CellType.Empty, grid.GetCell(10, 10).Type);
    }

    // ----- DispatchScanAndSync / DispatchRemove -----
    [Fact]
    public void DispatchScanAndSync_EphemeralMode_AddsTempStaticBodyForSandCellInFootprint()
    {
        var grid = CreateGrid();
        grid.SetCell(10, 15, new Cell { Type = CellType.Sand });
        var physics = new FakeRigidBodySimulation();
        physics.AddDynamicBox(10.5f, 4.5f, 0.4f, 0.4f);
        var strategy = new TestCouplingStrategy(1f, WorldSize);

        strategy.ExposedDispatchScanAndSync(grid, physics);

        var tempBody = Assert.Single(physics.GetBodies(), b => b.IsStatic);
        AssertVectorApprox(10.5f, 4.5f, tempBody.Position);
    }

    [Fact]
    public void DispatchRemove_EphemeralMode_RemovesAllTempBodiesAddedThisTick()
    {
        var grid = CreateGrid();
        grid.SetCell(10, 15, new Cell { Type = CellType.Sand });
        var physics = new FakeRigidBodySimulation();
        physics.AddDynamicBox(10.5f, 4.5f, 0.4f, 0.4f);
        var strategy = new TestCouplingStrategy(1f, WorldSize);
        strategy.ExposedDispatchScanAndSync(grid, physics);
        var tempHandle = physics.GetBodies().Single(b => b.IsStatic).Handle;

        strategy.ExposedDispatchRemove(physics);

        Assert.Contains(tempHandle, physics.RemovedBodies);
        Assert.DoesNotContain(physics.GetBodies(), b => b.IsStatic);
    }

    [Fact]
    public void DispatchScanAndSync_LiquidCellsNeverCandidates_WithoutProbabilisticCollisionMode()
    {
        var grid = CreateGrid();
        grid.SetCell(10, 15, new Cell { Type = CellType.Water });
        var physics = new FakeRigidBodySimulation();
        physics.AddDynamicBox(10.5f, 4.5f, 0.4f, 0.4f);
        var strategy = new TestCouplingStrategy(1f, WorldSize);

        strategy.ExposedDispatchScanAndSync(grid, physics);

        Assert.DoesNotContain(physics.GetBodies(), b => b.IsStatic);
    }

    [Fact]
    public void DispatchScanAndSync_PersistentMode_KeepsSameBodyAliveWhenCellStillDesiredAcrossTicks()
    {
        var grid = CreateGrid();
        grid.SetCell(10, 15, new Cell { Type = CellType.Sand });
        var physics = new FakeRigidBodySimulation();
        physics.AddDynamicBox(10.5f, 4.5f, 0.4f, 0.4f);
        var strategy = new TestCouplingStrategy(1f, WorldSize, tempBodyLifetime: TempBodyLifetime.Persistent);
        strategy.ExposedDispatchScanAndSync(grid, physics);
        var tempHandle = physics.GetBodies().Single(b => b.IsStatic).Handle;

        strategy.ExposedDispatchScanAndSync(grid, physics);

        Assert.DoesNotContain(tempHandle, physics.RemovedBodies);
        Assert.Single(physics.GetBodies(), b => b.IsStatic);
    }

    [Fact]
    public void DispatchScanAndSync_PersistentMode_RemovesBodyWhenCellNoLongerDesired()
    {
        var grid = CreateGrid();
        grid.SetCell(10, 15, new Cell { Type = CellType.Sand });
        var physics = new FakeRigidBodySimulation();
        physics.AddDynamicBox(10.5f, 4.5f, 0.4f, 0.4f);
        var strategy = new TestCouplingStrategy(1f, WorldSize, tempBodyLifetime: TempBodyLifetime.Persistent);
        strategy.ExposedDispatchScanAndSync(grid, physics);
        var tempHandle = physics.GetBodies().Single(b => b.IsStatic).Handle;
        grid.SetCell(10, 15, new Cell { Type = CellType.Empty });

        strategy.ExposedDispatchScanAndSync(grid, physics);

        Assert.Contains(tempHandle, physics.RemovedBodies);
        Assert.DoesNotContain(physics.GetBodies(), b => b.IsStatic);
    }

    [Fact]
    public void DispatchScanAndSync_PersistentMode_NeverCreatesTempBodiesForLiquidCells()
    {
        var grid = CreateGrid();
        grid.SetCell(10, 15, new Cell { Type = CellType.Water });
        var physics = new FakeRigidBodySimulation();
        physics.AddDynamicBox(10.5f, 4.5f, 0.4f, 0.4f);
        var strategy = new TestCouplingStrategy(
            1f, WorldSize, liquidPhysicsMode: LiquidPhysicsMode.ProbabilisticCollision, tempBodyLifetime: TempBodyLifetime.Persistent);

        strategy.ExposedDispatchScanAndSync(grid, physics);

        Assert.DoesNotContain(physics.GetBodies(), b => b.IsStatic);
    }

    [Fact]
    public void DispatchScanAndSync_PersistentMode_BodyMoved_TracksNewFootprintInsteadOfStaleOne()
    {
        var grid = CreateGrid();
        grid.SetCell(10, 15, new Cell { Type = CellType.Sand });
        grid.SetCell(5, 15, new Cell { Type = CellType.Sand });
        var physics = new FakeRigidBodySimulation();
        var handle = physics.AddDynamicBox(10.5f, 4.5f, 0.4f, 0.4f);
        var strategy = new TestCouplingStrategy(1f, WorldSize, tempBodyLifetime: TempBodyLifetime.Persistent);
        strategy.ExposedDispatchScanAndSync(grid, physics);
        AssertVectorApprox(10.5f, 4.5f, physics.GetBodies().Single(b => b.IsStatic).Position);

        physics.MoveBody(handle, new Vector2(5.5f, 4.5f));
        strategy.ExposedDispatchScanAndSync(grid, physics);

        AssertVectorApprox(5.5f, 4.5f, physics.GetBodies().Single(b => b.IsStatic).Position);
    }

    // ----- Temp body merging -----
    [Fact]
    public void DispatchScanAndSync_HorizontalMerging_MakesOneBodyForARunOfSandCells()
    {
        var grid = CreateGrid();
        FillSand(grid, 9, 11, 15, 15);
        var physics = new FakeRigidBodySimulation();
        physics.AddDynamicBox(10.5f, 4.5f, 0.4f, 0.4f);
        var strategy = new TestCouplingStrategy(1f, WorldSize, tempBodyMerging: TempBodyMerging.HorizontalMerging);

        strategy.ExposedDispatchScanAndSync(grid, physics);

        var tempBody = Assert.Single(physics.GetBodies(), b => b.IsStatic);
        AssertVectorApprox(10.5f, 4.5f, tempBody.Position);
        Assert.Equal(new BoxShape(1.5f, 0.5f), tempBody.Shape);
    }

    [Fact]
    public void DispatchScanAndSync_RectangleMerging_MakesOneBodyForABlockOfSandCells()
    {
        var grid = CreateGrid();
        FillSand(grid, 9, 11, 15, 16);
        var physics = new FakeRigidBodySimulation();
        physics.AddDynamicBox(10.5f, 4.5f, 0.4f, 0.4f);
        var strategy = new TestCouplingStrategy(1f, WorldSize, tempBodyMerging: TempBodyMerging.RectangleMerging);

        strategy.ExposedDispatchScanAndSync(grid, physics);

        var tempBody = Assert.Single(physics.GetBodies(), b => b.IsStatic);
        AssertVectorApprox(10.5f, 4f, tempBody.Position);
        Assert.Equal(new BoxShape(1.5f, 1f), tempBody.Shape);
    }

    [Fact]
    public void DispatchScanAndSync_NoMerging_MakesOneBodyPerSandCell()
    {
        var grid = CreateGrid();
        FillSand(grid, 9, 11, 15, 15);
        var physics = new FakeRigidBodySimulation();
        physics.AddDynamicBox(10.5f, 4.5f, 0.4f, 0.4f);
        var strategy = new TestCouplingStrategy(1f, WorldSize);

        strategy.ExposedDispatchScanAndSync(grid, physics);

        Assert.Equal(3, physics.GetBodies().Count(b => b.IsStatic));
    }

    [Fact]
    public void DispatchScanAndSync_EphemeralMergedBodies_AreAllRemovedByDispatchRemove()
    {
        var grid = CreateGrid();
        FillSand(grid, 9, 11, 15, 16);
        var physics = new FakeRigidBodySimulation();
        physics.AddDynamicBox(10.5f, 4.5f, 0.4f, 0.4f);
        var strategy = new TestCouplingStrategy(1f, WorldSize, tempBodyMerging: TempBodyMerging.RectangleMerging);
        strategy.ExposedDispatchScanAndSync(grid, physics);

        strategy.ExposedDispatchRemove(physics);

        Assert.DoesNotContain(physics.GetBodies(), b => b.IsStatic);
    }

    [Theory]
    [InlineData(TempBodyMerging.HorizontalMerging)]
    [InlineData(TempBodyMerging.RectangleMerging)]
    public void DispatchScanAndSync_PersistentMergedBody_IsKeptWhenTheRigidBodyMovesAlongTheSameSand(TempBodyMerging merging)
    {
        var grid = CreateGrid();
        FillSand(grid, 5, 12, 15, 15);
        var physics = new FakeRigidBodySimulation();
        var handle = physics.AddDynamicBox(10.5f, 4.5f, 0.4f, 0.4f);
        var strategy = new TestCouplingStrategy(
            1f, WorldSize, tempBodyLifetime: TempBodyLifetime.Persistent, tempBodyMerging: merging);
        strategy.ExposedDispatchScanAndSync(grid, physics);
        var tempHandle = physics.GetBodies().Single(b => b.IsStatic).Handle;

        physics.MoveBody(handle, new Vector2(8.5f, 4.5f));
        strategy.ExposedDispatchScanAndSync(grid, physics);

        Assert.DoesNotContain(tempHandle, physics.RemovedBodies);
        Assert.Single(physics.GetBodies(), b => b.IsStatic);
    }

    [Fact]
    public void DispatchScanAndSync_PersistentMergedBody_IsReplacedWhenTheSandInItChanges()
    {
        var grid = CreateGrid();
        FillSand(grid, 9, 11, 15, 15);
        var physics = new FakeRigidBodySimulation();
        physics.AddDynamicBox(10.5f, 4.5f, 0.4f, 0.4f);
        var strategy = new TestCouplingStrategy(
            1f, WorldSize, tempBodyLifetime: TempBodyLifetime.Persistent, tempBodyMerging: TempBodyMerging.HorizontalMerging);
        strategy.ExposedDispatchScanAndSync(grid, physics);
        var tempHandle = physics.GetBodies().Single(b => b.IsStatic).Handle;
        grid.SetCell(10, 15, new Cell { Type = CellType.Empty });

        strategy.ExposedDispatchScanAndSync(grid, physics);

        Assert.Contains(tempHandle, physics.RemovedBodies);
        Assert.Equal(2, physics.GetBodies().Count(b => b.IsStatic));
    }

    // ----- ComputeLiquidInteraction / ApplyDamping -----
    [Fact]
    public void LiquidInteraction_ModeNone_NeverAppliesForceOrDamping()
    {
        var (physics, handle, strategy) = SetUpBodyForLiquidTest(LiquidPhysicsMode.None);

        strategy.ExposedComputeLiquidInteraction(CreateGrid(), physics);
        strategy.ExposedApplyDamping(CreateGrid(), physics);

        Assert.Empty(physics.AppliedForces);
        Assert.Empty(physics.SetLinearVelocityCalls);
        AssertVectorApprox(1f, 2f, physics.GetLinearVelocity(handle));
    }

    [Fact]
    public void LiquidInteraction_ProbabilisticCollision_NeverAppliesForceOrDamping()
    {
        var (physics, handle, strategy) = SetUpBodyForLiquidTest(LiquidPhysicsMode.ProbabilisticCollision);

        strategy.ExposedComputeLiquidInteraction(CreateGrid(), physics);
        strategy.ExposedApplyDamping(CreateGrid(), physics);

        Assert.Empty(physics.AppliedForces);
        Assert.Empty(physics.SetLinearVelocityCalls);
        AssertVectorApprox(1f, 2f, physics.GetLinearVelocity(handle));
    }

    [Fact]
    public void LiquidInteraction_VelocityDamping_ScalesVelocityButNeverAppliesForce()
    {
        var grid = CreateGrid();
        grid.SetCell(10, 15, new Cell { Type = CellType.Water });
        var (physics, handle, strategy) = SetUpBodyForLiquidTest(LiquidPhysicsMode.VelocityDamping);

        strategy.ExposedComputeLiquidInteraction(grid, physics);
        strategy.ExposedApplyDamping(grid, physics);

        // total=1 (the single 0-margin cell), dampingSum = DampingStrength(Water) = 0.05
        // scale = 1 - 0.05/1 = 0.95
        Assert.Empty(physics.AppliedForces);
        AssertVectorApprox(0.95f, 1.9f, physics.GetLinearVelocity(handle));
        Assert.Equal(2.85f, physics.GetAngularVelocity(handle), precision: 3);
    }

    [Fact]
    public void LiquidInteraction_BuoyancyAndDamping_AppliesUpwardForceAndScalesVelocity()
    {
        var grid = CreateGrid();
        grid.SetCell(10, 15, new Cell { Type = CellType.Water });
        var (physics, handle, strategy) = SetUpBodyForLiquidTest(LiquidPhysicsMode.BuoyancyAndDamping);

        strategy.ExposedComputeLiquidInteraction(grid, physics);
        strategy.ExposedApplyDamping(grid, physics);

        // buoyancy = DensityKgM3(Water) * cellArea(1) * gravityMagnitude(10) = 800 * 1 * 10 = 8000
        var force = Assert.Single(physics.AppliedForces, f => f.Handle.Equals(handle));
        AssertVectorApprox(0f, 8000f, new Vector2(force.ForceX, force.ForceY));
        AssertVectorApprox(0.95f, 1.9f, physics.GetLinearVelocity(handle));
    }

    [Fact]
    public void ApplyDamping_BodyWithNoLiquidCellsInFootprint_NeverCallsSetVelocity()
    {
        var (physics, handle, strategy) = SetUpBodyForLiquidTest(LiquidPhysicsMode.BuoyancyAndDamping);
        var grid = CreateGrid();

        // No liquid cell placed anywhere near the body this time.
        strategy.ExposedComputeLiquidInteraction(grid, physics);
        strategy.ExposedApplyDamping(grid, physics);

        Assert.Empty(physics.AppliedForces);
        Assert.Empty(physics.SetLinearVelocityCalls);
        Assert.Empty(physics.SetAngularVelocityCalls);
        AssertVectorApprox(1f, 2f, physics.GetLinearVelocity(handle));
    }

    [Fact]
    public void ApplyDamping_StaticBody_IsAlwaysSkipped()
    {
        var grid = CreateGrid();
        grid.SetCell(10, 15, new Cell { Type = CellType.Water });
        var physics = new FakeRigidBodySimulation();
        physics.AddStaticBox(10.5f, 4.5f, 0.4f, 0.4f);
        var strategy = new TestCouplingStrategy(1f, WorldSize, liquidPhysicsMode: LiquidPhysicsMode.BuoyancyAndDamping);

        strategy.ExposedComputeLiquidInteraction(grid, physics);
        strategy.ExposedApplyDamping(grid, physics);

        Assert.Empty(physics.AppliedForces);
        Assert.Empty(physics.SetLinearVelocityCalls);
    }

    // ----- Parallel body processing consistency -----
    [Fact]
    public void MultipleNonOverlappingBodies_ParallelBodyProcessingMatchesSequential()
    {
        var sequentialGrid = CreateGrid();
        var sequentialPhysics = new FakeRigidBodySimulation();
        sequentialPhysics.AddDynamicBox(5.5f, 9.5f, 0.4f, 0.4f);
        sequentialPhysics.AddDynamicBox(15.5f, 9.5f, 0.4f, 0.4f);
        var sequentialStrategy = new TestCouplingStrategy(1f, WorldSize, bodyParallelism: RasterizationBodyParallelism.Sequential);

        var parallelGrid = CreateGrid();
        var parallelPhysics = new FakeRigidBodySimulation();
        parallelPhysics.AddDynamicBox(5.5f, 9.5f, 0.4f, 0.4f);
        parallelPhysics.AddDynamicBox(15.5f, 9.5f, 0.4f, 0.4f);
        var parallelStrategy = new TestCouplingStrategy(1f, WorldSize, bodyParallelism: RasterizationBodyParallelism.Parallel);

        sequentialStrategy.ExposedStampBodies(sequentialGrid, sequentialPhysics);
        parallelStrategy.ExposedStampBodies(parallelGrid, parallelPhysics);

        Assert.Equal(CellType.Static, sequentialGrid.GetCell(5, 10).Type);
        Assert.Equal(CellType.Static, sequentialGrid.GetCell(15, 10).Type);
        Assert.Equal(sequentialGrid.GetCell(5, 10).Type, parallelGrid.GetCell(5, 10).Type);
        Assert.Equal(sequentialGrid.GetCell(15, 10).Type, parallelGrid.GetCell(15, 10).Type);
    }

    private static (FakeRigidBodySimulation Physics, BodyHandle Handle, TestCouplingStrategy Strategy) SetUpBodyForLiquidTest(
        LiquidPhysicsMode liquidPhysicsMode)
    {
        var physics = new FakeRigidBodySimulation();
        var handle = physics.AddDynamicBox(10.5f, 4.5f, 0.4f, 0.4f);
        physics.SetLinearVelocity(handle, new Vector2(1, 2));
        physics.SetAngularVelocity(handle, 3f);
        physics.SetLinearVelocityCalls.Clear();
        physics.SetAngularVelocityCalls.Clear();
        var strategy = new TestCouplingStrategy(1f, WorldSize, liquidPhysicsMode: liquidPhysicsMode);
        return (physics, handle, strategy);
    }

    private static CaGrid CreateGrid(int width = WorldSize, int height = WorldSize, ChunkSize chunkSize = ChunkSize.Disabled) =>
        new(width, height, new PerCellAlgorithm(1), chunkSize);

    private static void FillSand(CaGrid grid, int fromX, int toX, int fromY, int toY)
    {
        for (int y = fromY; y <= toY; y++)
        {
            for (int x = fromX; x <= toX; x++)
            {
                grid.SetCell(x, y, new Cell { Type = CellType.Sand });
            }
        }
    }

    private static void AssertVectorApprox(float expectedX, float expectedY, Vector2 actual, float tolerance = 1e-3f)
    {
        Assert.True(MathF.Abs(actual.X - expectedX) < tolerance, $"X: expected {expectedX}, got {actual.X}");
        Assert.True(MathF.Abs(actual.Y - expectedY) < tolerance, $"Y: expected {expectedY}, got {actual.Y}");
    }
}
