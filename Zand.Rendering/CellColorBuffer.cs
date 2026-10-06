namespace Zand.Rendering;

using System;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Zand.Core.CellularAutomata;

// Gets the color of every visible cell and writes it into a flat array, one slot per cell.
// It only reads the grid and only writes its own portion of the array, so the rows can be split across threads.
// None of it touches the GPU, which is why CellRenderer does the upload and draw itself after.
public static class CellColorBuffer
{
    private const int GridChunkSize = 10;

    private static readonly Color SandColor = new(194, 178, 128);
    private static readonly Color WaterColor = new(64, 164, 223);
    private static readonly Color StaticColor = new(60, 60, 60);
    private static readonly Color OutOfBoundsColor = Color.Red;
    private static readonly Color GridLight = new(180, 180, 180);
    private static readonly Color GridDark = new(150, 150, 150);

    private static readonly (int Dx, int Dy)[] OrthogonalNeighbors =
    {
        (-1, 0), (1, 0), (0, -1), (0, 1)
    };

    // pixels is width * height, row by row. Cell (column, row) shows world cell (cellX0 + column, cellY0 + row).
    public static void Fill(
        Color[] pixels, int width, int height, CaGrid grid, int cellX0, int cellY0,
        CellRenderSmoothing smoothing, RenderParallelism parallelism)
    {
        if (parallelism == RenderParallelism.Sequential)
        {
            FillRows(pixels, width, 0, height, grid, cellX0, cellY0, smoothing);
            return;
        }

        // More bands than cores so a cheap band don't leave idle cores
        // while expensive bands (like those full of water) are still running.
        int bandCount = Math.Min(Environment.ProcessorCount * 2, height);
        Parallel.For(0, bandCount, band =>
        {
            int startRow = band * height / bandCount;
            int endRow = (band + 1) * height / bandCount;
            FillRows(pixels, width, startRow, endRow, grid, cellX0, cellY0, smoothing);
        });
    }

    private static void FillRows(
        Color[] pixels, int width, int startRow, int endRow, CaGrid grid, int cellX0, int cellY0,
        CellRenderSmoothing smoothing)
    {
        for (int row = startRow; row < endRow; row++)
        {
            int worldY = cellY0 + row;
            int rowStart = row * width;
            for (int column = 0; column < width; column++)
            {
                pixels[rowStart + column] = ColorAt(grid, cellX0 + column, worldY, smoothing);
            }
        }
    }

    private static Color ColorAt(CaGrid grid, int worldX, int worldY, CellRenderSmoothing smoothing)
    {
        if (worldX < 0 || worldX >= grid.Width || worldY < 0 || worldY >= grid.Height)
        {
            return OutOfBoundsColor;
        }

        var solidColor = ColorFor(grid.GetCell(worldX, worldY).Type);
        if (solidColor != null)
        {
            return solidColor.Value;
        }

        bool checker = ((worldX / GridChunkSize) + (worldY / GridChunkSize)) % 2 == 0;
        Color checkerColor = checker ? GridLight : GridDark;
        return smoothing switch
        {
            CellRenderSmoothing.NeighborFill => TryNeighborFill(grid, worldX, worldY) ?? checkerColor,
            CellRenderSmoothing.Smoothing => BlendWithNeighbors(grid, worldX, worldY, checkerColor),
            _ => checkerColor
        };
    }

    private static Color? ColorFor(CellType type) => type switch
    {
        CellType.Sand => SandColor,
        CellType.Water => WaterColor,
        CellType.Static => StaticColor,
        _ => null
    };

    // When an empty cell is flanked on both sides by the same CA particle type,
    // paint that cell with the same color. Prevents some visual artifacts resulting from water moving irregularly
    // across chunk borders.
    private static Color? TryNeighborFill(CaGrid grid, int x, int y)
    {
        if (TryMatchingPair(grid, x - 1, y, x + 1, y, out var horizontal))
        {
            return horizontal;
        }

        if (TryMatchingPair(grid, x, y - 1, x, y + 1, out var vertical))
        {
            return vertical;
        }

        return null;
    }

    private static bool TryMatchingPair(CaGrid grid, int ax, int ay, int bx, int by, out Color color)
    {
        color = default;
        if (!grid.InBounds(ax, ay) || !grid.InBounds(bx, by))
        {
            return false;
        }

        var typeA = grid.GetCell(ax, ay).Type;
        if (typeA != grid.GetCell(bx, by).Type)
        {
            return false;
        }

        var matched = ColorFor(typeA);
        if (matched == null)
        {
            return false;
        }

        color = matched.Value;
        return true;
    }

    // Causes empty cells to take on a blend of the colors from their neighboring cells.
    // Prevents some visual artifacts resulting from water moving irregularly
    // across chunk borders.
    private static Color BlendWithNeighbors(CaGrid grid, int x, int y, Color checkerColor)
    {
        float totalFill = 0f;
        float r = 0f;
        float g = 0f;
        float b = 0f;
        int consideredNeighbors = 0;

        foreach (var (dx, dy) in OrthogonalNeighbors)
        {
            int nx = x + dx;
            int ny = y + dy;
            if (!grid.InBounds(nx, ny))
            {
                continue;
            }

            consideredNeighbors++;
            float fill = grid.GetFill(nx, ny);
            if (fill <= 0f)
            {
                continue;
            }

            var neighborColor = ColorFor(grid.GetCell(nx, ny).Type) ?? checkerColor;
            r += neighborColor.R * fill;
            g += neighborColor.G * fill;
            b += neighborColor.B * fill;
            totalFill += fill;
        }

        if (consideredNeighbors == 0 || totalFill <= 0f)
        {
            return checkerColor;
        }

        var blended = new Color((byte)(r / totalFill), (byte)(g / totalFill), (byte)(b / totalFill));
        float alpha = Math.Clamp(totalFill / consideredNeighbors, 0f, 1f);
        return Color.Lerp(checkerColor, blended, alpha);
    }
}
