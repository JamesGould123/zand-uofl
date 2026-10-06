namespace Zand.Rendering;

using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

public class InfoPanelRenderer
{
    private const int Padding = 6;
    private const int TopRightLabelGap = 12;

    private static readonly Color BackdropColor = Color.Black * 0.55f;
    private static readonly Color HeadingColor = Color.Yellow;
    private static readonly Color LineColor = Color.White;

    private readonly Texture2D _pixel;

    public InfoPanelRenderer(GraphicsDevice graphicsDevice)
    {
        _pixel = new Texture2D(graphicsDevice, 1, 1);
        _pixel.SetData([Color.White]);
    }

    // Returns the drawn box's height in pixels
    public int Draw(
        SpriteBatch spriteBatch,
        SpriteFont font,
        Vector2 position,
        IReadOnlyList<string> lines,
        string? topRightLabel = null)
    {
        if (lines.Count == 0)
        {
            return 0;
        }

        float lineHeight = font.LineSpacing;
        float contentWidth = 0f;
        foreach (var line in lines)
        {
            contentWidth = Math.Max(contentWidth, font.MeasureString(line).X);
        }

        if (topRightLabel != null)
        {
            float headingWithLabelWidth = font.MeasureString(lines[0]).X + TopRightLabelGap + font.MeasureString(topRightLabel).X;
            contentWidth = Math.Max(contentWidth, headingWithLabelWidth);
        }

        int boxWidth = (int)contentWidth + (Padding * 2);
        int boxHeight = (int)(lineHeight * lines.Count) + (Padding * 2);

        spriteBatch.Draw(_pixel, new Rectangle((int)position.X, (int)position.Y, boxWidth, boxHeight), BackdropColor);

        for (int i = 0; i < lines.Count; i++)
        {
            var linePosition = new Vector2(position.X + Padding, position.Y + Padding + (i * lineHeight));
            spriteBatch.DrawString(font, lines[i], linePosition, i == 0 ? HeadingColor : LineColor);
        }

        if (topRightLabel != null)
        {
            float labelWidth = font.MeasureString(topRightLabel).X;
            var labelPosition = new Vector2(position.X + boxWidth - Padding - labelWidth, position.Y + Padding);
            spriteBatch.DrawString(font, topRightLabel, labelPosition, HeadingColor);
        }

        return boxHeight;
    }
}
