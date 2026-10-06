namespace Zand.App.Metrics;

using System.Globalization;
using System.Linq;
using Zand.Core.CellularAutomata;
using Zand.Core.RigidBody;

// Records state of the world when a run ends,
// (hopefully) allows comparison of results between runs of the app.
//
// Only dynamic bodies are listed, since static temp bodies are an internal detail of the coupling.
public static class FinalStateRecorder
{
    public const string Header = "run_id,frames,dynamic_bodies,non_empty_cells,grid_hash,bodies";

    // FNV-1a 64-bit constants
    private const ulong HashOffset = 14695981039346656037UL;
    private const ulong HashPrime = 1099511628211UL;

    public static string Capture(int frames, CaGrid grid, IRigidBodySimulation physics)
    {
        var bodies = physics.GetBodies().Where(b => !b.IsStatic).ToList();
        string bodyList = string.Join(
            ";",
            bodies.Select(b => string.Create(
                CultureInfo.InvariantCulture,
                $"{b.Position.X:F3}:{b.Position.Y:F3}:{b.Angle:F3}")));

        ulong hash = HashOffset;
        int nonEmpty = 0;
        for (int y = 0; y < grid.Height; y++)
        {
            for (int x = 0; x < grid.Width; x++)
            {
                var type = grid.GetCell(x, y).Type;
                if (type != CellType.Empty)
                {
                    nonEmpty++;
                }

                hash ^= (ulong)(int)type;
                hash *= HashPrime;
            }
        }

        return $"{frames},{bodies.Count},{nonEmpty},{hash:X16},\"{bodyList}\"";
    }
}
