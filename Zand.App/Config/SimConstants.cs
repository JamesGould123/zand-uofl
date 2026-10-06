namespace Zand.App.Config;

public static class SimConstants
{
    public const int CameraWidthCells = 800 / CellSize;
    public const int CameraHeightCells = 480 / CellSize;

    public const int CellSize = 4;
    public const float PixelsPerMeter = 20f;

    // Every simulation step advances time by this amount,
    // even if the frame processing took longer or shorter
    public const float SimStepSeconds = 1f / 60f;

    public const int MenuWidthCells = 1280 / CellSize;
    public const int MenuHeightCells = 952 / CellSize;
}
