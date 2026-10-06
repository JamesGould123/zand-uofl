namespace Zand.Core.CellularAutomata;

using System.Collections.Concurrent;
using System.Threading.Tasks;
using Zand.Core.CellularAutomata.Algorithms;
using Zand.Core.CellularAutomata.Chunking;

public class CaGrid
{
    // Using flat byte[] instead of Cell[,]: this is a flat buffer of raw CellType bytes in one contiguous
    // memory region. Allows reading of bulk slices of cells at once.
    // Citation: Cagigas-Muñiz, D., Diaz-del-Rio, F., Sevillano-Ramos, J. L., & Guisado-Lizar, J.-L. (2022).
    // Efficient simulation execution of cellular automata on GPU. Simulation Modelling Practice and Theory,
    // 118, 102519. Sec. 5.2 (Packet coding).
    private readonly byte[] _cellTypes;
    private readonly ICaAlgorithm _algorithm;
    private readonly ChunkSize _chunkSize;
    private readonly ActiveRectangle _activeRectangleMode;
    private readonly ParallelismMode _parallelismMode;
    private readonly ActiveRectangleTracker? _activeRectangles;

    private readonly float[,]? _liquidFill;
    private readonly int[,]? _waterCellCountByChunk;

    private readonly uint[,]? _lastMovedTick;
    private readonly Random _chunkOrderRng;

    // Starts at 1, not 0. This is to support the SameTickMoveGuard -
    // cells that haven't been moved yet have their last moved time as 0
    // and cells are only allowed to move if this tick is after the lastMovedTick
    // so a real tick of 0 would make every fresh cell look like it already moved this tick
    // and the guard would skip it.
    private uint _currentTick = 1;

    public CaGrid(int width, int height, ICaAlgorithm algorithm,
        ChunkSize chunkSize = ChunkSize.Disabled,
        ActiveRectangle activeRectangleMode = ActiveRectangle.Disabled,
        ParallelismMode parallelismMode = ParallelismMode.Sequential,
        LiquidPressureMode liquidPressureMode = LiquidPressureMode.Disabled,
        SameTickMoveGuard sameTickMoveGuard = SameTickMoveGuard.Disabled,
        int chunkOrderSeed = 1337)
    {
        Width = width;
        Height = height;
        _cellTypes = new byte[width * height];
        _algorithm = algorithm;
        _chunkSize = chunkSize;
        _activeRectangleMode = activeRectangleMode;
        _parallelismMode = parallelismMode;
        _activeRectangles = chunkSize == ChunkSize.Disabled
            ? null
            : new ActiveRectangleTracker(width, height, chunkSize.ToCellCount(), algorithm.SettlingPeriod);
        _chunkOrderRng = new Random(chunkOrderSeed);
        LiquidPressureMode = liquidPressureMode;
        _liquidFill = liquidPressureMode == LiquidPressureMode.Enabled ? new float[width, height] : null;
        _waterCellCountByChunk = chunkSize != ChunkSize.Disabled && liquidPressureMode == LiquidPressureMode.Enabled
            ? new int[
                (width + chunkSize.ToCellCount() - 1) / chunkSize.ToCellCount(),
                      (height + chunkSize.ToCellCount() - 1) / chunkSize.ToCellCount()]
            : null;
        _lastMovedTick = sameTickMoveGuard == SameTickMoveGuard.Enabled ? new uint[width, height] : null;
    }

    public int Width { get; }

    public int Height { get; }

    public ICaAlgorithm Algorithm => _algorithm;

    public LiquidPressureMode LiquidPressureMode { get; }

    public bool InBounds(int x, int y) => x >= 0 && x < Width && y >= 0 && y < Height;

    public Cell GetCell(int x, int y) => new Cell { Type = (CellType)_cellTypes[Index(x, y)] };

    public void SetCell(int x, int y, Cell cell)
    {
        int index = Index(x, y);
        TrackWaterCount(x, y, (CellType)_cellTypes[index], cell.Type);
        _cellTypes[index] = (byte)cell.Type;
        if (_liquidFill != null)
        {
            _liquidFill[x, y] = cell.Type == CellType.Water ? 1f : 0f;
        }

        _activeRectangles?.MarkDirty(x, y);
    }

