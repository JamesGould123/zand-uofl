namespace Zand.Core.RigidBody;

public static class RigidBodyDefaults
{
    // Friction of static bodies for both rigid body engines:
    // the walls, the floor, and the temporary bodies stamped in for sand.
    // Set explicitly on both backends so they agree.
    public const float StaticFriction = 0.2f;
}
