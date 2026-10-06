namespace Zand.Core.Tests.CellularAutomata.Chunking;

using Xunit;
using Zand.Core.CellularAutomata.Chunking;

public class ActiveRectangleTrackerTests
{
    // ----- MarkDirty: which chunks a touched cell propagates to -----
    [Fact]
    public void MarkDirty_InteriorCell_OnlyMarksOwningChunk()
    {
        var tracker = new ActiveRectangleTracker(gridWidth: 32, gridHeight: 32, chunkSize: 8, settlingPeriod: 3);

        tracker.MarkDirty(3, 3);

        AssertBounds(3, 3, 4, 4, tracker.GetActiveBounds(0, 0));
        AssertNoBounds(tracker.GetActiveBounds(1, 0));
        AssertNoBounds(tracker.GetActiveBounds(0, 1));
        AssertNoBounds(tracker.GetActiveBounds(1, 1));
    }

    [Fact]
    public void MarkDirty_LeftEdgeCell_AlsoTouchesLeftNeighborChunk()
    {
        var tracker = new ActiveRectangleTracker(gridWidth: 32, gridHeight: 32, chunkSize: 8, settlingPeriod: 3);

        // (8, 3) is the leftmost column of chunk (1, 0)
        tracker.MarkDirty(8, 3);

        AssertBounds(8, 3, 9, 4, tracker.GetActiveBounds(1, 0));
        AssertBounds(7, 3, 8, 4, tracker.GetActiveBounds(0, 0));
    }

    [Fact]
    public void MarkDirty_RightEdgeCell_AlsoTouchesRightNeighborChunk()
    {
        var tracker = new ActiveRectangleTracker(gridWidth: 32, gridHeight: 32, chunkSize: 8, settlingPeriod: 3);

        // (7, 3) is the rightmost column of chunk (0, 0)
        tracker.MarkDirty(7, 3);

        AssertBounds(7, 3, 8, 4, tracker.GetActiveBounds(0, 0));
        AssertBounds(8, 3, 9, 4, tracker.GetActiveBounds(1, 0));
    }

    [Fact]
    public void MarkDirty_TopEdgeCell_AlsoTouchesChunkAbove()
    {
        var tracker = new ActiveRectangleTracker(gridWidth: 32, gridHeight: 32, chunkSize: 8, settlingPeriod: 3);

        // (3, 8) is the topmost row of chunk (0, 1)
        tracker.MarkDirty(3, 8);

        AssertBounds(3, 8, 4, 9, tracker.GetActiveBounds(0, 1));
        AssertBounds(3, 7, 4, 8, tracker.GetActiveBounds(0, 0));
    }

    [Fact]
    public void MarkDirty_BottomEdgeCell_AlsoTouchesChunkBelow()
    {
        var tracker = new ActiveRectangleTracker(gridWidth: 32, gridHeight: 32, chunkSize: 8, settlingPeriod: 3);

        // (3, 7) is the bottommost row of chunk (0, 0)
        tracker.MarkDirty(3, 7);

        AssertBounds(3, 7, 4, 8, tracker.GetActiveBounds(0, 0));
        AssertBounds(3, 8, 4, 9, tracker.GetActiveBounds(0, 1));
    }

    [Fact]
    public void MarkDirty_TopLeftCornerCell_AlsoTouchesDiagonalNeighborChunk()
    {
        var tracker = new ActiveRectangleTracker(gridWidth: 32, gridHeight: 32, chunkSize: 8, settlingPeriod: 3);

        // (8, 8) is the top-left corner of chunk (1, 1): touches left, top, and the diagonal chunk
        tracker.MarkDirty(8, 8);

        AssertBounds(8, 8, 9, 9, tracker.GetActiveBounds(1, 1));
        AssertBounds(7, 8, 8, 9, tracker.GetActiveBounds(0, 1));
        AssertBounds(8, 7, 9, 8, tracker.GetActiveBounds(1, 0));
        AssertBounds(7, 7, 8, 8, tracker.GetActiveBounds(0, 0));
        AssertNoBounds(tracker.GetActiveBounds(2, 1));
        AssertNoBounds(tracker.GetActiveBounds(1, 2));
    }

