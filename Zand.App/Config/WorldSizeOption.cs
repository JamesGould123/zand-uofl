namespace Zand.App.Config;

using System;

public enum WorldSizeOption
{
    Small512,
    Medium1024,
    Large2048
}

public static class WorldSizeOptionExtensions
{
    public static (int Width, int Height) Dimensions(this WorldSizeOption option) => option switch
    {
        WorldSizeOption.Small512 => (512, 512),
        WorldSizeOption.Medium1024 => (1024, 1024),
        WorldSizeOption.Large2048 => (2048, 2048),
        _ => throw new ArgumentOutOfRangeException(nameof(option), option, null)
    };
}
