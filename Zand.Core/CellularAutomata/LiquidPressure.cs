namespace Zand.Core.CellularAutomata;

using Zand.Core.CellularAutomata.Chunking;

// Based on the Liquid Pressure implementation laid out here: Elsts, J. (2009). Simple fluid simulation.
// W-Shadow.com. https://w-shadow.com/blog/2009/09/01/simple-fluid-simulation/
// An attempt to make water act more "intelligently"/follow "real" pressure rules, i.e.
// - equalizing between two spaces (pushing "up" out of a space when pressurized, instead of only ever down or sideways)
// - avoiding oscillation/flickering patterns and "walls", both bugs where water can fail to properly fall down because it keeps flicking left and right
// - ideally, water on the surface of a puddle should settle instead moving side to side rapidly
// In reality, it doesn't accomplish any of this very well yet,
// it mainly makes water more sluggish and costly to simulate in this prototype state,
// so further development time hasn't been allocated here.
public static class LiquidPressure
{
    // In theory a cell will only ever hold 1 unit of water, but max compress allows it to slightly exceed that,
    // allowing water to "push out" from the bottom of a container instead of only spreading sideways.
    private const float MaxMass = 1f;
    private const float MaxCompress = 0.02f;
    private const float MinMass = 0.0001f;
    private const float MaxSpeed = 1f;

    public static void Equalize(CaGrid grid, ChunkBounds bounds)
    {
        int minX = Math.Max(bounds.MinX - 1, 0);
        int minY = Math.Max(bounds.MinY - 1, 0);
        int maxX = Math.Min(bounds.MaxX + 1, grid.Width);
        int maxY = Math.Min(bounds.MaxY + 1, grid.Height);
        int w = maxX - minX;
        int h = maxY - minY;
        if (w <= 0 || h <= 0)
        {
            return;
        }

        var fill = new float[w, h];
        for (int ly = 0; ly < h; ly++)
        {
            for (int lx = 0; lx < w; lx++)
            {
                fill[lx, ly] = grid.GetFill(minX + lx, minY + ly);
            }
        }

        var newFill = (float[,])fill.Clone();

        for (int ly = 0; ly < h; ly++)
        {
            int gy = minY + ly;
            if (gy < bounds.MinY || gy >= bounds.MaxY)
            {
                continue;
            }

            for (int lx = 0; lx < w; lx++)
            {
                int gx = minX + lx;
                if (gx < bounds.MinX || gx >= bounds.MaxX)
                {
                    continue;
                }

                if (!IsPassable(grid, gx, gy) || fill[lx, ly] <= MinMass)
                {
                    continue;
                }

                float mass = fill[lx, ly];

                if (ly + 1 < h && IsPassable(grid, gx, gy + 1))
                {
                    float flow = Math.Clamp(
                        GetStableStateBelow(mass + fill[lx, ly + 1]) - fill[lx, ly + 1], 0f, Math.Min(MaxSpeed, mass));
                    if (flow > MinMass)
                    {
                        newFill[lx, ly] -= flow;
                        newFill[lx, ly + 1] += flow;
                        mass -= flow;
                    }
                }

                mass = SpreadLateral(grid, fill, newFill, lx, ly, lx - 1, minX, gy, mass, w);
                mass = SpreadLateral(grid, fill, newFill, lx, ly, lx + 1, minX, gy, mass, w);

                if (mass > MaxMass && ly > 0 && IsPassable(grid, gx, gy - 1))
                {
                    float flow = Math.Clamp(mass - GetStableStateBelow(mass + fill[lx, ly - 1]), 0f, mass - MaxMass);
                    if (flow > MinMass)
                    {
                        newFill[lx, ly] -= flow;
                        newFill[lx, ly - 1] += flow;
                    }
                }
            }
        }

        for (int ly = 0; ly < h; ly++)
        {
            for (int lx = 0; lx < w; lx++)
            {
                if (MathF.Abs(newFill[lx, ly] - fill[lx, ly]) > MinMass)
                {
                    grid.SetLiquidFill(minX + lx, minY + ly, MathF.Max(newFill[lx, ly], 0f));
                }
            }
        }
    }

    private static float SpreadLateral(
        CaGrid grid, float[,] fill, float[,] newFill, int lx, int ly, int nlx, int minX, int gy, float mass, int w)
    {
        if (mass <= MinMass || nlx < 0 || nlx >= w)
        {
            return mass;
        }

        int ngx = minX + nlx;
        if (!IsPassable(grid, ngx, gy))
        {
            return mass;
        }

        float diff = mass - fill[nlx, ly];
        if (diff <= 0f)
        {
            return mass;
        }

        float flow = Math.Clamp(diff / 4f, 0f, Math.Min(MaxSpeed, mass));
        if (flow <= MinMass)
        {
            return mass;
        }

        newFill[lx, ly] -= flow;
        newFill[nlx, ly] += flow;
        return mass - flow;
    }

    private static bool IsPassable(CaGrid grid, int x, int y)
    {
        if (!grid.InBounds(x, y))
        {
            return false;
        }

        var type = grid.GetCell(x, y).Type;
        return type == CellType.Empty || type == CellType.Water;
    }

    private static float GetStableStateBelow(float combinedMass)
    {
        if (combinedMass <= MaxMass)
        {
            return MaxMass;
        }

        if (combinedMass < (2f * MaxMass) + MaxCompress)
        {
            return ((MaxMass * MaxMass) + (combinedMass * MaxCompress)) / (MaxMass + MaxCompress);
        }

        return (combinedMass + MaxCompress) / 2f;
    }
}
