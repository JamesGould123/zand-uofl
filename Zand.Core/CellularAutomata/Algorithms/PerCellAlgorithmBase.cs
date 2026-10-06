namespace Zand.Core.CellularAutomata.Algorithms;

using Zand.Core.CellularAutomata.Chunking;

public abstract class PerCellAlgorithmBase : ICaAlgorithm
{
    private readonly RowSweepOrder _rowSweepOrder;

    protected PerCellAlgorithmBase(int seed, RowSweepOrder rowSweepOrder = RowSweepOrder.BottomUp)
    {
        Rng = new Random(seed);
        _rowSweepOrder = rowSweepOrder;

        // whether cells are updated starting from the top going down, or from the bottom going up (see RowSweepOrder)
        SweepBottomToTop = rowSweepOrder switch
        {
            RowSweepOrder.BottomUp => true,
            RowSweepOrder.TopDown => false,
            RowSweepOrder.Oscillating => true,
            RowSweepOrder.Randomized => Rng.Next(2) == 0,
            _ => throw new ArgumentOutOfRangeException(nameof(rowSweepOrder), rowSweepOrder, message: null)
        };
    }

    internal bool SweepRightToLeft { get; set; }

    internal bool SweepBottomToTop { get; set; }

    protected Random Rng { get; }

    public void Update(CaGrid grid, ChunkBounds bounds)
    {
        if (SweepBottomToTop)
        {
            for (int y = bounds.MaxY - 1; y >= bounds.MinY; y--)
            {
                UpdateRow(grid, bounds, y);
            }
        }
        else
        {
            for (int y = bounds.MinY; y < bounds.MaxY; y++)
            {
                UpdateRow(grid, bounds, y);
            }
        }
    }

    public virtual void OnTickComplete()
    {
        SweepRightToLeft = Rng.Next(2) == 0;
        SweepBottomToTop = _rowSweepOrder switch
        {
            RowSweepOrder.BottomUp => true,
            RowSweepOrder.TopDown => false,
            RowSweepOrder.Oscillating => !SweepBottomToTop,
            RowSweepOrder.Randomized => Rng.Next(2) == 0,
            _ => throw new ArgumentOutOfRangeException(nameof(_rowSweepOrder), _rowSweepOrder, message: null)
        };
    }

    protected abstract void UpdateCell(CaGrid grid, int x, int y);

    private void UpdateRow(CaGrid grid, ChunkBounds bounds, int y)
    {
        if (SweepRightToLeft)
        {
            for (int x = bounds.MaxX - 1; x >= bounds.MinX; x--)
            {
                UpdateCell(grid, x, y);
            }
        }
        else
        {
            for (int x = bounds.MinX; x < bounds.MaxX; x++)
            {
                UpdateCell(grid, x, y);
            }
        }
    }
}