    [Fact]
    public void MarkDirty_TopRightCornerCell_AlsoTouchesDiagonalNeighborChunk()
    {
        var tracker = new ActiveRectangleTracker(gridWidth: 32, gridHeight: 32, chunkSize: 8, settlingPeriod: 3);

        // (15, 8) is the top-right corner of chunk (1, 1): touches right, top, and the diagonal chunk
        tracker.MarkDirty(15, 8);

        AssertBounds(15, 8, 16, 9, tracker.GetActiveBounds(1, 1));
        AssertBounds(16, 8, 17, 9, tracker.GetActiveBounds(2, 1));
        AssertBounds(15, 7, 16, 8, tracker.GetActiveBounds(1, 0));
        AssertBounds(16, 7, 17, 8, tracker.GetActiveBounds(2, 0));
    }

    [Fact]
    public void MarkDirty_BottomLeftCornerCell_AlsoTouchesDiagonalNeighborChunk()
    {
        var tracker = new ActiveRectangleTracker(gridWidth: 32, gridHeight: 32, chunkSize: 8, settlingPeriod: 3);

        // (8, 15) is the bottom-left corner of chunk (1, 1): touches left, bottom, and the diagonal chunk
        tracker.MarkDirty(8, 15);

        AssertBounds(8, 15, 9, 16, tracker.GetActiveBounds(1, 1));
        AssertBounds(7, 15, 8, 16, tracker.GetActiveBounds(0, 1));
        AssertBounds(8, 16, 9, 17, tracker.GetActiveBounds(1, 2));
        AssertBounds(7, 16, 8, 17, tracker.GetActiveBounds(0, 2));
    }

    [Fact]
    public void MarkDirty_BottomRightCornerCell_AlsoTouchesDiagonalNeighborChunk()
    {
        var tracker = new ActiveRectangleTracker(gridWidth: 32, gridHeight: 32, chunkSize: 8, settlingPeriod: 3);

        // (15, 15) is the bottom-right corner of chunk (1, 1): touches right, bottom, and the diagonal chunk
        tracker.MarkDirty(15, 15);

        AssertBounds(15, 15, 16, 16, tracker.GetActiveBounds(1, 1));
        AssertBounds(16, 15, 17, 16, tracker.GetActiveBounds(2, 1));
        AssertBounds(15, 16, 16, 17, tracker.GetActiveBounds(1, 2));
        AssertBounds(16, 16, 17, 17, tracker.GetActiveBounds(2, 2));
    }

    [Fact]
    public void MarkDirty_CellAtGridCorner_IgnoresOutOfBoundsNeighborChunks()
    {
        var tracker = new ActiveRectangleTracker(gridWidth: 16, gridHeight: 16, chunkSize: 8, settlingPeriod: 3);

        // (0, 0) would touch chunks (-1, 0), (0, -1) and (-1, -1), all outside the grid
        var exception = Record.Exception(() => tracker.MarkDirty(0, 0));

        Assert.Null(exception);
        AssertBounds(0, 0, 1, 1, tracker.GetActiveBounds(0, 0));
        Assert.Single(tracker.TrackedChunks());
    }

    [Fact]
    public void MarkDirty_TwoCellsInSameChunk_UnionsIntoEncompassingBounds()
    {
        var tracker = new ActiveRectangleTracker(gridWidth: 32, gridHeight: 32, chunkSize: 8, settlingPeriod: 3);

        tracker.MarkDirty(1, 1);
        tracker.MarkDirty(5, 5);

        // The union spans the two points, including the untouched cells between them
        AssertBounds(1, 1, 6, 6, tracker.GetActiveBounds(0, 0));
    }

    [Fact]
    public void MarkDirty_CellsInSeparateChunks_TrackedIndependently()
    {
        var tracker = new ActiveRectangleTracker(gridWidth: 32, gridHeight: 32, chunkSize: 8, settlingPeriod: 3);

        tracker.MarkDirty(1, 1);
        tracker.MarkDirty(17, 17);

        AssertBounds(1, 1, 2, 2, tracker.GetActiveBounds(0, 0));
        AssertBounds(17, 17, 18, 18, tracker.GetActiveBounds(2, 2));
        AssertNoBounds(tracker.GetActiveBounds(1, 1));
    }

