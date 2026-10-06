namespace Zand.Core.CellularAutomata.Elements;

public static class LiquidElement
{
    public static void Update(CaGrid grid, int x, int y)
    {
        if (TryMove(grid, x, y, x, y + 1))
        {
            return;
        }

        if (TryMove(grid, x, y, x - 1, y + 1))
        {
            return;
        }

        if (TryMove(grid, x, y, x + 1, y + 1))
        {
            return;
        }

        if (grid.LiquidPressureMode == LiquidPressureMode.Enabled)
        {
            return;
        }

        if (TryMove(grid, x, y, x - 1, y))
        {
            return;
        }

        TryMove(grid, x, y, x + 1, y);
    }

    private static bool TryMove(CaGrid grid, int fx, int fy, int tx, int ty)
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
        return true;
    }
}
