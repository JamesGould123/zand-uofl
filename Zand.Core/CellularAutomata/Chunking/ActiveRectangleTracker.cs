namespace Zand.Core.CellularAutomata.Chunking;

using System.Collections.Concurrent;

// Based on the system described in: Purho, P. (2019). Exploring the tech and design of Noita [Conference talk].
// Game Developers Conference. https://www.youtube.com/watch?v=prXuyMCgbTc

// An optimization in which an active part (an "active rectangles") of each active chunk is tracked,
// and updates are only calculated for that portion of the chunk.
// Unlike chunking itself, this should have no effect on the behavior of the simulation.
public class ActiveRectangleTracker
{
    private readonly int _chunkSize;
    private readonly int _chunkCountX;
    private readonly int _chunkCountY;
    private readonly int _windowSize;

    private readonly ConcurrentDictionary<(int Cx, int Cy), ChunkWindow> _chunks = new();

    public ActiveRectangleTracker(int gridWidth, int gridHeight, int chunkSize, int settlingPeriod)
    {
        _chunkSize = chunkSize;
        _chunkCountX = (gridWidth + chunkSize - 1) / chunkSize;
        _chunkCountY = (gridHeight + chunkSize - 1) / chunkSize;
        _windowSize = Math.Max(1, settlingPeriod);
    }

    // Expands the owning chunk's active rectangle to include the given cell,
    // and if the cell sits on the edge of a chunk,
    // expands the adjacent chunk's active rectangle to include the adjacent cell
    public void MarkDirty(int cellX, int cellY)
    {
        int cx = cellX / _chunkSize;
        int cy = cellY / _chunkSize;
        int localX = cellX - (cx * _chunkSize);
        int localY = cellY - (cy * _chunkSize);

        bool touchesLeft = localX == 0;
        bool touchesRight = localX == _chunkSize - 1;
        bool touchesTop = localY == 0;
        bool touchesBottom = localY == _chunkSize - 1;

        Touch(cx, cy, cellX, cellY);
        if (touchesLeft)
        {
            Touch(cx - 1, cy, cellX - 1, cellY);
        }

        if (touchesRight)
        {
            Touch(cx + 1, cy, cellX + 1, cellY);
        }

        if (touchesTop)
        {
            Touch(cx, cy - 1, cellX, cellY - 1);
        }

        if (touchesBottom)
        {
            Touch(cx, cy + 1, cellX, cellY + 1);
        }

        if (touchesLeft && touchesTop)
        {
            Touch(cx - 1, cy - 1, cellX - 1, cellY - 1);
        }

        if (touchesLeft && touchesBottom)
        {
            Touch(cx - 1, cy + 1, cellX - 1, cellY + 1);
        }

        if (touchesRight && touchesTop)
        {
            Touch(cx + 1, cy - 1, cellX + 1, cellY - 1);
        }

        if (touchesRight && touchesBottom)
        {
            Touch(cx + 1, cy + 1, cellX + 1, cellY + 1);
        }
    }

    // Gets the bounds of the active rectangle for a given chunk.
    // Takes into account the write history in the chunk,
    // as a cell being dormant for a single cycle doesn't guarantee long-term dormancy
    public ChunkBounds? GetActiveBounds(int chunkX, int chunkY)
    {
        if (!_chunks.TryGetValue((chunkX, chunkY), out var window))
        {
            return null;
        }

        ChunkBounds? union = window.ThisTick;
        foreach (var entry in window.History)
        {
            union = Union(union, entry);
        }

        return union;
    }

    // Given a section of the grid and an origin from which that section is calculated,
    // gets all chunks in the sections, unions their active rectangles into a larger bounds
    // then accounts for Margolus-style offsets in the origin
    // (making sure the processable bounds contain full processable units).
    public ChunkBounds? GetBoundsToProcess(ChunkBounds processingBounds, int originX, int originY)
    {
        ChunkBounds? union = null;
        foreach (var (cx, cy) in GetOverlappedChunks(processingBounds))
        {
            union = Union(union, GetActiveBounds(cx, cy));
        }

        if (union == null)
        {
            return null;
        }

        int minX = Math.Max(union.Value.MinX - 1, processingBounds.MinX);
        int minY = Math.Max(union.Value.MinY - 1, processingBounds.MinY);
        int maxX = Math.Min(union.Value.MaxX + 1, processingBounds.MaxX);
        int maxY = Math.Min(union.Value.MaxY + 1, processingBounds.MaxY);

        // For Margolus algorithm: depending on the phase of the margolus algorithm,
        // potentially adds rows of additional cells to the given chunk to align the active cells
        // with the Margolus grid
        minX = Math.Max(SnapDown(minX, originX & 1), processingBounds.MinX);
        minY = Math.Max(SnapDown(minY, originY & 1), processingBounds.MinY);
        maxX = Math.Min(SnapUp(maxX, originX & 1), processingBounds.MaxX);
        maxY = Math.Min(SnapUp(maxY, originY & 1), processingBounds.MaxY);

        if (minX >= maxX || minY >= maxY)
        {
            return null;
        }

        return new ChunkBounds(minX, minY, maxX, maxY);
    }

