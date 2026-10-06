namespace Zand.Core.CellularAutomata.Algorithms;

using Zand.Core.CellularAutomata.Elements;

public class PerCellRandomizedAlgorithm : PerCellAlgorithmBase
{
    private readonly uint _seed;

    private uint _tick;

    public PerCellRandomizedAlgorithm(int seed, RowSweepOrder rowSweepOrder = RowSweepOrder.BottomUp)
        : base(seed, rowSweepOrder)
    {
        _seed = (uint)seed;
    }

    public override void OnTickComplete()
    {
        base.OnTickComplete();
        _tick++;
    }

    // Each liquid cell's left/right preference comes from a hash of the seed, its position and the tick,
    // rather than a shared Random. This provides a significant improvement for frametime in the heavy scenario.
    //
    // Chunks are updated in parallel, and a shared generator would need a lock
    // on every liquid cell, which the worker threads end up queueing for once there are many water cells.
    // The hash needs no shared state, and gives the same choice whichever thread handles the cell.
    internal static bool LeftFirst(uint seed, int x, int y, uint tick)
    {
        // Each input is multiplied by a different large odd constant (xxHash32's first three primes)
        // so x, y and tick don't cancel each other out before mixing.
        // Source: Collet, Y. xxHash. https://github.com/Cyan4973/xxHash/blob/dev/xxhash.h
        uint h = seed ^ ((uint)x * 0x9E3779B1u) ^ ((uint)y * 0x85EBCA77u) ^ (tick * 0xC2B2AE3Du);

        // Final mixing steps (fmix32) from MurmurHash3, so neighbouring cells and ticks don't produce related bits.
        // Source: Appleby, A. MurmurHash3. https://github.com/aappleby/smhasher/blob/master/src/MurmurHash3.cpp
        h ^= h >> 16;
        h *= 0x85EBCA6Bu;
        h ^= h >> 13;
        h *= 0xC2B2AE35u;
        h ^= h >> 16;
        return (h & 1) == 0;
    }

    protected override void UpdateCell(CaGrid grid, int x, int y)
    {
        switch (ElementProperties.GetCategory(grid.GetCell(x, y).Type))
        {
            case ParticleCategory.Powder:
                PowderElement.Update(grid, x, y);
                break;
            case ParticleCategory.Liquid:
                RandomizedLiquidElement.Update(grid, x, y, LeftFirst(_seed, x, y, _tick));
                break;
        }
    }
}