    // ----- GetActiveBounds -----
    [Fact]
    public void GetActiveBounds_UntrackedChunk_ReturnsNull()
    {
        var tracker = new ActiveRectangleTracker(gridWidth: 32, gridHeight: 32, chunkSize: 8, settlingPeriod: 3);

        AssertNoBounds(tracker.GetActiveBounds(0, 0));
    }

    [Fact]
    public void GetActiveBounds_UnionsCurrentTickWithHistory()
    {
        var tracker = new ActiveRectangleTracker(gridWidth: 32, gridHeight: 32, chunkSize: 8, settlingPeriod: 3);

        tracker.MarkDirty(1, 1);
        tracker.EndTick();
        tracker.MarkDirty(5, 5);

        // The chunk's active area is the union of this tick's writes and any still-remembered
        // writes from recent prior ticks, since a cell going quiet for one tick doesn't mean it settled.
        AssertBounds(1, 1, 6, 6, tracker.GetActiveBounds(0, 0));
    }

    // ----- EndTick lifecycle -----
    [Fact]
    public void EndTick_MovesThisTickIntoHistoryAndKeepsChunkTracked()
    {
        var tracker = new ActiveRectangleTracker(gridWidth: 32, gridHeight: 32, chunkSize: 8, settlingPeriod: 2);

        tracker.MarkDirty(3, 3);
        tracker.EndTick();

        AssertBounds(3, 3, 4, 4, tracker.GetActiveBounds(0, 0));
    }

    [Fact]
    public void EndTick_RemovesChunkOnceHistoryWindowFullyIdle()
    {
        var tracker = new ActiveRectangleTracker(gridWidth: 32, gridHeight: 32, chunkSize: 8, settlingPeriod: 2);

        tracker.MarkDirty(3, 3);

        // With a settling period of 2, the write survives two idle ticks...
        tracker.EndTick();
        tracker.EndTick();
        AssertBounds(3, 3, 4, 4, tracker.GetActiveBounds(0, 0));

        // ...and is only forgotten once a third idle tick has fully cycled the history window.
        tracker.EndTick();
        AssertNoBounds(tracker.GetActiveBounds(0, 0));
        Assert.Empty(tracker.TrackedChunks());
    }

    [Fact]
    public void EndTick_WithNothingTracked_DoesNotThrow()
    {
        var tracker = new ActiveRectangleTracker(gridWidth: 32, gridHeight: 32, chunkSize: 8, settlingPeriod: 3);

        var exception = Record.Exception(tracker.EndTick);

        Assert.Null(exception);
    }

    [Fact]
    public void Constructor_NonPositiveSettlingPeriod_ClampsWindowSizeToOne()
    {
        var tracker = new ActiveRectangleTracker(gridWidth: 32, gridHeight: 32, chunkSize: 8, settlingPeriod: 0);

        tracker.MarkDirty(3, 3);

        // A window size of 1 survives exactly one idle tick before being forgotten on the next.
        tracker.EndTick();
        AssertBounds(3, 3, 4, 4, tracker.GetActiveBounds(0, 0));

        tracker.EndTick();
        AssertNoBounds(tracker.GetActiveBounds(0, 0));
    }

    // ----- IsRegionActive -----
    [Fact]
    public void IsRegionActive_NoTrackedChunks_ReturnsFalse()
    {
        var tracker = new ActiveRectangleTracker(gridWidth: 32, gridHeight: 32, chunkSize: 8, settlingPeriod: 3);

        Assert.False(tracker.IsRegionActive(new ChunkBounds(0, 0, 32, 32)));
    }

    [Fact]
    public void IsRegionActive_OverlapsActiveBounds_ReturnsTrue()
    {
        var tracker = new ActiveRectangleTracker(gridWidth: 32, gridHeight: 32, chunkSize: 8, settlingPeriod: 3);
        tracker.MarkDirty(1, 1);

        Assert.True(tracker.IsRegionActive(new ChunkBounds(0, 0, 8, 8)));
    }