    // Gets a cell's level of "liquid" mass.
    // 1 for non-water, non-empty cells (Sand/Static)
    // 0 for empty cells
    // If water:
    //     If liquidPressureMode enabled, a fraction
    //     Else 1
    public float GetFill(int x, int y)
    {
        if (_liquidFill != null)
        {
            return _liquidFill[x, y];
        }

        return (CellType)_cellTypes[Index(x, y)] == CellType.Empty ? 0f : 1f;
    }

    // For LiquidPressure's setting cells:
    // below MinFill the cell becomes Empty,
    // otherwise it is set to Water carrying a given fractional fill value
    public void SetLiquidFill(int x, int y, float fill)
    {
        const float MinFill = 0.0005f;
        var newType = fill <= MinFill ? CellType.Empty : CellType.Water;
        int index = Index(x, y);
        TrackWaterCount(x, y, (CellType)_cellTypes[index], newType);
        _cellTypes[index] = (byte)newType;
        if (_liquidFill != null)
        {
            _liquidFill[x, y] = newType == CellType.Water ? fill : 0f;
        }

        _activeRectangles?.MarkDirty(x, y);
    }

    // Exchanges the type (and when tracked, fill,) of two cells.
    // Every call site treats (ax, ay) as the cell a mover is leaving and (bx, by) as the cell it's
    // entering - SwapCells itself doesn't need that distinction for the swap, but the same-tick
    // move guard does: only the destination (bx, by) gets stamped as "moved this tick", since only
    // an occupied cell is ever guard-checked before moving, and every real call site leaves an
    // Empty cell behind at (ax, ay).
    public void SwapCells(int ax, int ay, int bx, int by)
    {
        int aIndex = Index(ax, ay);
        int bIndex = Index(bx, by);
        var aType = (CellType)_cellTypes[aIndex];
        var bType = (CellType)_cellTypes[bIndex];
        TrackWaterCount(ax, ay, aType, bType);
        TrackWaterCount(bx, by, bType, aType);
        _cellTypes[aIndex] = (byte)bType;
        _cellTypes[bIndex] = (byte)aType;
        if (_liquidFill != null)
        {
            (_liquidFill[ax, ay], _liquidFill[bx, by]) = (_liquidFill[bx, by], _liquidFill[ax, ay]);
        }

        if (_lastMovedTick != null)
        {
            _lastMovedTick[bx, by] = _currentTick;
        }

        _activeRectangles?.MarkDirty(ax, ay);
        _activeRectangles?.MarkDirty(bx, by);
    }

    // Whether the cell at (x, y) has already been moved once this tick by SwapCells - used to stop
    // the PerCell family's scanline sweep from re-evaluating (and potentially re-moving) the same
    // physical parcel of water/sand a second time in one tick, which otherwise happens whenever a
    // move lands in territory the sweep hasn't reached yet (a not-yet-processed chunk, or a column
    // further along the current row). Always false when SameTickMoveGuard is Disabled.
    public bool HasMovedThisTick(int x, int y) => _lastMovedTick != null && _lastMovedTick[x, y] == _currentTick;

    public bool IsRegionActive(ChunkBounds bounds) => _activeRectangles?.IsRegionActive(bounds) ?? true;

    public IEnumerable<ChunkBounds> ActiveRectangles() =>
        _activeRectangles?.TrackedChunks().Select(t => t.Bounds) ?? [];

    public void Update()
    {
        _currentTick++;

        if (_chunkSize == ChunkSize.Disabled)
        {
            _algorithm.Update(this);
            ApplyLiquidPressure();
            return;
        }

        var tracker = _activeRectangles!;

        var (originX, originY) = _algorithm.ChunkOrigin;
        var chunkGrid = new ChunkGrid(Width, Height, _chunkSize.ToCellCount(), originX, originY);
        bool reverseChunkOrder = _chunkOrderRng.Next(2) == 0;

        if (_parallelismMode == ParallelismMode.Sequential)
        {
            foreach (var band in chunkGrid.EnumerateRowBands())
            {
                var chunksInOrder = reverseChunkOrder ? band.Reverse() : band;
                foreach (var (_, bounds) in chunksInOrder)
                {
                    var boundsToProcess = BoundsToProcess(tracker, bounds, originX, originY);
                    if (boundsToProcess != null)
                    {
                        _algorithm.Update(this, boundsToProcess.Value);
                    }
                }
            }
        }
        else
        {
            UpdateParallel(tracker, chunkGrid, originX, originY, reverseChunkOrder);
        }

        _algorithm.OnTickComplete();
        tracker.EndTick();
        ApplyLiquidPressure();
    }

