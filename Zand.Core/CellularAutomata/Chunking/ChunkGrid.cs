namespace Zand.Core.CellularAutomata.Chunking;

// Chunking: A divide-and-conquer process useful for reducing processing time by splitting the "world"
// of the full CA grid into square chunks.
// Optionally originates the chunks from a non-zero origin to support Margolus's phase-shifted 2x2 block lattice
// (see MargolusAlgorithm.ChunkOrigin).
// Can create undersized rectangular "stub" chunks on the edges if the full world/offset doesn't allow for only fully-sized chunks.
public readonly struct ChunkGrid
{
    private readonly int _gridWidth;
    private readonly int _gridHeight;
    private readonly int _chunkSize;
    private readonly int _originX;
    private readonly int _originY;

    public ChunkGrid(int gridWidth, int gridHeight, int chunkSize, int originX = 0, int originY = 0)
    {
        _gridWidth = gridWidth;
        _gridHeight = gridHeight;
        _chunkSize = chunkSize;
        _originX = originX;
        _originY = originY;
    }

    // Chunks are visited bottom to top in row-bands to mitigate the risk of cells being processed multiple times
    // (as most cells will be falling downwards due to gravity).
    // This may cause extra floatiness with particles that float upwards depending on implementation.
    public IEnumerable<ChunkBounds> EnumerateChunks() =>
        EnumerateRowBands().SelectMany(band => band.Select(c => c.Bounds));

    public IEnumerable<IReadOnlyList<(int ChunkX, ChunkBounds Bounds)>> EnumerateRowBands()
    {
        var yRanges = EnumerateAxis(_originY, _gridHeight).ToList();
        var xRanges = EnumerateAxis(_originX, _gridWidth).ToList();

        for (int iy = yRanges.Count - 1; iy >= 0; iy--)
        {
            var (y0, y1) = yRanges[iy];
            var band = new List<(int ChunkX, ChunkBounds Bounds)>(xRanges.Count);
            for (int ix = 0; ix < xRanges.Count; ix++)
            {
                var (x0, x1) = xRanges[ix];
                band.Add((ix, new ChunkBounds(x0, y0, x1, y1)));
            }

            yield return band;
        }
    }

    private IEnumerable<(int Start, int End)> EnumerateAxis(int origin, int extent)
    {
        if (origin > 0)
        {
            yield return (0, origin);
        }

        int cursor = origin;
        while (cursor < extent)
        {
            int end = Math.Min(cursor + _chunkSize, extent);
            yield return (cursor, end);
            cursor = end;
        }
    }
}