    [Fact]
    public void IsRegionActive_ChunkTrackedButQueryDoesNotOverlapActiveRectangle_ReturnsFalse()
    {
        var tracker = new ActiveRectangleTracker(gridWidth: 32, gridHeight: 32, chunkSize: 8, settlingPeriod: 3);

        // Active rectangle within chunk (0, 0) is (1, 1, 2, 2); this query sits in the same chunk
        // but nowhere near that sub-rectangle.
        tracker.MarkDirty(1, 1);

        Assert.False(tracker.IsRegionActive(new ChunkBounds(5, 5, 8, 8)));
    }

    [Fact]
    public void IsRegionActive_QuerySpansMultipleChunks_TrueIfAnyIsActive()
    {
        var tracker = new ActiveRectangleTracker(gridWidth: 32, gridHeight: 32, chunkSize: 8, settlingPeriod: 3);

        // Only chunk (1, 0) has an active rectangle; chunk (0, 0) does not.
        tracker.MarkDirty(9, 1);

        Assert.True(tracker.IsRegionActive(new ChunkBounds(0, 0, 16, 4)));
    }

    // ----- GetBoundsToProcess -----
    [Fact]
    public void GetBoundsToProcess_NoActiveChunksInRegion_ReturnsNull()
    {
        var tracker = new ActiveRectangleTracker(gridWidth: 32, gridHeight: 32, chunkSize: 8, settlingPeriod: 3);

        var result = tracker.GetBoundsToProcess(new ChunkBounds(0, 0, 8, 8), originX: 0, originY: 0);

        AssertNoBounds(result);
    }

    [Fact]
    public void GetBoundsToProcess_ExpandsActiveBoundsByOneCellMargin()
    {
        var tracker = new ActiveRectangleTracker(gridWidth: 32, gridHeight: 32, chunkSize: 8, settlingPeriod: 3);
        tracker.MarkDirty(4, 4);

        var result = tracker.GetBoundsToProcess(new ChunkBounds(0, 0, 8, 8), originX: 0, originY: 0);

        // Active bounds (4, 4, 5, 5) expanded by a 1-cell margin gives (3, 3, 6, 6),
        // then snapped outward so every edge lands on an even (origin-aligned) coordinate.
        AssertBounds(2, 2, 6, 6, result);
    }

    [Fact]
    public void GetBoundsToProcess_MarginClampedAtProcessingBoundsEdge()
    {
        var tracker = new ActiveRectangleTracker(gridWidth: 32, gridHeight: 32, chunkSize: 8, settlingPeriod: 3);
        tracker.MarkDirty(4, 4);

        // processingBounds is tighter than the natural +1 margin around (4, 4, 5, 5),
        // so the margin can't push past processingBounds.MinX/MinY on the low side.
        var result = tracker.GetBoundsToProcess(new ChunkBounds(4, 4, 8, 8), originX: 0, originY: 0);

        AssertBounds(4, 4, 6, 6, result);
    }

    [Fact]
    public void GetBoundsToProcess_EvenOriginParity_SnapsEdgesOutToEvenAlignment()
    {
        var tracker = new ActiveRectangleTracker(gridWidth: 32, gridHeight: 32, chunkSize: 8, settlingPeriod: 3);
        tracker.MarkDirty(2, 2);
        tracker.MarkDirty(5, 5);

        var result = tracker.GetBoundsToProcess(new ChunkBounds(0, 0, 8, 8), originX: 0, originY: 0);

        // Union (2,2,6,6) expanded by margin gives (1,1,7,7); with an even origin every edge
        // must land on an even coordinate, so it snaps all the way out to the full chunk.
        AssertBounds(0, 0, 8, 8, result);
    }

    [Fact]
    public void GetBoundsToProcess_OddOriginParity_LeavesAlreadyOddAlignedEdgesUnchanged()
    {
        var tracker = new ActiveRectangleTracker(gridWidth: 32, gridHeight: 32, chunkSize: 8, settlingPeriod: 3);
        tracker.MarkDirty(2, 2);
        tracker.MarkDirty(5, 5);

        // Same setup as the even-parity case above, but with an odd origin: (1,1,7,7) is
        // already odd-aligned on every edge, so no snapping is needed this time.
        var result = tracker.GetBoundsToProcess(new ChunkBounds(0, 0, 8, 8), originX: 1, originY: 1);

        AssertBounds(1, 1, 7, 7, result);
    }