    private static int Area(ChunkBounds bounds) => (bounds.MaxX - bounds.MinX) * (bounds.MaxY - bounds.MinY);

    // x is the contiguous axis in memory: every sweep (PerCellAlgorithmBase, MargolusAlgorithm) walks y
    // outer and x inner, so this keeps the hot inner loop moving through consecutive memory
    // instead of jumping columns each step.
    private int Index(int x, int y) => (y * Width) + x;

    private void TrackWaterCount(int x, int y, CellType oldType, CellType newType)
    {
        if (_waterCellCountByChunk == null)
        {
            return;
        }

        bool wasWater = oldType == CellType.Water;
        bool isWater = newType == CellType.Water;
        if (wasWater == isWater)
        {
            return;
        }

        int chunkSize = _chunkSize.ToCellCount();
        _waterCellCountByChunk[x / chunkSize, y / chunkSize] += isWater ? 1 : -1;
    }

    private void ApplyLiquidPressure()
    {
        if (LiquidPressureMode != LiquidPressureMode.Enabled)
        {
            return;
        }

        if (_chunkSize == ChunkSize.Disabled)
        {
            LiquidPressure.Equalize(this, new ChunkBounds(0, 0, Width, Height));
            return;
        }

        int chunkSize = _chunkSize.ToCellCount();
        foreach (var bounds in ActiveRectangles())
        {
            int cx = bounds.MinX / chunkSize;
            int cy = bounds.MinY / chunkSize;
            if (_waterCellCountByChunk![cx, cy] <= 0)
            {
                continue;
            }

            LiquidPressure.Equalize(this, bounds);
        }
    }

    private ChunkBounds? BoundsToProcess(ActiveRectangleTracker tracker, ChunkBounds bounds, int originX, int originY)
    {
        if (_activeRectangleMode == ActiveRectangle.Disabled)
        {
            return tracker.IsRegionActive(bounds) ? bounds : null;
        }

        return tracker.GetBoundsToProcess(bounds, originX, originY);
    }

    // Updating is done by rows of chunks bottom-to-top
    // (to reduce the odds of gravity causing double movement across chunk lines)
    // Chunks are split odd and even so that bordering chunks don't run at the same time,
    // accounting for issues with processing of cells that cross chunk borders
    private void UpdateParallel(ActiveRectangleTracker tracker, ChunkGrid chunkGrid, int originX, int originY, bool reverseChunkOrder)
    {
        foreach (var band in chunkGrid.EnumerateRowBands())
        {
            List<ChunkBounds> evenX = [];
            List<ChunkBounds> oddX = [];
            foreach (var (chunkX, bounds) in band)
            {
                var boundsToProcess = BoundsToProcess(tracker, bounds, originX, originY);
                if (boundsToProcess != null)
                {
                    (chunkX % 2 == 0 ? evenX : oddX).Add(boundsToProcess.Value);
                }
            }

            if (reverseChunkOrder)
            {
                RunPass(oddX);
                RunPass(evenX);
            }
            else
            {
                RunPass(evenX);
                RunPass(oddX);
            }
        }
    }

    private void RunPass(List<ChunkBounds> pass)
    {
        if (pass.Count == 0)
        {
            return;
        }

        if (_parallelismMode == ParallelismMode.ParallelLargestFirst)
        {
            pass.Sort((a, b) => Area(b).CompareTo(Area(a)));
            var partitioner = Partitioner.Create(pass, EnumerablePartitionerOptions.NoBuffering);
            Parallel.ForEach(partitioner, bounds => _algorithm.Update(this, bounds));
        }
        else
        {
            Parallel.ForEach(pass, bounds => _algorithm.Update(this, bounds));
        }
    }
}
