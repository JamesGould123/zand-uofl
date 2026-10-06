namespace Zand.Rendering;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Zand.Core.Ballistics;
using Zand.Core.CellularAutomata;

// This renderer is specifically for airborne particles in PointSwarm mode.
// Renders temporary bodies created when rigid bodies collide with CA particles,
// converting the CA particles temporarily into ballistic bodies.
public class BallisticParticleRenderer
{
    private static readonly Color SandColor = new(194, 178, 128);
    private static readonly Color WaterColor = new(64, 164, 223);

    private readonly Texture2D _pixel;
    private readonly float _pixelsPerMeter;
    private readonly int _particleSizePixels;

    public BallisticParticleRenderer(GraphicsDevice graphicsDevice, int cellSize, float pixelsPerMeter = 20f)
    {
        _particleSizePixels = cellSize;
        _pixelsPerMeter = pixelsPerMeter;
        _pixel = new Texture2D(graphicsDevice, 1, 1);
        _pixel.SetData([Color.White]);
    }

    public void Draw(SpriteBatch spriteBatch, IReadOnlyList<AirborneParticle> particles, Camera camera, int worldHeightCells)
    {
        float originX = -camera.X * _particleSizePixels;
        float originY = (worldHeightCells - camera.Y) * _particleSizePixels;
        float viewportWidth = camera.WidthCells * _particleSizePixels;
        float viewportHeight = camera.HeightCells * _particleSizePixels;
        float halfSize = _particleSizePixels / 2f;

        foreach (var particle in particles)
        {
            float sx = originX + (particle.Position.X * _pixelsPerMeter);
            float sy = originY - (particle.Position.Y * _pixelsPerMeter);
            if (sx + halfSize < 0 || sx - halfSize > viewportWidth || sy + halfSize < 0 || sy - halfSize > viewportHeight)
            {
                continue;
            }

            var color = particle.CellType == CellType.Water ? WaterColor : SandColor;
            spriteBatch.Draw(
                _pixel,
                new Rectangle(
                    (int)(sx - (_particleSizePixels / 2f)), (int)(sy - (_particleSizePixels / 2f)),
                    _particleSizePixels, _particleSizePixels),
                color);
        }
    }
}
