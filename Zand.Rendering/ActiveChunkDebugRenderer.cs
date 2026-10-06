namespace Zand.Rendering;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Zand.Core.CellularAutomata;
using Zand.Core.CellularAutomata.Chunking;

// Outlines every chunk on screen, highlighting the active chunks (those with data to process / recently processed)
public class ActiveChunkDebugRenderer
{
    private static readonly Color ActiveColor = Color.Cyan;
    private static readonly Color InactiveColor = new Color(80, 80, 80, 60);

    private readonly Texture2D _pixel;
    private readonly int _cellSize;

    public ActiveChunkDebugRenderer(GraphicsDevice graphicsDevice, int cellSize)
    {
        _cellSize = cellSize;
        _pixel = new Texture2D(graphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
    }

    public void Draw(SpriteBatch spriteBatch, CaGrid grid, Camera camera, int chunkSizeCells)
    {
        var chunkGrid = new ChunkGrid(grid.Width, grid.Height, chunkSizeCells);
        float viewMinX = camera.X, viewMinY = camera.Y;
        float viewMaxX = camera.X + camera.WidthCells, viewMaxY = camera.Y + camera.HeightCells;

        foreach (var bounds in chunkGrid.EnumerateChunks())
        {
            if (bounds.MaxX <= viewMinX || bounds.MinX >= viewMaxX ||
                bounds.MaxY <= viewMinY || bounds.MinY >= viewMaxY)
            {
                continue;
            }

            bool active = grid.IsRegionActive(bounds);
            DrawOutline(spriteBatch, camera, bounds, active ? ActiveColor : InactiveColor);
        }
    }

    private void DrawOutline(SpriteBatch spriteBatch, Camera camera, ChunkBounds bounds, Color color)
    {
        int screenX = (int)((bounds.MinX - camera.X) * _cellSize);
        int screenY = (int)((bounds.MinY - camera.Y) * _cellSize);
        int width = (bounds.MaxX - bounds.MinX) * _cellSize;
        int height = (bounds.MaxY - bounds.MinY) * _cellSize;

        spriteBatch.Draw(_pixel, new Rectangle(screenX, screenY, width, 1), color);
        spriteBatch.Draw(_pixel, new Rectangle(screenX, screenY + height - 1, width, 1), color);
        spriteBatch.Draw(_pixel, new Rectangle(screenX, screenY, 1, height), color);
        spriteBatch.Draw(_pixel, new Rectangle(screenX + width - 1, screenY, 1, height), color);
    }
}