    [Fact]
    public void GetBoundsToProcess_ActiveRegionOutsideProcessingBounds_ReturnsNull()
    {
        var tracker = new ActiveRectangleTracker(gridWidth: 8, gridHeight: 8, chunkSize: 8, settlingPeriod: 3);

        // The only active cell is in the far corner of the (single) chunk, but the processing
        // bounds we ask about only cover the opposite corner - chunk-granularity lookup finds
        // the chunk, but its actual active rectangle doesn't overlap the requested region.
        tracker.MarkDirty(7, 7);

        var result = tracker.GetBoundsToProcess(new ChunkBounds(0, 0, 3, 3), originX: 0, originY: 0);

        AssertNoBounds(result);
    }

    [Fact]
    public void GetBoundsToProcess_UnionsMultipleOverlappedChunksBeforeExpanding()
    {
        var tracker = new ActiveRectangleTracker(gridWidth: 32, gridHeight: 32, chunkSize: 8, settlingPeriod: 3);
        tracker.MarkDirty(6, 3);
        tracker.MarkDirty(9, 3);

        var result = tracker.GetBoundsToProcess(new ChunkBounds(0, 0, 16, 8), originX: 0, originY: 0);

        // Chunk (0,0)'s (6,3,7,4) and chunk (1,0)'s (9,3,10,4) are unioned into (6,3,10,4)
        // before the margin and parity snap are applied.
        AssertBounds(4, 2, 12, 6, result);
    }

    // ----- TrackedChunks -----
    [Fact]
    public void TrackedChunks_ReturnsOnlyChunksWithNonNullBounds()
    {
        var tracker = new ActiveRectangleTracker(gridWidth: 32, gridHeight: 32, chunkSize: 8, settlingPeriod: 3);
        tracker.MarkDirty(1, 1);
        tracker.MarkDirty(17, 17);

        var tracked = tracker.TrackedChunks().ToList();

        Assert.Equal(2, tracked.Count);
        Assert.Contains(tracked, t => t is { ChunkX: 0, ChunkY: 0 });
        Assert.Contains(tracked, t => t is { ChunkX: 2, ChunkY: 2 });
    }

    [Fact]
    public void TrackedChunks_NoLongerIncludesChunkAfterHistoryExpires()
    {
        var tracker = new ActiveRectangleTracker(gridWidth: 32, gridHeight: 32, chunkSize: 8, settlingPeriod: 1);
        tracker.MarkDirty(1, 1);

        Assert.Single(tracker.TrackedChunks());

        tracker.EndTick();
        tracker.EndTick();

        Assert.Empty(tracker.TrackedChunks());
    }

    // ----- Concurrency -----
    [Fact]
    public void MarkDirty_CalledConcurrentlyFromMultipleThreads_ProducesConsistentBounds()
    {
        var tracker = new ActiveRectangleTracker(gridWidth: 256, gridHeight: 256, chunkSize: 16, settlingPeriod: 3);

        Parallel.For(0, 16, i => tracker.MarkDirty((i * 16) + 8, (i * 16) + 8));

        for (int i = 0; i < 16; i++)
        {
            AssertBounds((i * 16) + 8, (i * 16) + 8, (i * 16) + 9, (i * 16) + 9, tracker.GetActiveBounds(i, i));
        }

        Assert.Equal(16, tracker.TrackedChunks().Count());
    }

    private static void AssertBounds(int minX, int minY, int maxX, int maxY, ChunkBounds? actual)
    {
        Assert.NotNull(actual);
        Assert.Equal(minX, actual!.Value.MinX);
        Assert.Equal(minY, actual.Value.MinY);
        Assert.Equal(maxX, actual.Value.MaxX);
        Assert.Equal(maxY, actual.Value.MaxY);
    }

    private static void AssertNoBounds(ChunkBounds? actual)
    {
        Assert.Null(actual);
    }
}
