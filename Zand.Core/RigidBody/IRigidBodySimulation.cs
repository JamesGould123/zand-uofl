namespace Zand.Core.RigidBody;

using System.Numerics;

public interface IRigidBodySimulation
{
    void Step(float deltaTime);

    BodyHandle AddStaticBox(float x, float y, float halfWidth, float halfHeight);

    BodyHandle AddDynamicBox(float x, float y, float halfWidth, float halfHeight, float density = 1f, float friction = 0.3f, float angle = 0f);

    BodyHandle AddDynamicPolygon(float x, float y, Vector2[] localVertices, float density = 1f, float friction = 0.3f);

    BodyHandle AddDynamicCircle(float x, float y, float radius, float density = 1f, float friction = 0.3f);

    IReadOnlyList<BodyState> GetBodies();

    void RemoveBody(BodyHandle handle);

    void ApplyForce(BodyHandle handle, float forceX, float forceY);

    void ApplyLinearImpulseAtPoint(BodyHandle handle, Vector2 impulse, Vector2 worldPoint);

    float GetMass(BodyHandle handle);

    Vector2 GetLinearVelocity(BodyHandle handle);

    void SetLinearVelocity(BodyHandle handle, Vector2 velocity);

    float GetAngularVelocity(BodyHandle handle);

    void SetAngularVelocity(BodyHandle handle, float omega);
}
