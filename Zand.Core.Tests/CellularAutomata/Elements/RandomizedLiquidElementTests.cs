namespace Zand.Core.Tests.CellularAutomata.Elements;

using Xunit;
using Zand.Core.CellularAutomata;
using Zand.Core.CellularAutomata.Algorithms;
using Zand.Core.CellularAutomata.Elements;

public class RandomizedLiquidElementTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Update_EmptyCellBelow_MovesStraightDownRegardlessOfLeftFirst(bool leftFirst)
    {
        var grid = CreateGrid(3, 3);
        grid.SetCell(1, 1, new Cell { Type = CellType.Water });

        RandomizedLiquidElement.Update(grid, 1, 1, leftFirst);

        Assert.Equal(CellType.Empty, grid.GetCell(1, 1).Type);
        Assert.Equal(CellType.Water, grid.GetCell(1, 2).Type);
    }

    [Fact]
    public void Update_LeftFirstTrue_BothDiagonalsAvailable_PrefersDownLeft()
    {
        var grid = CreateGrid(3, 3);
        grid.SetCell(1, 1, new Cell { Type = CellType.Water });
        grid.SetCell(1, 2, new Cell { Type = CellType.Static });

        RandomizedLiquidElement.Update(grid, 1, 1, leftFirst: true);

        Assert.Equal(CellType.Water, grid.GetCell(0, 2).Type);
        Assert.Equal(CellType.Empty, grid.GetCell(2, 2).Type);
    }

    [Fact]
    public void Update_LeftFirstFalse_BothDiagonalsAvailable_PrefersDownRight()
    {
        var grid = CreateGrid(3, 3);
        grid.SetCell(1, 1, new Cell { Type = CellType.Water });
        grid.SetCell(1, 2, new Cell { Type = CellType.Static });

        RandomizedLiquidElement.Update(grid, 1, 1, leftFirst: false);

        Assert.Equal(CellType.Water, grid.GetCell(2, 2).Type);
        Assert.Equal(CellType.Empty, grid.GetCell(0, 2).Type);
    }

    [Fact]
    public void Update_LeftFirstTrue_PreferredDiagonalBlocked_FallsBackToOtherDiagonal()
    {
        var grid = CreateGrid(3, 3);
        grid.SetCell(1, 1, new Cell { Type = CellType.Water });
        grid.SetCell(1, 2, new Cell { Type = CellType.Static });
        grid.SetCell(0, 2, new Cell { Type = CellType.Static });

        RandomizedLiquidElement.Update(grid, 1, 1, leftFirst: true);

        Assert.Equal(CellType.Water, grid.GetCell(2, 2).Type);
    }

    [Fact]
    public void Update_LeftFirstTrue_AllBelowBlockedBothLateralsAvailable_PrefersLeft()
    {
        var grid = CreateGrid(3, 3);
        grid.SetCell(1, 1, new Cell { Type = CellType.Water });
        grid.SetCell(0, 2, new Cell { Type = CellType.Static });
        grid.SetCell(1, 2, new Cell { Type = CellType.Static });
        grid.SetCell(2, 2, new Cell { Type = CellType.Static });

        RandomizedLiquidElement.Update(grid, 1, 1, leftFirst: true);

        Assert.Equal(CellType.Water, grid.GetCell(0, 1).Type);
        Assert.Equal(CellType.Empty, grid.GetCell(2, 1).Type);
    }

    [Fact]
    public void Update_LeftFirstFalse_AllBelowBlockedBothLateralsAvailable_PrefersRight()
    {
        var grid = CreateGrid(3, 3);
        grid.SetCell(1, 1, new Cell { Type = CellType.Water });
        grid.SetCell(0, 2, new Cell { Type = CellType.Static });
        grid.SetCell(1, 2, new Cell { Type = CellType.Static });
        grid.SetCell(2, 2, new Cell { Type = CellType.Static });

        RandomizedLiquidElement.Update(grid, 1, 1, leftFirst: false);

        Assert.Equal(CellType.Water, grid.GetCell(2, 1).Type);
        Assert.Equal(CellType.Empty, grid.GetCell(0, 1).Type);
    }

    [Fact]
    public void Update_LiquidPressureModeEnabled_SkipsLateralFallback()
    {
        var grid = CreateGrid(3, 3, LiquidPressureMode.Enabled);
        grid.SetCell(1, 1, new Cell { Type = CellType.Water });
        grid.SetCell(0, 2, new Cell { Type = CellType.Static });
        grid.SetCell(1, 2, new Cell { Type = CellType.Static });
        grid.SetCell(2, 2, new Cell { Type = CellType.Static });

        RandomizedLiquidElement.Update(grid, 1, 1, leftFirst: true);

        Assert.Equal(CellType.Water, grid.GetCell(1, 1).Type);
    }

    [Fact]
    public void Update_AllNeighborsBlocked_StaysInPlace()
    {
        var grid = CreateGrid(3, 3);
        grid.SetCell(1, 1, new Cell { Type = CellType.Water });
        grid.SetCell(0, 1, new Cell { Type = CellType.Static });
        grid.SetCell(2, 1, new Cell { Type = CellType.Static });
        grid.SetCell(0, 2, new Cell { Type = CellType.Static });
        grid.SetCell(1, 2, new Cell { Type = CellType.Static });
        grid.SetCell(2, 2, new Cell { Type = CellType.Static });

        RandomizedLiquidElement.Update(grid, 1, 1, leftFirst: true);

        Assert.Equal(CellType.Water, grid.GetCell(1, 1).Type);
    }

    private static CaGrid CreateGrid(int width, int height, LiquidPressureMode liquidPressureMode = LiquidPressureMode.Disabled) =>
        new(width, height, new PerCellAlgorithm(1), liquidPressureMode: liquidPressureMode);
}
