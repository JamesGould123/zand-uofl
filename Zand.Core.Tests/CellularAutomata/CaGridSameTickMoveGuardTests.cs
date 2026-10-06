namespace Zand.Core.Tests.CellularAutomata;

using Xunit;
using Zand.Core.CellularAutomata;
using Zand.Core.CellularAutomata.Algorithms;

public class CaGridSameTickMoveGuardTests
{
    [Fact]
    public void HasMovedThisTick_GuardDisabled_AlwaysFalse()
    {
        var grid = CreateGrid(SameTickMoveGuard.Disabled);
        grid.SwapCells(0, 0, 1, 0);

        Assert.False(grid.HasMovedThisTick(1, 0));
    }

    [Fact]
    public void HasMovedThisTick_GuardEnabled_TrueForSwapDestination()
    {
        var grid = CreateGrid(SameTickMoveGuard.Enabled);
        grid.SwapCells(0, 0, 1, 0);

        Assert.True(grid.HasMovedThisTick(1, 0));
    }

    [Fact]
    public void HasMovedThisTick_GuardEnabled_FalseForSwapSource()
    {
        // Only the destination of a swap is stamped - the vacated source is never guard-checked
        // before another element tries to move into it, since it's Empty in every real call site.
        var grid = CreateGrid(SameTickMoveGuard.Enabled);
        grid.SwapCells(0, 0, 1, 0);

        Assert.False(grid.HasMovedThisTick(0, 0));
    }

    [Fact]
    public void HasMovedThisTick_GuardEnabled_FalseForCellNeverSwapped()
    {
        var grid = CreateGrid(SameTickMoveGuard.Enabled);

        Assert.False(grid.HasMovedThisTick(2, 2));
    }

    [Fact]
    public void HasMovedThisTick_GuardEnabled_ClearsAutomaticallyOnNextTick()
    {
        var grid = CreateGrid(SameTickMoveGuard.Enabled);
        grid.SwapCells(0, 0, 1, 0);
        Assert.True(grid.HasMovedThisTick(1, 0));

        grid.Update();

        Assert.False(grid.HasMovedThisTick(1, 0));
    }

    private static CaGrid CreateGrid(SameTickMoveGuard sameTickMoveGuard) =>
        new(3, 3, new PerCellAlgorithm(1), sameTickMoveGuard: sameTickMoveGuard);
}
