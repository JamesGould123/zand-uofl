namespace Zand.Core.Tests.Coupling;

using Xunit;
using Zand.Core.CellularAutomata;
using Zand.Core.CellularAutomata.Algorithms;
using Zand.Core.CellularAutomata.Chunking;
using Zand.Core.Coupling;

public class TempBodyRectsTests
{
    [Fact]
    public void NoMerging_ReturnsOneRectPerCell()
    {
        var grid = CreateGrid();
        FillSand(grid, 3, 6, 5, 5);

        var rects = TempBodyRects.Build(grid, [(3, 5), (4, 5)], TempBodyMerging.NoMerging);

        Assert.Equal([new CellRect(3, 5, 1, 1), new CellRect(4, 5, 1, 1)], rects);
    }

    [Fact]
    public void HorizontalMerging_ExtendsRunToTheEndsOfTheSand()
    {
        var grid = CreateGrid();
        FillSand(grid, 3, 7, 5, 5);

        var rects = TempBodyRects.Build(grid, [(5, 5)], TempBodyMerging.HorizontalMerging);

        Assert.Equal([new CellRect(3, 5, 5, 1)], rects);
    }

    [Fact]
    public void HorizontalMerging_KeepsRunsSeparatedByAGapApart()
    {
        var grid = CreateGrid();
        FillSand(grid, 2, 4, 5, 5);
        FillSand(grid, 6, 8, 5, 5);

        var rects = TempBodyRects.Build(grid, [(3, 5), (7, 5)], TempBodyMerging.HorizontalMerging);

        Assert.Equal([new CellRect(2, 5, 3, 1), new CellRect(6, 5, 3, 1)], rects);
    }

    [Fact]
    public void HorizontalMerging_BuildsTheSameRunWhicheverCellInItIsRequested()
    {
        var grid = CreateGrid();
        FillSand(grid, 3, 7, 5, 5);

        var fromLeft = TempBodyRects.Build(grid, [(3, 5)], TempBodyMerging.HorizontalMerging);
        var fromRight = TempBodyRects.Build(grid, [(7, 5)], TempBodyMerging.HorizontalMerging);

        Assert.Equal(fromLeft, fromRight);
    }

    [Fact]
    public void HorizontalMerging_BuildsOneRectForSeveralCellsInTheSameRun()
    {
        var grid = CreateGrid();
        FillSand(grid, 3, 7, 5, 5);

        var rects = TempBodyRects.Build(grid, [(3, 5), (4, 5), (7, 5)], TempBodyMerging.HorizontalMerging);

        Assert.Single(rects);
    }

    [Fact]
    public void HorizontalMerging_CutsLongRunsIntoPiecesCountedFromTheLeftEnd()
    {
        var grid = CreateGrid(width: 100, height: 10);
        FillSand(grid, 0, 69, 2, 2);

        var firstPiece = TempBodyRects.Build(grid, [(10, 2)], TempBodyMerging.HorizontalMerging);
        var lastPiece = TempBodyRects.Build(grid, [(68, 2)], TempBodyMerging.HorizontalMerging);

        Assert.Equal([new CellRect(0, 2, TempBodyRects.MaxCells, 1)], firstPiece);
        Assert.Equal([new CellRect(TempBodyRects.MaxCells, 2, 70 - TempBodyRects.MaxCells, 1)], lastPiece);
    }

    [Fact]
    public void HorizontalMerging_StopsRunAtTheEdgeOfTheGrid()
    {
        var grid = CreateGrid();
        FillSand(grid, 0, 3, 5, 5);

        var rects = TempBodyRects.Build(grid, [(2, 5)], TempBodyMerging.HorizontalMerging);

        Assert.Equal([new CellRect(0, 5, 4, 1)], rects);
    }

    [Fact]
    public void HorizontalMerging_LeavesLiquidCandidatesAsSingleCells()
    {
        var grid = CreateGrid();
        FillSand(grid, 3, 7, 5, 5);
        grid.SetCell(5, 6, new Cell { Type = CellType.Water });
        grid.SetCell(6, 6, new Cell { Type = CellType.Water });

        var rects = TempBodyRects.Build(grid, [(5, 6)], TempBodyMerging.HorizontalMerging);

        Assert.Equal([new CellRect(5, 6, 1, 1)], rects);
    }

    [Fact]
    public void RectangleMerging_MergesRowsThatHoldTheSameRun()
    {
        var grid = CreateGrid();
        FillSand(grid, 3, 6, 4, 6);

        var rects = TempBodyRects.Build(grid, [(4, 5)], TempBodyMerging.RectangleMerging);

        Assert.Equal([new CellRect(3, 4, 4, 3)], rects);
    }

    [Fact]
    public void RectangleMerging_KeepsRowsWithDifferentRunsApart()
    {
        var grid = CreateGrid();
        FillSand(grid, 3, 7, 5, 5);
        FillSand(grid, 3, 6, 6, 6);

        var rects = TempBodyRects.Build(grid, [(3, 5), (3, 6)], TempBodyMerging.RectangleMerging);

        Assert.Equal([new CellRect(3, 5, 5, 1), new CellRect(3, 6, 4, 1)], rects);
    }

    [Fact]
    public void RectangleMerging_BuildsTheSameRectangleWhicheverCellInItIsRequested()
    {
        var grid = CreateGrid();
        FillSand(grid, 3, 6, 4, 6);

        var fromTopLeft = TempBodyRects.Build(grid, [(3, 4)], TempBodyMerging.RectangleMerging);
        var fromBottomRight = TempBodyRects.Build(grid, [(6, 6)], TempBodyMerging.RectangleMerging);

        Assert.Equal(fromTopLeft, fromBottomRight);
    }

    [Fact]
    public void RectangleMerging_BuildsOneRectangleWhenEveryCellOfItIsRequested()
    {
        var grid = CreateGrid();
        FillSand(grid, 3, 6, 4, 6);
        var everyCell = Enumerable.Range(3, 4).SelectMany(x => Enumerable.Range(4, 3), (x, y) => (x, y));

        var rects = TempBodyRects.Build(grid, everyCell, TempBodyMerging.RectangleMerging);

        Assert.Equal([new CellRect(3, 4, 4, 3)], rects);
    }

    [Fact]
    public void RectangleMerging_CutsTallRectanglesIntoPiecesCountedFromTheTop()
    {
        var grid = CreateGrid(width: 10, height: 100);
        FillSand(grid, 2, 4, 0, 69);

        var firstPiece = TempBodyRects.Build(grid, [(3, 10)], TempBodyMerging.RectangleMerging);
        var lastPiece = TempBodyRects.Build(grid, [(3, 66)], TempBodyMerging.RectangleMerging);

        Assert.Equal([new CellRect(2, 0, 3, TempBodyRects.MaxCells)], firstPiece);
        Assert.Equal([new CellRect(2, TempBodyRects.MaxCells, 3, 70 - TempBodyRects.MaxCells)], lastPiece);
    }

    private static CaGrid CreateGrid(int width = 20, int height = 20) =>
        new(width, height, new PerCellAlgorithm(1), ChunkSize.Disabled);

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
}
