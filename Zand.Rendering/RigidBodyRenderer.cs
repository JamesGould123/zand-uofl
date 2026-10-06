namespace Zand.Rendering;

using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Zand.Core.RigidBody;

public class RigidBodyRenderer
{
    private const int CircleSegments = 16;

    private static readonly Color DynamicColor = new Color(220, 100, 80);
    private static readonly Color StaticColor = new Color(100, 180, 100);

    private readonly Texture2D _pixel;
    private readonly float _pixelsPerMeter;
    private readonly int _cellSize;

    public RigidBodyRenderer(GraphicsDevice graphicsDevice, int cellSize, float pixelsPerMeter = 20f)
    {
        _cellSize = cellSize;
        _pixelsPerMeter = pixelsPerMeter;
        _pixel = new Texture2D(graphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
    }

    public void Draw(SpriteBatch spriteBatch, IRigidBodySimulation simulation, Camera camera, int worldHeightCells)
    {
        float originX = -camera.X * _cellSize;
        float originY = (worldHeightCells - camera.Y) * _cellSize;
        float viewportWidth = camera.WidthCells * _cellSize;
        float viewportHeight = camera.HeightCells * _cellSize;

        foreach (var body in simulation.GetBodies())
        {
            float sx = originX + (body.Position.X * _pixelsPerMeter);
            float sy = originY - (body.Position.Y * _pixelsPerMeter);
            float radius = BodyScreenRadius(body);
            if (sx + radius < 0 || sx - radius > viewportWidth || sy + radius < 0 || sy - radius > viewportHeight)
            {
                continue;
            }

            Color color = body.IsStatic ? StaticColor : DynamicColor;

            switch (body.Shape)
            {
                case BoxShape box:
                    spriteBatch.Draw(
                        _pixel,
                        new Vector2(sx, sy),
                        sourceRectangle: null,
                        color,
                        rotation: -body.Angle,
                        origin: new Vector2(0.5f, 0.5f),
                        scale: new Vector2(box.HalfWidth * 2 * _pixelsPerMeter, box.HalfHeight * 2 * _pixelsPerMeter),
                        SpriteEffects.None,
                        layerDepth: 0f);
                    break;

                case PolygonShape polygon:
                    DrawPolygon(spriteBatch, body.Position.X, body.Position.Y, body.Angle,
                        polygon.LocalVertices, originX, originY, color);
                    break;

                case CircleShape circle:
                    DrawCircle(spriteBatch, sx, sy, circle.Radius * _pixelsPerMeter, color);
                    break;
            }
        }
    }

    // Only gives a rough estimate of the radius per shape
    private float BodyScreenRadius(BodyState body) => body.Shape switch
    {
        BoxShape box => MathF.Sqrt((box.HalfWidth * box.HalfWidth) + (box.HalfHeight * box.HalfHeight)) * _pixelsPerMeter,
        CircleShape circle => circle.Radius * _pixelsPerMeter,
        PolygonShape polygon => polygon.LocalVertices.Max(v => v.Length()) * _pixelsPerMeter,
        _ => 0f
    };

    private void DrawPolygon(SpriteBatch spriteBatch, float posX, float posY, float angle,
        System.Numerics.Vector2[] localVerts, float originX, float originY, Color color)
    {
        float cos = MathF.Cos(angle);
        float sin = MathF.Sin(angle);
        int n = localVerts.Length;
        var screenVerts = new Vector2[n];
        for (int i = 0; i < n; i++)
        {
            float lx = localVerts[i].X;
            float ly = localVerts[i].Y;
            float wx = posX + (lx * cos) - (ly * sin);
            float wy = posY + (lx * sin) + (ly * cos);
            screenVerts[i] = new Vector2(originX + (wx * _pixelsPerMeter), originY - (wy * _pixelsPerMeter));
        }

        for (int i = 0; i < n; i++)
        {
            DrawLine(spriteBatch, screenVerts[i], screenVerts[(i + 1) % n], color);
        }
    }

    private void DrawCircle(SpriteBatch spriteBatch, float cx, float cy, float radiusPixels, Color color)
    {
        float step = MathF.PI * 2f / CircleSegments;
        for (int i = 0; i < CircleSegments; i++)
        {
            float a0 = i * step;
            float a1 = (i + 1) * step;
            var from = new Vector2(cx + (MathF.Cos(a0) * radiusPixels), cy + (MathF.Sin(a0) * radiusPixels));
            var to = new Vector2(cx + (MathF.Cos(a1) * radiusPixels), cy + (MathF.Sin(a1) * radiusPixels));
            DrawLine(spriteBatch, from, to, color);
        }
    }

    private void DrawLine(SpriteBatch spriteBatch, Vector2 from, Vector2 to, Color color)
    {
        var diff = to - from;
        float length = diff.Length();
        float angle = MathF.Atan2(diff.Y, diff.X);
        spriteBatch.Draw(
            _pixel,
            from,
            sourceRectangle: null,
            color,
            rotation: angle,
            origin: Vector2.Zero,
            scale: new Vector2(length, 1f),
            SpriteEffects.None,
            layerDepth: 0f);
    }
}
