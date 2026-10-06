namespace Zand.Core.Tests.CellularAutomata.Chunking;

using Xunit;
using Zand.Core.CellularAutomata.Chunking;

public class ChunkGridTests
{
    [Fact]
    public void EnumerateChunks_ExtentDividesEvenly_ProducesUniformlySizedChunks()
    {
        var grid = new ChunkGrid(gridWidth: 16, gridHeight: 16, chunkSize: 8);

        var chunks = grid.EnumerateChunks().ToList();

        Assert.Equal(4, chunks.Count);
        AssertContains(0, 0, 8, 8, chunks);
        AssertContains(8, 0, 16, 8, chunks);
        AssertContains(0, 8, 8, 16, chunks);
        AssertContains(8, 8, 16, 16, chunks);
    }

    [Fact]
    public void EnumerateChunks_ExtentNotDivisibleByChunkSize_ProducesUndersizedStubChunkAtEnd()
    {
        var grid = new ChunkGrid(gridWidth: 20, gridHeight: 8, chunkSize: 8);

        var chunks = grid.EnumerateChunks().ToList();

        Assert.Equal(3, chunks.Count);
        AssertContains(0, 0, 8, 8, chunks);
        AssertContains(8, 0, 16, 8, chunks);
        AssertContains(16, 0, 20, 8, chunks);
    }

    [Fact]
    public void EnumerateChunks_NonZeroOrigin_ProducesLeadingStubChunkOfOriginWidth()
    {
        var grid = new ChunkGrid(gridWidth: 19, gridHeight: 8, chunkSize: 8, originX: 3);

        var chunks = grid.EnumerateChunks().ToList();

        // The origin cuts a short leading chunk before the regular chunk-sized cadence resumes.
        Assert.Equal(3, chunks.Count);
        AssertContains(0, 0, 3, 8, chunks);
        AssertContains(3, 0, 11, 8, chunks);
        AssertContains(11, 0, 19, 8, chunks);
    }

    [Fact]
    public void EnumerateChunks_ChunkSizeLargerThanExtent_ProducesSingleStubCoveringWholeExtent()
    {
        var grid = new ChunkGrid(gridWidth: 5, gridHeight: 5, chunkSize: 8);

        var chunks = grid.EnumerateChunks().ToList();

        Assert.Equal(new ChunkBounds(0, 0, 5, 5), chunks.Single());
    }

    [Fact]
    public void EnumerateRowBands_OrdersBandsBottomToTop()
    {
        var grid = new ChunkGrid(gridWidth: 8, gridHeight: 24, chunkSize: 8);

        var bands = grid.EnumerateRowBands().ToList();

        Assert.Equal(3, bands.Count);
        Assert.Equal(new ChunkBounds(0, 16, 8, 24), bands[0].Single().Bounds);
        Assert.Equal(new ChunkBounds(0, 8, 8, 16), bands[1].Single().Bounds);
        Assert.Equal(new ChunkBounds(0, 0, 8, 8), bands[2].Single().Bounds);
    }

    [Fact]
    public void EnumerateRowBands_AssignsChunkXInLeftToRightOrderWithinEachBand()
    {
        var grid = new ChunkGrid(gridWidth: 24, gridHeight: 8, chunkSize: 8);

        var band = grid.EnumerateRowBands().Single();

        Assert.Equal(3, band.Count);
        Assert.True(band[0] is { ChunkX: 0, Bounds.MinX: 0, Bounds.MaxX: 8 });
        Assert.True(band[1] is { ChunkX: 1, Bounds.MinX: 8, Bounds.MaxX: 16 });
        Assert.True(band[2] is { ChunkX: 2, Bounds.MinX: 16, Bounds.MaxX: 24 });
    }

    [Fact]
    public void EnumerateChunks_FlattensRowBandsInBottomToTopThenLeftToRightOrder()
    {
        var grid = new ChunkGrid(gridWidth: 16, gridHeight: 16, chunkSize: 8);

        var chunks = grid.EnumerateChunks().ToList();

        Assert.Equal(
        [
            new ChunkBounds(0, 8, 8, 16),
            new ChunkBounds(8, 8, 16, 16),
            new ChunkBounds(0, 0, 8, 8),
            new ChunkBounds(8, 0, 16, 8)
        ], chunks);
    }

    [Fact]
    public void EnumerateChunks_NonSquareGrid_ProducesOneChunkPerAxisCombination()
    {
        var grid = new ChunkGrid(gridWidth: 24, gridHeight: 16, chunkSize: 8);

        var chunks = grid.EnumerateChunks().ToList();

        Assert.Equal(6, chunks.Count);
        AssertContains(0, 0, 8, 8, chunks);
        AssertContains(16, 8, 24, 16, chunks);
    }

    [Theory]
    [InlineData(16, 16, 8, 0, 0)]
    [InlineData(20, 13, 8, 0, 0)]
    [InlineData(19, 8, 8, 3, 0)]
    [InlineData(17, 17, 5, 2, 4)]
    [InlineData(5, 5, 8, 0, 0)]
    public void EnumerateChunks_AlwaysTilesGridExactlyWithNoGapsOrOverlaps(
        int gridWidth, int gridHeight, int chunkSize, int originX, int originY)
    {
        var grid = new ChunkGrid(gridWidth, gridHeight, chunkSize, originX, originY);

        var chunks = grid.EnumerateChunks().ToList();

        Assert.All(chunks, c => Assert.True(c.MinX >= 0 && c.MinY >= 0 && c.MaxX <= gridWidth && c.MaxY <= gridHeight));

        long totalArea = chunks.Sum(c => (long)(c.MaxX - c.MinX) * (c.MaxY - c.MinY));
        Assert.Equal((long)gridWidth * gridHeight, totalArea);

        for (int i = 0; i < chunks.Count; i++)
        {
            for (int j = i + 1; j < chunks.Count; j++)
            {
                Assert.False(Overlaps(chunks[i], chunks[j]));
            }
        }
    }

    private static bool Overlaps(ChunkBounds a, ChunkBounds b) =>
        a.MinX < b.MaxX && b.MinX < a.MaxX && a.MinY < b.MaxY && b.MinY < a.MaxY;

    private static void AssertContains(int minX, int minY, int maxX, int maxY, List<ChunkBounds> chunks)
    {
        Assert.Contains(chunks, c => c.MinX == minX && c.MinY == minY && c.MaxX == maxX && c.MaxY == maxY);
    }
}
