namespace Zand.Core.CellularAutomata.Elements;

public static class PowderElement
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

        TryMove(grid, x, y, x + 1, y + 1);
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

        var to = grid.GetCell(tx, ty);
        if (ElementProperties.GetDensity(to.Type) >= ElementProperties.GetDensity(CellType.Sand))
        {
            return false;
        }

        grid.SwapCells(fx, fy, tx, ty);
        return true;
    }
}
