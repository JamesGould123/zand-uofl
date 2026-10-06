namespace Zand.Rendering;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Zand.Core.CellularAutomata;
using Zand.Core.CellularAutomata.Chunking;

// Outlines every chunk's "active rectangle" on screen, highlighting the active portion of each active chunk
// An "Active Rectangle" is the portion of the chunk that has data to process or was recently processed
public class ActiveRectangleDebugRenderer
{
    private static readonly Color RectangleColor = Color.Yellow;

    private readonly Texture2D _pixel;
    private readonly int _cellSize;

    public ActiveRectangleDebugRenderer(GraphicsDevice graphicsDevice, int cellSize)
    {
        _cellSize = cellSize;
        _pixel = new Texture2D(graphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
    }

    public void Draw(SpriteBatch spriteBatch, CaGrid grid, Camera camera)
    {
        float viewMinX = camera.X, viewMinY = camera.Y;
        float viewMaxX = camera.X + camera.WidthCells, viewMaxY = camera.Y + camera.HeightCells;

        foreach (var bounds in grid.ActiveRectangles())
        {
            if (bounds.MaxX <= viewMinX || bounds.MinX >= viewMaxX ||
                bounds.MaxY <= viewMinY || bounds.MinY >= viewMaxY)
            {
                continue;
            }

            DrawOutline(spriteBatch, camera, bounds, RectangleColor);
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
