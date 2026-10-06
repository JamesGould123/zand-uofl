namespace Zand.Core.Tests.Mocks;

using System.Numerics;
using Zand.Core.RigidBody;

// A minimal in-memory stand-in for IRigidBodySimulation shared across Zand.Core.Tests.
internal sealed class FakeRigidBodySimulation : IRigidBodySimulation
{
    private readonly Dictionary<int, Body> _bodies = new();
    private int _nextId;

    public List<(BodyHandle Handle, Vector2 Velocity)> SetLinearVelocityCalls { get; } = [];

    public List<(BodyHandle Handle, float Omega)> SetAngularVelocityCalls { get; } = [];

    public List<(BodyHandle Handle, Vector2 Impulse, Vector2 Point)> AppliedImpulses { get; } = [];

    public List<(BodyHandle Handle, float ForceX, float ForceY)> AppliedForces { get; } = [];

    public List<BodyHandle> RemovedBodies { get; } = [];

    public void Step(float deltaTime)
    {
    }

    public BodyHandle AddStaticBox(float x, float y, float halfWidth, float halfHeight) =>
        Add(new Vector2(x, y), 0f, isStatic: true, new BoxShape(halfWidth, halfHeight), mass: 0f);

    public BodyHandle AddDynamicBox(
        float x, float y, float halfWidth, float halfHeight, float density = 1f, float friction = 0.3f, float angle = 0f) =>
        Add(new Vector2(x, y), angle, isStatic: false, new BoxShape(halfWidth, halfHeight), mass: density * halfWidth * halfHeight * 4f);

    public BodyHandle AddDynamicPolygon(float x, float y, Vector2[] localVertices, float density = 1f, float friction = 0.3f) =>
        Add(new Vector2(x, y), 0f, isStatic: false, new PolygonShape(localVertices), mass: density);

    public BodyHandle AddDynamicCircle(float x, float y, float radius, float density = 1f, float friction = 0.3f) =>
        Add(new Vector2(x, y), 0f, isStatic: false, new CircleShape(radius), mass: density * MathF.PI * radius * radius);

    public IReadOnlyList<BodyState> GetBodies() => _bodies.Values.Select(b => b.State).ToList();

    public void RemoveBody(BodyHandle handle)
    {
        _bodies.Remove(handle.Id);
        RemovedBodies.Add(handle);
    }

    public void ApplyForce(BodyHandle handle, float forceX, float forceY) =>
        AppliedForces.Add((handle, forceX, forceY));

    public void ApplyLinearImpulseAtPoint(BodyHandle handle, Vector2 impulse, Vector2 worldPoint)
    {
        AppliedImpulses.Add((handle, impulse, worldPoint));
        var body = _bodies[handle.Id];
        if (body.Mass > 0f)
        {
            body.LinearVelocity += impulse / body.Mass;
        }
    }

    public float GetMass(BodyHandle handle) => _bodies[handle.Id].Mass;

    public Vector2 GetLinearVelocity(BodyHandle handle) => _bodies[handle.Id].LinearVelocity;

    public void SetLinearVelocity(BodyHandle handle, Vector2 velocity)
    {
        SetLinearVelocityCalls.Add((handle, velocity));
        _bodies[handle.Id].LinearVelocity = velocity;
    }

    public float GetAngularVelocity(BodyHandle handle) => _bodies[handle.Id].AngularVelocity;

    public void SetAngularVelocity(BodyHandle handle, float omega)
    {
        SetAngularVelocityCalls.Add((handle, omega));
        _bodies[handle.Id].AngularVelocity = omega;
    }

    // Test-only helper simulating what a real physics step would do to a body's position between ticks.
    public void MoveBody(BodyHandle handle, Vector2 position)
    {
        var body = _bodies[handle.Id];
        body.State = new BodyState
        {
            Handle = body.State.Handle,
            Position = position,
            Angle = body.State.Angle,
            IsStatic = body.State.IsStatic,
            Shape = body.State.Shape
        };
    }

    private BodyHandle Add(Vector2 position, float angle, bool isStatic, BodyShape shape, float mass)
    {
        var handle = new BodyHandle(_nextId++);
        _bodies[handle.Id] = new Body
        {
            State = new BodyState { Handle = handle, Position = position, Angle = angle, IsStatic = isStatic, Shape = shape },
            Mass = mass
        };

        return handle;
    }

    private sealed class Body
    {
        public BodyState State { get; set; }

        public Vector2 LinearVelocity { get; set; }

        public float AngularVelocity { get; set; }

        public float Mass { get; set; }
    }
}
