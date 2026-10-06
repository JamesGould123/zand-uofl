namespace Zand.Core.Tests.CellularAutomata.Algorithms;

using Xunit;
using Zand.Core.CellularAutomata;
using Zand.Core.CellularAutomata.Algorithms;
using Zand.Core.CellularAutomata.Chunking;

public class PerCellAlgorithmTests
{
    [Fact]
    public void Update_PowderCell_DispatchesToPowderElement()
    {
        var grid = CreateGrid(3, 3);
        grid.SetCell(1, 1, new Cell { Type = CellType.Sand });

        new PerCellAlgorithm(seed: 1).Update(grid, new ChunkBounds(0, 0, 3, 3));

        Assert.Equal(CellType.Empty, grid.GetCell(1, 1).Type);
        Assert.Equal(CellType.Sand, grid.GetCell(1, 2).Type);
    }

    [Fact]
    public void Update_LiquidCell_DispatchesToLiquidElement()
    {
        var grid = CreateGrid(3, 3);
        grid.SetCell(1, 1, new Cell { Type = CellType.Water });

        new PerCellAlgorithm(seed: 1).Update(grid, new ChunkBounds(0, 0, 3, 3));

        Assert.Equal(CellType.Empty, grid.GetCell(1, 1).Type);
        Assert.Equal(CellType.Water, grid.GetCell(1, 2).Type);
    }

    [Fact]
    public void Update_StaticAndEmptyCells_AreLeftUntouched()
    {
        var grid = CreateGrid(3, 3);
        grid.SetCell(1, 1, new Cell { Type = CellType.Static });

        new PerCellAlgorithm(seed: 1).Update(grid, new ChunkBounds(0, 0, 3, 3));

        Assert.Equal(CellType.Static, grid.GetCell(1, 1).Type);
        Assert.Equal(CellType.Empty, grid.GetCell(1, 2).Type);
    }

    private static CaGrid CreateGrid(int width, int height) => new(width, height, new PerCellAlgorithm(1));
}
