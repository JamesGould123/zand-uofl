// Based on: mellinoe. (n.d.). ImGui.NET.SampleProgram.XNA/DrawVertDeclaration.cs [Source code]. GitHub.
// https://github.com/mellinoe/ImGui.NET/blob/master/src/ImGui.NET.SampleProgram.XNA/DrawVertDeclaration.cs
namespace Zand.App.UI
{
    using ImGuiNET;
    using Microsoft.Xna.Framework.Graphics;

    public static class DrawVertDeclaration
    {
        public static readonly VertexDeclaration Declaration;

        public static readonly int Size;

        static DrawVertDeclaration()
        {
            unsafe
            {
                Size = sizeof(ImDrawVert);
            }

            Declaration = new VertexDeclaration(
                Size,
                new VertexElement(0, VertexElementFormat.Vector2, VertexElementUsage.Position, 0), // Position
                new VertexElement(8, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 0), // UV
                new VertexElement(16, VertexElementFormat.Color, VertexElementUsage.Color, 0)); // Color
        }
    }
}
