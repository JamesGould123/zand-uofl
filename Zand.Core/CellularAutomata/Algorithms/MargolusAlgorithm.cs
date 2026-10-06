namespace Zand.Core.CellularAutomata.Algorithms;

using System.Collections.Concurrent;
using Zand.Core.CellularAutomata.Chunking;

// The Margolus BCA algorithm, with several independently toggleable
// configurations to avoid phase-lock (repetitive states).
// The main idea of a Margolus BCA is to split the CA grid into 2x2 blocks, and then solve each 2x2 block independently.
// In theory this should be highly efficient, however we do need to shift the origin of the 2x2 blocks
// that way particles can move freely, instead of only "inside" a set of 2x2 blocks.
// The main constructor can be used for "plain" Margolus,
// where the three configuration variations can be built via the Guarded/Randomized/GuardedRandomized factory methods.
// Citation: Toffoli, T., & Margolus, N. (1987). Cellular automata machines: A new environment for modeling.
// MIT Press.
public class MargolusAlgorithm : ICaAlgorithm
{
    private static readonly (int X, int Y)[] Offsets = [(0, 0), (1, 1), (1, 0), (0, 1)];

    private readonly bool _enableSwapGuard;
    private readonly int _historyWindow;
    private readonly bool _randomizePhaseOrder;

    // Only used in randomized variants. Re-rolled once per 4-phase cycle,
    // randomizing the order of the next cycle.
    private readonly Random? _phaseOrderRng;
    private readonly int[] _phaseOrder = [0, 1, 2, 3];

    // Only used in guarded variants.
    // Records the last lateral swap with a liquid in between horizontal cell pairs.
    // The idea is to prevent water particles from repeatedly moving right then left repeatedly.
    private readonly ConcurrentDictionary<(int X, int Y), int>? _lastLateralSwapTick;

    private int _phase;
    private int _tick;
    private int _cyclePosition;

    public MargolusAlgorithm(
        bool enableSwapGuard = false,
        bool randomizePhaseOrder = false,
        int historyWindow = 3,
        int phaseOrderSeed = 4111)
    {
        _enableSwapGuard = enableSwapGuard;
        _historyWindow = historyWindow;
        _lastLateralSwapTick = enableSwapGuard ? new ConcurrentDictionary<(int X, int Y), int>() : null;

        _randomizePhaseOrder = randomizePhaseOrder;
        if (randomizePhaseOrder)
        {
            _phaseOrderRng = new Random(phaseOrderSeed);
            ShufflePhaseOrder();
        }
    }

    public int CurrentPhase => _randomizePhaseOrder ? _phaseOrder[_cyclePosition] : _phase;

    public (int X, int Y) CurrentOffset => Offsets[CurrentPhase];

    // The origins of the Margolus blocks and the Chunks (see ChunkGrid) are both shifted together.
    public (int OffsetX, int OffsetY) ChunkOrigin => Offsets[CurrentPhase];

    // As there are 4 different offsets that we cycle between,
    // it is possible for a cell to only be moved in one of the offsets.
    // We don't want to mark the chunk as settled until all the offsets have been cycled through without movement.
    public int SettlingPeriod => Offsets.Length;

    public static MargolusAlgorithm Guarded(int historyWindow = 3) =>
        new(enableSwapGuard: true, historyWindow: historyWindow);

    public static MargolusAlgorithm Randomized(int phaseOrderSeed = 4111) =>
        new(randomizePhaseOrder: true, phaseOrderSeed: phaseOrderSeed);

    public static MargolusAlgorithm GuardedRandomized(int historyWindow = 3, int phaseOrderSeed = 4111) =>
        new(enableSwapGuard: true, randomizePhaseOrder: true, historyWindow: historyWindow, phaseOrderSeed: phaseOrderSeed);

    // Note: bounds must be aligned to this tick's phase offset.
    public void Update(CaGrid grid, ChunkBounds bounds)
    {
        for (int y = bounds.MinY; y + 1 < bounds.MaxY; y += 2)
        {
            for (int x = bounds.MinX; x + 1 < bounds.MaxX; x += 2)
            {
                UpdateBlock(grid, x, y);
            }
        }
    }

    public void OnTickComplete()
    {
        _tick++;
        if (_randomizePhaseOrder)
        {
            _cyclePosition++;
            if (_cyclePosition >= _phaseOrder.Length)
            {
                _cyclePosition = 0;
                ShufflePhaseOrder();
            }
        }
        else
        {
            _phase = (_phase + 1) % 4;
        }
    }

    private static bool TrySwap(CaGrid grid, int ax, int ay, int bx, int by)
    {
        var a = grid.GetCell(ax, ay);
        var b = grid.GetCell(bx, by);

        if (ElementProperties.GetDensity(a.Type) <= ElementProperties.GetDensity(b.Type))
        {
            return false;
        }

        grid.SwapCells(ax, ay, bx, by);
        return true;
    }

    private void ShufflePhaseOrder()
    {
        for (int i = _phaseOrder.Length - 1; i > 0; i--)
        {
            int j = _phaseOrderRng!.Next(i + 1);
            (_phaseOrder[i], _phaseOrder[j]) = (_phaseOrder[j], _phaseOrder[i]);
        }
    }

    private void UpdateBlock(CaGrid grid, int x, int y)
    {
        if (grid.GetCell(x, y).Type == CellType.Static ||
            grid.GetCell(x + 1, y).Type == CellType.Static ||
            grid.GetCell(x, y + 1).Type == CellType.Static ||
            grid.GetCell(x + 1, y + 1).Type == CellType.Static)
        {
            return;
        }

        bool leftMoved = TrySwap(grid, x, y, x, y + 1);
        bool rightMoved = TrySwap(grid, x + 1, y, x + 1, y + 1);

        if (!leftMoved && !rightMoved)
        {
            if (!TrySwap(grid, x, y, x + 1, y + 1))
            {
                TrySwap(grid, x + 1, y, x, y + 1);
            }
        }

        TryLateralLiquid(grid, x, y, x + 1, y);
        TryLateralLiquid(grid, x, y + 1, x + 1, y + 1);
    }

    private void TryLateralLiquid(CaGrid grid, int ax, int ay, int bx, int by)
    {
        if (grid.LiquidPressureMode == LiquidPressureMode.Enabled)
        {
            return;
        }

        var a = grid.GetCell(ax, ay);
        var b = grid.GetCell(bx, by);

        bool aToB = ElementProperties.GetCategory(a.Type) == ParticleCategory.Liquid && b.Type == CellType.Empty;
        bool bToA = ElementProperties.GetCategory(b.Type) == ParticleCategory.Liquid && a.Type == CellType.Empty;
        if (!aToB && !bToA)
        {
            return;
        }

        if (_enableSwapGuard)
        {
            var key = (ax, ay);
            if (_lastLateralSwapTick!.TryGetValue(key, out int lastTick) && _tick - lastTick <= _historyWindow)
            {
                return;
            }

            grid.SwapCells(ax, ay, bx, by);
            _lastLateralSwapTick[key] = _tick;
            return;
        }

        grid.SwapCells(ax, ay, bx, by);
    }
}
