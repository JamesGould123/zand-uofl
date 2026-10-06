namespace Zand.Core.CellularAutomata.Chunking;

// A rectangular region of the CaGrid. Exclusive on the max side, inclusive on the min side.
public readonly struct ChunkBounds
{
    public ChunkBounds(int minX, int minY, int maxX, int maxY)
    {
        MinX = minX;
        MinY = minY;
        MaxX = maxX;
        MaxY = maxY;
    }

    public int MinX { get; }

    public int MinY { get; }

    public int MaxX { get; }

    public int MaxY { get; }
}