    // Whether any chunk in a given area reports an active rectangle.
    public bool IsRegionActive(ChunkBounds bounds)
    {
        foreach (var (cx, cy) in GetOverlappedChunks(bounds))
        {
            var active = GetActiveBounds(cx, cy);
            if (active != null && Overlaps(active.Value, bounds))
            {
                return true;
            }
        }

        return false;
    }

    // Called at the end of a tick, once all active chunks are processed.
    // Updates the size of the active chunks with newly written touched regions, removing the oldest entry.
    // Also drops tracking for any inactive chunks.
    public void EndTick()
    {
        List<(int Cx, int Cy)>? toRemove = null;
        foreach (var (key, window) in _chunks)
        {
            window.History[window.NextSlot] = window.ThisTick;
            window.NextSlot = (window.NextSlot + 1) % _windowSize;
            window.ThisTick = null;

            bool anyHistory = false;
            foreach (var entry in window.History)
            {
                if (entry != null)
                {
                    anyHistory = true;
                    break;
                }
            }

            if (!anyHistory)
            {
                (toRemove ??= []).Add(key);
            }
        }

        if (toRemove != null)
        {
            foreach (var key in toRemove)
            {
                _chunks.TryRemove(key, out _);
            }
        }
    }

    public IEnumerable<(int ChunkX, int ChunkY, ChunkBounds Bounds)> TrackedChunks()
    {
        foreach (var key in _chunks.Keys)
        {
            var bounds = GetActiveBounds(key.Cx, key.Cy);
            if (bounds != null)
            {
                yield return (key.Cx, key.Cy, bounds.Value);
            }
        }
    }

    private static ChunkBounds? Union(ChunkBounds? a, ChunkBounds? b)
    {
        if (a == null)
        {
            return b;
        }

        if (b == null)
        {
            return a;
        }

        return new ChunkBounds(
            Math.Min(a.Value.MinX, b.Value.MinX),
            Math.Min(a.Value.MinY, b.Value.MinY),
            Math.Max(a.Value.MaxX, b.Value.MaxX),
            Math.Max(a.Value.MaxY, b.Value.MaxY));
    }

    private static bool Overlaps(ChunkBounds a, ChunkBounds b) =>
        a.MinX < b.MaxX && b.MinX < a.MaxX && a.MinY < b.MaxY && b.MinY < a.MaxY;

    private static bool HasParity(int value, int parity) => ((value - parity) & 1) == 0;

    private static int SnapDown(int value, int parity) => HasParity(value, parity) ? value : value - 1;

    private static int SnapUp(int value, int parity) => HasParity(value, parity) ? value : value + 1;

    private void Touch(int cx, int cy, int cellX, int cellY)
    {
        if (cx < 0 || cx >= _chunkCountX || cy < 0 || cy >= _chunkCountY)
        {
            return;
        }

        var window = _chunks.GetOrAdd((cx, cy), _ => new ChunkWindow(_windowSize));

        lock (window)
        {
            window.ThisTick = Union(window.ThisTick, new ChunkBounds(cellX, cellY, cellX + 1, cellY + 1));
        }
    }

    private (int Cx, int Cy)[] GetOverlappedChunks(ChunkBounds bounds)
    {
        int x0 = bounds.MinX / _chunkSize;
        int y0 = bounds.MinY / _chunkSize;
        int x1 = (bounds.MaxX - 1) / _chunkSize;
        int y1 = (bounds.MaxY - 1) / _chunkSize;

        if (x0 == x1 && y0 == y1)
        {
            return [(x0, y0)];
        }

        if (x0 == x1)
        {
            return [(x0, y0), (x0, y1)];
        }

        if (y0 == y1)
        {
            return [(x0, y0), (x1, y0)];
        }

        return [(x0, y0), (x1, y0), (x0, y1), (x1, y1)];
    }

    private class ChunkWindow
    {
        public ChunkWindow(int windowSize)
        {
            History = new ChunkBounds?[windowSize];
        }

        public ChunkBounds?[] History { get; }

        public int NextSlot { get; set; }

        public ChunkBounds? ThisTick { get; set; }
    }
}
