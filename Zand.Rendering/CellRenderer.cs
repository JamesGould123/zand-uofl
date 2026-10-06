namespace Zand.Rendering;

using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Zand.Core.CellularAutomata;

// Draws the visible part of the grid as a single small texture with a pixel per cell, scaled up by the cell size.
// SpriteBatch can only be used from the main thread, so the per-cell color work is done separately
// in CellColorBuffer (which can run on many threads) and only the upload and single draw happen here.
public class CellRenderer
{
    private readonly GraphicsDevice _graphicsDevice;
    private readonly int _cellSize;

    private Texture2D? _texture;
    private Color[] _pixels = [];
    private int _width;
    private int _height;

    public CellRenderer(GraphicsDevice graphicsDevice, int cellSize)
    {
        _graphicsDevice = graphicsDevice;
        _cellSize = cellSize;
    }

    public void Draw(
        SpriteBatch spriteBatch, CaGrid grid, Camera camera,
        CellRenderSmoothing smoothing = CellRenderSmoothing.Disabled,
        RenderParallelism parallelism = RenderParallelism.Sequential)
    {
        int cellX0 = (int)Math.Floor(camera.X);
        int cellY0 = (int)Math.Floor(camera.Y);
        int pixelOffsetX = -(int)((camera.X - cellX0) * _cellSize);
        int pixelOffsetY = -(int)((camera.Y - cellY0) * _cellSize);

        // One extra column and row so the partly scrolled-in cells at the edges of the screen are covered.
        EnsureBuffer(camera.WidthCells + 1, camera.HeightCells + 1);
        CellColorBuffer.Fill(_pixels, _width, _height, grid, cellX0, cellY0, smoothing, parallelism);
        _texture!.SetData(_pixels);

        spriteBatch.Draw(
            _texture,
            new Vector2(pixelOffsetX, pixelOffsetY),
            sourceRectangle: null,
            Color.White,
            rotation: 0f,
            origin: Vector2.Zero,
            scale: (float)_cellSize,
            SpriteEffects.None,
            layerDepth: 0f);
    }

    private void EnsureBuffer(int width, int height)
    {
        if (_texture != null && width == _width && height == _height)
        {
            return;
        }

        _texture?.Dispose();
        _width = width;
        _height = height;
        _pixels = new Color[width * height];
        _texture = new Texture2D(_graphicsDevice, width, height);
    }
}
