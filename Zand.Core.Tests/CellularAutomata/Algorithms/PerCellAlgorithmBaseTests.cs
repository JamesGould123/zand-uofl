namespace Zand.Core.Tests.CellularAutomata.Algorithms;

using Xunit;
using Zand.Core.CellularAutomata;
using Zand.Core.CellularAutomata.Algorithms;
using Zand.Core.CellularAutomata.Chunking;

public class PerCellAlgorithmBaseTests
{
    [Fact]
    public void Update_DefaultDirection_VisitsRowsBottomUpAndCellsLeftToRight()
    {
        var algorithm = new RecordingAlgorithm(seed: 1);
        var grid = new CaGrid(3, 2, algorithm);

        algorithm.Update(grid, new ChunkBounds(0, 0, 3, 2));

        Assert.Equal(
            [(0, 1), (1, 1), (2, 1), (0, 0), (1, 0), (2, 0)],
            algorithm.Visited);
    }

    [Fact]
    public void Update_ForcedRightToLeft_VisitsCellsRightToLeftWithinEachRow()
    {
        var algorithm = new RecordingAlgorithm(seed: 1);
        var grid = new CaGrid(3, 2, algorithm);
        algorithm.SweepRightToLeft = true;

        algorithm.Update(grid, new ChunkBounds(0, 0, 3, 2));

        Assert.Equal(
            [(2, 1), (1, 1), (0, 1), (2, 0), (1, 0), (0, 0)],
            algorithm.Visited);
    }

    [Fact]
    public void Update_ForcedTopDown_VisitsRowsTopDownAndCellsLeftToRight()
    {
        var algorithm = new RecordingAlgorithm(seed: 1);
        var grid = new CaGrid(3, 2, algorithm);
        algorithm.SweepBottomToTop = false;

        algorithm.Update(grid, new ChunkBounds(0, 0, 3, 2));

        Assert.Equal(
            [(0, 0), (1, 0), (2, 0), (0, 1), (1, 1), (2, 1)],
            algorithm.Visited);
    }

    [Fact]
    public void Constructor_BottomUp_StartsSweepingBottomToTop()
    {
        var algorithm = new RecordingAlgorithm(seed: 1, RowSweepOrder.BottomUp);

        Assert.True(algorithm.SweepBottomToTop);
    }

    [Fact]
    public void Constructor_TopDown_StartsSweepingTopToBottom()
    {
        var algorithm = new RecordingAlgorithm(seed: 1, RowSweepOrder.TopDown);

        Assert.False(algorithm.SweepBottomToTop);
    }

    [Fact]
    public void Constructor_Oscillating_StartsSweepingBottomToTop()
    {
        var algorithm = new RecordingAlgorithm(seed: 1, RowSweepOrder.Oscillating);

        Assert.True(algorithm.SweepBottomToTop);
    }

    [Fact]
    public void OnTickComplete_BottomUp_NeverChanges()
    {
        var algorithm = new RecordingAlgorithm(seed: 1, RowSweepOrder.BottomUp);

        algorithm.OnTickComplete();
        algorithm.OnTickComplete();

        Assert.True(algorithm.SweepBottomToTop);
    }

    [Fact]
    public void OnTickComplete_TopDown_NeverChanges()
    {
        var algorithm = new RecordingAlgorithm(seed: 1, RowSweepOrder.TopDown);

        algorithm.OnTickComplete();
        algorithm.OnTickComplete();

        Assert.False(algorithm.SweepBottomToTop);
    }

    [Fact]
    public void OnTickComplete_Oscillating_FlipsEveryTick()
    {
        var algorithm = new RecordingAlgorithm(seed: 1, RowSweepOrder.Oscillating);

        algorithm.OnTickComplete();
        Assert.False(algorithm.SweepBottomToTop);

        algorithm.OnTickComplete();
        Assert.True(algorithm.SweepBottomToTop);
    }

    [Fact]
    public void Update_ForcedBottomUp_BlockedColumnCollapsesDownInOneTick()
    {
        var algorithm = new PerCellAlgorithm(seed: 1);
        var grid = new CaGrid(2, 4, algorithm, sameTickMoveGuard: SameTickMoveGuard.Enabled);
        SetupBlockedColumnWithOpenNeighbor(grid);

        algorithm.Update(grid, new ChunkBounds(0, 0, 2, 4));

        Assert.Equal(CellType.Empty, grid.GetCell(0, 0).Type);
        Assert.Equal(CellType.Water, grid.GetCell(0, 1).Type);
        Assert.Equal(CellType.Empty, grid.GetCell(1, 1).Type);
        Assert.Equal(CellType.Water, grid.GetCell(1, 2).Type);
    }

