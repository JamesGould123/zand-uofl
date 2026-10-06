namespace Zand.Core.RigidBody;

public readonly record struct BodyHandle
{
    internal readonly int Id;

    internal BodyHandle(int id) => Id = id;
}
