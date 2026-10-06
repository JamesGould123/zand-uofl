namespace Zand.Core.Tests.CellularAutomata.Elements;

using Xunit;
using Zand.Core.CellularAutomata;
using Zand.Core.CellularAutomata.Algorithms;
using Zand.Core.CellularAutomata.Elements;

public class PowderElementTests
{
    [Fact]
    public void Update_EmptyCellBelow_MovesStraightDown()
    {
        var grid = CreateGrid(3, 3);
        grid.SetCell(1, 1, new Cell { Type = CellType.Sand });

        PowderElement.Update(grid, 1, 1);

        Assert.Equal(CellType.Empty, grid.GetCell(1, 1).Type);
        Assert.Equal(CellType.Sand, grid.GetCell(1, 2).Type);
    }

    [Fact]
    public void Update_StraightDownBlockedEmptyBelowLeft_MovesDownLeft()
    {
        var grid = CreateGrid(3, 3);
        grid.SetCell(1, 1, new Cell { Type = CellType.Sand });
        grid.SetCell(1, 2, new Cell { Type = CellType.Static });

        PowderElement.Update(grid, 1, 1);

        Assert.Equal(CellType.Empty, grid.GetCell(1, 1).Type);
        Assert.Equal(CellType.Sand, grid.GetCell(0, 2).Type);
    }

    [Fact]
    public void Update_StraightDownAndBelowLeftBlockedEmptyBelowRight_MovesDownRight()
    {
        var grid = CreateGrid(3, 3);
        grid.SetCell(1, 1, new Cell { Type = CellType.Sand });
        grid.SetCell(1, 2, new Cell { Type = CellType.Static });
        grid.SetCell(0, 2, new Cell { Type = CellType.Static });

        PowderElement.Update(grid, 1, 1);

        Assert.Equal(CellType.Empty, grid.GetCell(1, 1).Type);
        Assert.Equal(CellType.Sand, grid.GetCell(2, 2).Type);
    }

    [Fact]
    public void Update_AllBelowNeighborsBlockedByEqualOrHigherDensity_StaysInPlace()
    {
        var grid = CreateGrid(3, 3);
        grid.SetCell(1, 1, new Cell { Type = CellType.Sand });
        grid.SetCell(0, 2, new Cell { Type = CellType.Static });
        grid.SetCell(1, 2, new Cell { Type = CellType.Sand });
        grid.SetCell(2, 2, new Cell { Type = CellType.Static });

        PowderElement.Update(grid, 1, 1);

        Assert.Equal(CellType.Sand, grid.GetCell(1, 1).Type);
    }

    [Fact]
    public void Update_LowerDensityCellBelow_SwapsPlaces()
    {
        var grid = CreateGrid(3, 3);
        grid.SetCell(1, 1, new Cell { Type = CellType.Sand });
        grid.SetCell(1, 2, new Cell { Type = CellType.Water });

        PowderElement.Update(grid, 1, 1);

        Assert.Equal(CellType.Water, grid.GetCell(1, 1).Type);
        Assert.Equal(CellType.Sand, grid.GetCell(1, 2).Type);
    }

    [Fact]
    public void Update_AtBottomRowOfGrid_DoesNotMoveAndDoesNotThrow()
    {
        var grid = CreateGrid(3, 1);
        grid.SetCell(1, 0, new Cell { Type = CellType.Sand });

        var exception = Record.Exception(() => PowderElement.Update(grid, 1, 0));

        Assert.Null(exception);
        Assert.Equal(CellType.Sand, grid.GetCell(1, 0).Type);
    }

    private static CaGrid CreateGrid(int width, int height) => new(width, height, new PerCellAlgorithm(1));
}