    [Fact]
    public void Update_ForcedTopDown_OriginalBottomCellStaysPutForOneTick()
    {
        var algorithm = new PerCellAlgorithm(seed: 1);
        var grid = new CaGrid(2, 4, algorithm, sameTickMoveGuard: SameTickMoveGuard.Enabled);
        algorithm.SweepBottomToTop = false;
        SetupBlockedColumnWithOpenNeighbor(grid);

        algorithm.Update(grid, new ChunkBounds(0, 0, 2, 4));

        Assert.Equal(CellType.Empty, grid.GetCell(0, 1).Type);
        Assert.Equal(CellType.Water, grid.GetCell(1, 1).Type);
        Assert.Equal(CellType.Water, grid.GetCell(0, 2).Type);
        Assert.Equal(CellType.Water, grid.GetCell(1, 2).Type);
    }

    [Fact]
    public void Update_BoundsSubRegion_OnlyVisitsCellsWithinBounds()
    {
        var algorithm = new RecordingAlgorithm(seed: 1);
        var grid = new CaGrid(4, 4, algorithm);

        algorithm.Update(grid, new ChunkBounds(1, 1, 3, 3));

        Assert.Equal(
            [(1, 2), (2, 2), (1, 1), (2, 1)],
            algorithm.Visited);
    }

    // Note: this and the next test are specifically testing part of the bias that
    // the Randomized and Directional variants aim to avoid.
    [Fact]
    public void Update_SweepLeftToRight_LiquidRowOnlyNearestCellFillsGap()
    {
        var algorithm = new PerCellAlgorithm(seed: 1);
        var grid = new CaGrid(4, 2, algorithm);
        SetupLiquidRowWithGapAtRightEnd(grid);

        algorithm.Update(grid, new ChunkBounds(0, 0, 3, 2));

        Assert.Equal(CellType.Water, grid.GetCell(0, 0).Type);
        Assert.Equal(CellType.Water, grid.GetCell(1, 0).Type);
        Assert.Equal(CellType.Empty, grid.GetCell(2, 0).Type);
        Assert.Equal(CellType.Water, grid.GetCell(3, 0).Type);
    }

    [Fact]
    public void Update_SweepRightToLeft_LiquidRowCascadesAcrossWholeRow()
    {
        var algorithm = new PerCellAlgorithm(seed: 1);
        var grid = new CaGrid(4, 2, algorithm);
        SetupLiquidRowWithGapAtRightEnd(grid);
        algorithm.SweepRightToLeft = true;

        algorithm.Update(grid, new ChunkBounds(0, 0, 3, 2));

        Assert.Equal(CellType.Empty, grid.GetCell(0, 0).Type);
        Assert.Equal(CellType.Water, grid.GetCell(1, 0).Type);
        Assert.Equal(CellType.Water, grid.GetCell(2, 0).Type);
        Assert.Equal(CellType.Water, grid.GetCell(3, 0).Type);
    }

    private static void SetupLiquidRowWithGapAtRightEnd(CaGrid grid)
    {
        for (int x = 0; x < grid.Width; x++)
        {
            grid.SetCell(x, 1, new Cell { Type = CellType.Static });
        }

        grid.SetCell(0, 0, new Cell { Type = CellType.Water });
        grid.SetCell(1, 0, new Cell { Type = CellType.Water });
        grid.SetCell(2, 0, new Cell { Type = CellType.Water });
    }

    // A 3-cell tall column of water particles at x=0, y 0-2 placed above a "floor" of two static particles x=0-1, y=3,
    // with the neighboring column x=1 left empty above the floor.
    private static void SetupBlockedColumnWithOpenNeighbor(CaGrid grid)
    {
        grid.SetCell(0, 3, new Cell { Type = CellType.Static });
        grid.SetCell(1, 3, new Cell { Type = CellType.Static });
        grid.SetCell(0, 0, new Cell { Type = CellType.Water });
        grid.SetCell(0, 1, new Cell { Type = CellType.Water });
        grid.SetCell(0, 2, new Cell { Type = CellType.Water });
    }

    private sealed class RecordingAlgorithm(int seed, RowSweepOrder rowSweepOrder = RowSweepOrder.BottomUp)
        : PerCellAlgorithmBase(seed, rowSweepOrder)
    {
        public List<(int X, int Y)> Visited { get; } = [];

        protected override void UpdateCell(CaGrid grid, int x, int y) => Visited.Add((x, y));
    }
}
