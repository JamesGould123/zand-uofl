namespace Zand.Core.CellularAutomata.Elements;

public static class DirectionalLiquidElement
{
    public static void Update(CaGrid grid, int x, int y, bool[,] flowsRight)
    {
        bool dir = flowsRight[x, y];
        int preferred = dir ? x + 1 : x - 1;
        int other = dir ? x - 1 : x + 1;

        if (TryMove(grid, x, y, x, y + 1, flowsRight, dir))
        {
            return;
        }

        if (TryMove(grid, x, y, preferred, y + 1, flowsRight, dir))
        {
            return;
        }

        if (TryMove(grid, x, y, other, y + 1, flowsRight, !dir))
        {
            return;
        }

        if (grid.LiquidPressureMode == LiquidPressureMode.Enabled)
        {
            return;
        }

        if (TryMove(grid, x, y, preferred, y, flowsRight, dir))
        {
            return;
        }

        TryMove(grid, x, y, other, y, flowsRight, !dir);
    }

    private static bool TryMove(CaGrid grid, int fx, int fy, int tx, int ty, bool[,] flowsRight, bool newDirection)
    {
        if (!grid.InBounds(tx, ty))
        {
            return false;
        }

        if (grid.HasMovedThisTick(fx, fy))
        {
            return false;
        }

        if (grid.GetCell(tx, ty).Type != CellType.Empty)
        {
            return false;
        }

        grid.SwapCells(fx, fy, tx, ty);
        flowsRight[tx, ty] = newDirection;
        return true;
    }
}
