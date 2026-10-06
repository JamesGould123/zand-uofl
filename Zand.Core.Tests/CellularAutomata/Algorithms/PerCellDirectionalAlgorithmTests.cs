namespace Zand.Core.Tests.CellularAutomata.Algorithms;

using Xunit;
using Zand.Core.CellularAutomata;
using Zand.Core.CellularAutomata.Algorithms;
using Zand.Core.CellularAutomata.Chunking;

public class PerCellDirectionalAlgorithmTests
{
    [Fact]
    public void Update_PowderCell_DispatchesToPowderElementUnaffectedByFlowsRight()
    {
        var grid = CreateGrid(3, 3);
        grid.SetCell(1, 1, new Cell { Type = CellType.Sand });

        new PerCellDirectionalAlgorithm(3, 3, seed: 1).Update(grid, new ChunkBounds(0, 0, 3, 3));

        Assert.Equal(CellType.Empty, grid.GetCell(1, 1).Type);
        Assert.Equal(CellType.Sand, grid.GetCell(1, 2).Type);
    }

    [Fact]
    public void Update_WaterAtEveryCorner_DoesNotThrow()
    {
        var grid = CreateGrid(3, 3);
        grid.SetCell(0, 0, new Cell { Type = CellType.Water });
        grid.SetCell(2, 0, new Cell { Type = CellType.Water });
        grid.SetCell(0, 2, new Cell { Type = CellType.Water });
        grid.SetCell(2, 2, new Cell { Type = CellType.Water });
        var algorithm = new PerCellDirectionalAlgorithm(3, 3, seed: 1);

        var exception = Record.Exception(() => algorithm.Update(grid, new ChunkBounds(0, 0, 3, 3)));

        Assert.Null(exception);
    }

    [Fact]
    public void Update_AcrossTwoTicks_RemembersFlowDirectionFromPreviousTick()
    {
        var grid = CreateGrid(5, 4);
        var algorithm = new PerCellDirectionalAlgorithm(5, 4, seed: 1);
        var bounds = new ChunkBounds(0, 0, 5, 4);

        // Tick 1: straight-down and down-left are blocked, only down-right is empty -
        // the water must go right, which should set flowsRight = true.
        grid.SetCell(2, 1, new Cell { Type = CellType.Water });
        grid.SetCell(2, 2, new Cell { Type = CellType.Static });
        grid.SetCell(1, 2, new Cell { Type = CellType.Static });

        algorithm.Update(grid, bounds);

        Assert.Equal(CellType.Empty, grid.GetCell(2, 1).Type);
        Assert.Equal(CellType.Water, grid.GetCell(3, 2).Type);

        // Tick 2: straight-down is now blocked, this time both diagonal downs are open.
        // It should go right-down as flowsRight is true.
        grid.SetCell(3, 3, new Cell { Type = CellType.Static });

        algorithm.Update(grid, bounds);

        Assert.Equal(CellType.Empty, grid.GetCell(3, 2).Type);
        Assert.Equal(CellType.Water, grid.GetCell(4, 3).Type);
        Assert.Equal(CellType.Empty, grid.GetCell(2, 3).Type);
    }

    private static CaGrid CreateGrid(int width, int height) => new(width, height, new PerCellAlgorithm(1));
}
