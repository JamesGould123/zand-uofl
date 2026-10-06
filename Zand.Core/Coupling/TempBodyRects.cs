namespace Zand.Core.Coupling;

using Zand.Core.CellularAutomata;

// Rectangle of CA cells (X/Y are top-left)
public readonly record struct CellRect(int X, int Y, int Width, int Height);

// Groups solid cells that need a temp static body into rectangles, per TempBodyMerging.
//
// Merged rectangles go until the end of the row of the sand in the grid,
// not limited to the radius directly around a rigid body.
// This should ideally prevent situations where the temp bodies
// have to be remade on every slight rigid body movement.
public static class TempBodyRects
{
    public const int MaxCells = 64;

    public static List<CellRect> Build(CaGrid grid, IEnumerable<(int X, int Y)> cells, TempBodyMerging mode)
    {
        var rects = new List<CellRect>();

        // Only sand is merged.
        // Other candidates (e.g. liquid cells picked at random during ProbabilisticCollision) stay one body per cell,
        // since merging them would also include neighbouring cells that should not have temp bodies.
        var sandCells = new List<(int X, int Y)>();
        foreach (var (x, y) in cells)
        {
            if (mode == TempBodyMerging.NoMerging || !IsSand(grid, x, y))
            {
                rects.Add(new CellRect(x, y, 1, 1));
            }
            else
            {
                sandCells.Add((x, y));
            }
        }

        if (sandCells.Count == 0)
        {
            return rects;
        }

        // Cells already inside a rectangle built for an earlier cell, by row, so each rectangle is built once.
        var covered = new Dictionary<int, List<CellRect>>();
        foreach (var (x, y) in sandCells.OrderBy(c => c.Y).ThenBy(c => c.X))
        {
            if (IsCovered(covered, x, y))
            {
                continue;
            }

            var (start, width) = RowRun(grid, x, y);
            var rect = mode == TempBodyMerging.HorizontalMerging
                ? new CellRect(start, y, width, 1)
                : ExtendVertically(grid, start, width, y);

            rects.Add(rect);
            for (int row = rect.Y; row < rect.Y + rect.Height; row++)
            {
                if (!covered.TryGetValue(row, out var inRow))
                {
                    inRow = [];
                    covered[row] = inRow;
                }

                inRow.Add(rect);
            }
        }

        return rects;
    }

    private static bool IsSand(CaGrid grid, int x, int y) =>
        grid.InBounds(x, y) && grid.GetCell(x, y).Type == CellType.Sand;

    // checks if the cell is already in any of the rectangles
    private static bool IsCovered(Dictionary<int, List<CellRect>> covered, int x, int y)
    {
        if (!covered.TryGetValue(y, out var inRow))
        {
            return false;
        }

        return inRow.Any(rect => x >= rect.X && x < rect.X + rect.Width);
    }

    // gets the horizontal rectangle the cell would be included in
    private static (int Start, int Width) RowRun(CaGrid grid, int x, int y)
    {
        int left = x;
        while (IsSand(grid, left - 1, y))
        {
            left--;
        }

        int right = x;
        while (IsSand(grid, right + 1, y))
        {
            right++;
        }

        int start = left + (((x - left) / MaxCells) * MaxCells);
        return (start, Math.Min(MaxCells, right - start + 1));
    }

    // Gets a rectangle from a horizontal row.
    private static CellRect ExtendVertically(CaGrid grid, int start, int width, int y)
    {
        int top = y;
        while (IsSand(grid, start, top - 1) && RowRun(grid, start, top - 1) == (start, width))
        {
            top--;
        }

        int bottom = y;
        while (IsSand(grid, start, bottom + 1) && RowRun(grid, start, bottom + 1) == (start, width))
        {
            bottom++;
        }

        int pieceTop = top + (((y - top) / MaxCells) * MaxCells);
        int pieceBottom = Math.Min(bottom, pieceTop + MaxCells - 1);
        return new CellRect(start, pieceTop, width, pieceBottom - pieceTop + 1);
    }
}
