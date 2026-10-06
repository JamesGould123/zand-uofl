namespace Zand.Core.Tests.CellularAutomata.Elements;

using Xunit;
using Zand.Core.CellularAutomata;
using Zand.Core.CellularAutomata.Algorithms;
using Zand.Core.CellularAutomata.Elements;

public class LiquidElementTests
{
    [Fact]
    public void Update_EmptyCellBelow_MovesStraightDown()
    {
        var grid = CreateGrid(3, 3);
        grid.SetCell(1, 1, new Cell { Type = CellType.Water });

        LiquidElement.Update(grid, 1, 1);

        Assert.Equal(CellType.Empty, grid.GetCell(1, 1).Type);
        Assert.Equal(CellType.Water, grid.GetCell(1, 2).Type);
    }

    [Fact]
    public void Update_StraightDownBlockedEmptyBelowLeft_MovesDownLeft()
    {
        var grid = CreateGrid(3, 3);
        grid.SetCell(1, 1, new Cell { Type = CellType.Water });
        grid.SetCell(1, 2, new Cell { Type = CellType.Static });

        LiquidElement.Update(grid, 1, 1);

        Assert.Equal(CellType.Empty, grid.GetCell(1, 1).Type);
        Assert.Equal(CellType.Water, grid.GetCell(0, 2).Type);
    }

    [Fact]
    public void Update_StraightDownAndBelowLeftBlockedEmptyBelowRight_MovesDownRight()
    {
        var grid = CreateGrid(3, 3);
        grid.SetCell(1, 1, new Cell { Type = CellType.Water });
        grid.SetCell(1, 2, new Cell { Type = CellType.Static });
        grid.SetCell(0, 2, new Cell { Type = CellType.Static });

        LiquidElement.Update(grid, 1, 1);

        Assert.Equal(CellType.Empty, grid.GetCell(1, 1).Type);
        Assert.Equal(CellType.Water, grid.GetCell(2, 2).Type);
    }

    [Fact]
    public void Update_AllBelowNeighborsBlockedEmptyLeft_MovesLeft()
    {
        var grid = CreateGrid(3, 3);
        grid.SetCell(1, 1, new Cell { Type = CellType.Water });
        grid.SetCell(0, 2, new Cell { Type = CellType.Static });
        grid.SetCell(1, 2, new Cell { Type = CellType.Static });
        grid.SetCell(2, 2, new Cell { Type = CellType.Static });

        LiquidElement.Update(grid, 1, 1);

        Assert.Equal(CellType.Empty, grid.GetCell(1, 1).Type);
        Assert.Equal(CellType.Water, grid.GetCell(0, 1).Type);
    }

    [Fact]
    public void Update_AllBelowAndLeftNeighborsBlockedEmptyRight_MovesRight()
    {
        var grid = CreateGrid(3, 3);
        grid.SetCell(1, 1, new Cell { Type = CellType.Water });
        grid.SetCell(0, 2, new Cell { Type = CellType.Static });
        grid.SetCell(1, 2, new Cell { Type = CellType.Static });
        grid.SetCell(2, 2, new Cell { Type = CellType.Static });
        grid.SetCell(0, 1, new Cell { Type = CellType.Static });

        LiquidElement.Update(grid, 1, 1);

        Assert.Equal(CellType.Empty, grid.GetCell(1, 1).Type);
        Assert.Equal(CellType.Water, grid.GetCell(2, 1).Type);
    }

    [Fact]
    public void Update_NonEmptyCellBelow_CannotDisplaceItEvenIfLowerDensity()
    {
        var grid = CreateGrid(3, 3);
        grid.SetCell(1, 1, new Cell { Type = CellType.Water });
        grid.SetCell(1, 2, new Cell { Type = CellType.Water });
        grid.SetCell(0, 2, new Cell { Type = CellType.Static });
        grid.SetCell(2, 2, new Cell { Type = CellType.Static });
        grid.SetCell(0, 1, new Cell { Type = CellType.Static });
        grid.SetCell(2, 1, new Cell { Type = CellType.Static });

        LiquidElement.Update(grid, 1, 1);

        Assert.Equal(CellType.Water, grid.GetCell(1, 1).Type);
        Assert.Equal(CellType.Water, grid.GetCell(1, 2).Type);
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

        LiquidElement.Update(grid, 1, 1);

        Assert.Equal(CellType.Water, grid.GetCell(1, 1).Type);
    }

    [Fact]
    public void Update_LiquidPressureModeEnabled_NeverFallsBackToLateralMovement()
    {
        // With pressure mode on, lateral spreading is done in LiquidPressure.Equalize
        var grid = CreateGrid(3, 3, LiquidPressureMode.Enabled);
        grid.SetCell(1, 1, new Cell { Type = CellType.Water });
        grid.SetCell(0, 2, new Cell { Type = CellType.Static });
        grid.SetCell(1, 2, new Cell { Type = CellType.Static });
        grid.SetCell(2, 2, new Cell { Type = CellType.Static });

        LiquidElement.Update(grid, 1, 1);

        Assert.Equal(CellType.Water, grid.GetCell(1, 1).Type);
    }

    [Fact]
    public void Update_SameTickMoveGuardDisabled_ReEvaluatingNewPositionUndoesTheMove()
    {
        var grid = CreateGrid(4, 3);
        grid.SetCell(1, 1, new Cell { Type = CellType.Water });
        FillRow(grid, y: 2, width: 4); // floor - nothing can fall through
        grid.SetCell(0, 1, new Cell { Type = CellType.Static }); // left wall

        LiquidElement.Update(grid, 1, 1);
        Assert.Equal(CellType.Water, grid.GetCell(2, 1).Type);

        LiquidElement.Update(grid, 2, 1);

        Assert.Equal(CellType.Water, grid.GetCell(1, 1).Type);
        Assert.Equal(CellType.Empty, grid.GetCell(2, 1).Type);
    }

    [Fact]
    public void Update_SameTickMoveGuardEnabled_ReEvaluatingNewPositionDoesNotMoveAgain()
    {
        var grid = CreateGrid(4, 3, sameTickMoveGuard: SameTickMoveGuard.Enabled);
        grid.SetCell(1, 1, new Cell { Type = CellType.Water });
        FillRow(grid, y: 2, width: 4);
        grid.SetCell(0, 1, new Cell { Type = CellType.Static });

        LiquidElement.Update(grid, 1, 1);
        Assert.Equal(CellType.Water, grid.GetCell(2, 1).Type);

        LiquidElement.Update(grid, 2, 1);

        Assert.Equal(CellType.Water, grid.GetCell(2, 1).Type);
        Assert.Equal(CellType.Empty, grid.GetCell(1, 1).Type);
    }

    private static void FillRow(CaGrid grid, int y, int width)
    {
        for (int x = 0; x < width; x++)
        {
            grid.SetCell(x, y, new Cell { Type = CellType.Static });
        }
    }

    private static CaGrid CreateGrid(
        int width, int height,
        LiquidPressureMode liquidPressureMode = LiquidPressureMode.Disabled,
        SameTickMoveGuard sameTickMoveGuard = SameTickMoveGuard.Disabled) =>
        new(width, height, new PerCellAlgorithm(1), liquidPressureMode: liquidPressureMode, sameTickMoveGuard: sameTickMoveGuard);
}
