namespace Zand.Core.CellularAutomata.Algorithms;

using Zand.Core.CellularAutomata.Chunking;

public interface ICaAlgorithm
{
    // A Margolus/BCA concept: Margolus breaks the CA grid into a series of 2x2 blocks,
    // and resolves each of these blocks individually. To avoid visible artifacts and repeating patterns
    // the borders of the grid of margolus blocks move in a randomized (but repeating) order up/down/left/right.
    // When splitting the world into chunks, this oscillation of the Margolus grid is taken into account using this origin.
    (int OffsetX, int OffsetY) ChunkOrigin => (0, 0);

    // Used for the ActiveRectangle optimization (see ActiveRectangleTracker).
    // Cells must be "settled" (without activity) for this many ticks before they're removed from tracking,
    // this avoids cells that are inactive for a single tick becoming incorrectly permanently inactive, e.g.
    // freezing a cell of sand in the air just because it had sand under it for one tick that has now moved.
    int SettlingPeriod => 1;

    // Update the whole CA grid. Only used when Chunking disabled.
    void Update(CaGrid grid)
    {
        var (originX, originY) = ChunkOrigin;
        Update(grid, new ChunkBounds(originX, originY, grid.Width, grid.Height));
        OnTickComplete();
    }

    // Updates a specific section of the CA grid.
    void Update(CaGrid grid, ChunkBounds bounds);

    // Called when Update is completed for the whole grid.
    void OnTickComplete()
    {
    }
}
