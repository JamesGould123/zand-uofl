namespace Zand.Core.RigidBody;

using System.Numerics;

public readonly struct BodyState
{
    public BodyHandle Handle { get; init; }

    public Vector2 Position { get; init; }

    public float Angle { get; init; }

    public bool IsStatic { get; init; }

    public BodyShape Shape { get; init; }
}
