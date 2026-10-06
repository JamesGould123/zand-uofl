namespace Zand.App.Scenarios;

using System;

[AttributeUsage(AttributeTargets.Method)]
public sealed class CameraPanAttribute : Attribute
{
    public CameraPanAttribute(int startX, int startY, int endX, int endY)
    {
        StartX = startX;
        StartY = startY;
        EndX = endX;
        EndY = endY;
    }

    public int StartX { get; }

    public int StartY { get; }

    public int EndX { get; }

    public int EndY { get; }
}
