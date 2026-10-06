namespace Zand.Core.CellularAutomata.Elements;

public static class RandomizedLiquidElement
{
    // leftFirst defines the default for which diagonal/lateral direction will be tried first when both equally valid
    public static void Update(CaGrid grid, int x, int y, bool leftFirst)
    {
        if (TryMove(grid, x, y, x, y + 1))
        {
            return;
        }

        int firstDiag = leftFirst ? x - 1 : x + 1;
        int secondDiag = leftFirst ? x + 1 : x - 1;
        if (TryMove(grid, x, y, firstDiag, y + 1))
        {
            return;
        }

        if (TryMove(grid, x, y, secondDiag, y + 1))
        {
            return;
        }

        if (grid.LiquidPressureMode == LiquidPressureMode.Enabled)
        {
            return;
        }

        int firstH = leftFirst ? x - 1 : x + 1;
        int secondH = leftFirst ? x + 1 : x - 1;
        if (TryMove(grid, x, y, firstH, y))
        {
            return;
        }

        TryMove(grid, x, y, secondH, y);
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
