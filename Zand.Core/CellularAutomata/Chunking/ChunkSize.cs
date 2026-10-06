namespace Zand.Core.CellularAutomata.Chunking;

public enum ChunkSize
{
    Disabled,
    Eight,
    Sixteen,
    ThirtyTwo,
    SixtyFour,
    OneTwentyEight
}

public static class ChunkSizeExtensions
{
    public static int ToCellCount(this ChunkSize chunkSize) => chunkSize switch
    {
        ChunkSize.Disabled => 0,
        ChunkSize.Eight => 8,
        ChunkSize.Sixteen => 16,
        ChunkSize.ThirtyTwo => 32,
        ChunkSize.SixtyFour => 64,
        ChunkSize.OneTwentyEight => 128,
        _ => throw new ArgumentOutOfRangeException(nameof(chunkSize), chunkSize, null)
    };
}
