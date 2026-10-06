namespace Zand.Core.Tests.CellularAutomata.Elements;

using Xunit;
using Zand.Core.CellularAutomata;
using Zand.Core.CellularAutomata.Algorithms;
using Zand.Core.CellularAutomata.Elements;

public class DirectionalLiquidElementTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Update_EmptyCellBelow_MovesStraightDownAndPreservesDirection(bool dir)
    {
        var grid = CreateGrid(3, 3);
        var flowsRight = new bool[3, 3];
        grid.SetCell(1, 1, new Cell { Type = CellType.Water });
        flowsRight[1, 1] = dir;

        DirectionalLiquidElement.Update(grid, 1, 1, flowsRight);

        Assert.Equal(CellType.Empty, grid.GetCell(1, 1).Type);
        Assert.Equal(CellType.Water, grid.GetCell(1, 2).Type);
        Assert.Equal(dir, flowsRight[1, 2]);
    }

    [Fact]
    public void Update_FlowsRightTrue_BothDiagonalsAvailable_PrefersDownRightAndKeepsDirection()
    {
        var grid = CreateGrid(3, 3);
        var flowsRight = new bool[3, 3];
        grid.SetCell(1, 1, new Cell { Type = CellType.Water });
        grid.SetCell(1, 2, new Cell { Type = CellType.Static });
        flowsRight[1, 1] = true;

        DirectionalLiquidElement.Update(grid, 1, 1, flowsRight);

        Assert.Equal(CellType.Water, grid.GetCell(2, 2).Type);
        Assert.True(flowsRight[2, 2]);
        Assert.Equal(CellType.Empty, grid.GetCell(0, 2).Type);
    }

    [Fact]
    public void Update_FlowsRightFalse_BothDiagonalsAvailable_PrefersDownLeftAndKeepsDirection()
    {
        var grid = CreateGrid(3, 3);
        var flowsRight = new bool[3, 3];
        grid.SetCell(1, 1, new Cell { Type = CellType.Water });
        grid.SetCell(1, 2, new Cell { Type = CellType.Static });
        flowsRight[1, 1] = false;

        DirectionalLiquidElement.Update(grid, 1, 1, flowsRight);

        Assert.Equal(CellType.Water, grid.GetCell(0, 2).Type);
        Assert.False(flowsRight[0, 2]);
        Assert.Equal(CellType.Empty, grid.GetCell(2, 2).Type);
    }

    [Fact]
    public void Update_PreferredDiagonalBlocked_FallsBackToOtherDiagonalAndFlipsDirection()
    {
        var grid = CreateGrid(3, 3);
        var flowsRight = new bool[3, 3];
        grid.SetCell(1, 1, new Cell { Type = CellType.Water });
        grid.SetCell(1, 2, new Cell { Type = CellType.Static });
        grid.SetCell(2, 2, new Cell { Type = CellType.Static });
        flowsRight[1, 1] = true;

        DirectionalLiquidElement.Update(grid, 1, 1, flowsRight);

        Assert.Equal(CellType.Water, grid.GetCell(0, 2).Type);
        Assert.False(flowsRight[0, 2]);
    }

    [Fact]
    public void Update_FlowsRightTrue_PrefersRightLateralMoveAndKeepsDirection()
    {
        var grid = CreateGrid(3, 3);
        var flowsRight = new bool[3, 3];
        grid.SetCell(1, 1, new Cell { Type = CellType.Water });
        grid.SetCell(0, 2, new Cell { Type = CellType.Static });
        grid.SetCell(1, 2, new Cell { Type = CellType.Static });
        grid.SetCell(2, 2, new Cell { Type = CellType.Static });
        flowsRight[1, 1] = true;

        DirectionalLiquidElement.Update(grid, 1, 1, flowsRight);

        Assert.Equal(CellType.Water, grid.GetCell(2, 1).Type);
        Assert.True(flowsRight[2, 1]);
        Assert.Equal(CellType.Empty, grid.GetCell(0, 1).Type);
    }

    [Fact]
    public void Update_FlowsRightFalse_PrefersLeftLateralMoveAndKeepsDirection()
    {
        var grid = CreateGrid(3, 3);
        var flowsRight = new bool[3, 3];
        grid.SetCell(1, 1, new Cell { Type = CellType.Water });
        grid.SetCell(0, 2, new Cell { Type = CellType.Static });
        grid.SetCell(1, 2, new Cell { Type = CellType.Static });
        grid.SetCell(2, 2, new Cell { Type = CellType.Static });
        flowsRight[1, 1] = false;

        DirectionalLiquidElement.Update(grid, 1, 1, flowsRight);

        Assert.Equal(CellType.Water, grid.GetCell(0, 1).Type);
        Assert.False(flowsRight[0, 1]);
        Assert.Equal(CellType.Empty, grid.GetCell(2, 1).Type);
    }

    [Fact]
    public void Update_PreferredLateralDirectionBlocked_FallsBackToOtherSideAndFlipsDirection()
    {
        var grid = CreateGrid(3, 3);
        var flowsRight = new bool[3, 3];
        grid.SetCell(1, 1, new Cell { Type = CellType.Water });
        grid.SetCell(0, 2, new Cell { Type = CellType.Static });
        grid.SetCell(1, 2, new Cell { Type = CellType.Static });
        grid.SetCell(2, 2, new Cell { Type = CellType.Static });
        grid.SetCell(2, 1, new Cell { Type = CellType.Static });
        flowsRight[1, 1] = true;

        DirectionalLiquidElement.Update(grid, 1, 1, flowsRight);

        Assert.Equal(CellType.Water, grid.GetCell(0, 1).Type);
        Assert.False(flowsRight[0, 1]);
    }

    [Fact]
    public void Update_LiquidPressureModeEnabled_SkipsLateralFallback()
    {
        var grid = CreateGrid(3, 3, LiquidPressureMode.Enabled);
        var flowsRight = new bool[3, 3];
        grid.SetCell(1, 1, new Cell { Type = CellType.Water });
        grid.SetCell(0, 2, new Cell { Type = CellType.Static });
        grid.SetCell(1, 2, new Cell { Type = CellType.Static });
        grid.SetCell(2, 2, new Cell { Type = CellType.Static });

        DirectionalLiquidElement.Update(grid, 1, 1, flowsRight);

        Assert.Equal(CellType.Water, grid.GetCell(1, 1).Type);
    }

    [Fact]
    public void Update_AllNeighborsBlocked_StaysInPlace()
    {
        var grid = CreateGrid(3, 3);
        var flowsRight = new bool[3, 3];
        grid.SetCell(1, 1, new Cell { Type = CellType.Water });
        grid.SetCell(0, 1, new Cell { Type = CellType.Static });
        grid.SetCell(2, 1, new Cell { Type = CellType.Static });
        grid.SetCell(0, 2, new Cell { Type = CellType.Static });
        grid.SetCell(1, 2, new Cell { Type = CellType.Static });
        grid.SetCell(2, 2, new Cell { Type = CellType.Static });

        DirectionalLiquidElement.Update(grid, 1, 1, flowsRight);

        Assert.Equal(CellType.Water, grid.GetCell(1, 1).Type);
    }

    private static CaGrid CreateGrid(int width, int height, LiquidPressureMode liquidPressureMode = LiquidPressureMode.Disabled) =>
        new(width, height, new PerCellAlgorithm(1), liquidPressureMode: liquidPressureMode);
}
