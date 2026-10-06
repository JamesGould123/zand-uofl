namespace Zand.Core.RigidBody.Box2D;

using System.Numerics;
using Box2DSharp.Dynamics;
using B2CircleShape = Box2DSharp.Collision.Shapes.CircleShape;
using B2PolyShape = Box2DSharp.Collision.Shapes.PolygonShape;

public class Box2DSimulation : IRigidBodySimulation
{
    private readonly World _world;
    private readonly Dictionary<int, BodyMeta> _bodies = new();
    private int _nextId;

    public Box2DSimulation(float gravityX = 0f, float gravityY = -10f)
    {
        _world = new World(new Vector2(gravityX, gravityY));
    }

    public void Step(float deltaTime) => _world.Step(deltaTime, 8, 3);

    public BodyHandle AddStaticBox(float x, float y, float halfWidth, float halfHeight)
    {
        var bodyDef = new BodyDef
        {
            BodyType = BodyType.StaticBody,
            Position = new Vector2(x, y)
        };
        var body = _world.CreateBody(bodyDef);
        var shape = new B2PolyShape();
        shape.SetAsBox(halfWidth, halfHeight);
        var fixtureDef = new FixtureDef { Shape = shape, Density = 0f, Friction = RigidBodyDefaults.StaticFriction };
        body.CreateFixture(fixtureDef);
        return Register(body, new BoxShape(halfWidth, halfHeight), isStatic: true);
    }

    public BodyHandle AddDynamicBox(float x, float y, float halfWidth, float halfHeight,
        float density = 1f, float friction = 0.3f, float angle = 0f)
    {
        var bodyDef = new BodyDef
        {
            BodyType = BodyType.DynamicBody,
            Position = new Vector2(x, y)
        };
        var body = _world.CreateBody(bodyDef);
        body.SetTransform(new Vector2(x, y), angle);
        var shape = new B2PolyShape();
        shape.SetAsBox(halfWidth, halfHeight);
        var fixtureDef = new FixtureDef { Shape = shape, Density = density, Friction = friction };
        body.CreateFixture(fixtureDef);
        return Register(body, new BoxShape(halfWidth, halfHeight), isStatic: false);
    }

    public BodyHandle AddDynamicPolygon(float x, float y, Vector2[] localVertices,
        float density = 1f, float friction = 0.3f)
    {
        var bodyDef = new BodyDef
        {
            BodyType = BodyType.DynamicBody,
            Position = new Vector2(x, y)
        };
        var body = _world.CreateBody(bodyDef);
        var shape = new B2PolyShape();
        shape.Set(localVertices);
        var fixtureDef = new FixtureDef { Shape = shape, Density = density, Friction = friction };
        body.CreateFixture(fixtureDef);
        return Register(body, new PolygonShape(localVertices), isStatic: false);
    }

    public BodyHandle AddDynamicCircle(float x, float y, float radius, float density = 1f, float friction = 0.3f)
    {
        var bodyDef = new BodyDef
        {
            BodyType = BodyType.DynamicBody,
            Position = new Vector2(x, y)
        };
        var body = _world.CreateBody(bodyDef);
        var shape = new B2CircleShape { Radius = radius };
        var fixtureDef = new FixtureDef { Shape = shape, Density = density, Friction = friction };
        body.CreateFixture(fixtureDef);
        return Register(body, new CircleShape(radius), isStatic: false);
    }

    public IReadOnlyList<BodyState> GetBodies()
    {
        var result = new List<BodyState>(_bodies.Count);
        foreach (var (id, meta) in _bodies)
        {
            result.Add(new BodyState
            {
                Handle = new BodyHandle(id),
                Position = meta.Body.GetPosition(),
                Angle = meta.Body.GetAngle(),
                IsStatic = meta.IsStatic,
                Shape = meta.Shape
            });
        }

        return result;
    }

    public void RemoveBody(BodyHandle handle)
    {
        if (!_bodies.TryGetValue(handle.Id, out var meta))
        {
            return;
        }

        _world.DestroyBody(meta.Body);
        _bodies.Remove(handle.Id);
    }

    public void ApplyForce(BodyHandle handle, float forceX, float forceY)
    {
        if (!_bodies.TryGetValue(handle.Id, out var meta))
        {
            return;
        }

        meta.Body.ApplyForceToCenter(new Vector2(forceX, forceY), wake: true);
    }

    public void ApplyLinearImpulseAtPoint(BodyHandle handle, Vector2 impulse, Vector2 worldPoint)
    {
        if (!_bodies.TryGetValue(handle.Id, out var meta))
        {
            return;
        }

        meta.Body.ApplyLinearImpulse(impulse, worldPoint, wake: true);
    }

    public float GetMass(BodyHandle handle)
    {
        if (!_bodies.TryGetValue(handle.Id, out var meta))
        {
            return 0f;
        }

        return meta.Body.Mass;
    }

    public Vector2 GetLinearVelocity(BodyHandle handle)
    {
        if (!_bodies.TryGetValue(handle.Id, out var meta))
        {
            return Vector2.Zero;
        }

        return meta.Body.LinearVelocity;
    }

    public void SetLinearVelocity(BodyHandle handle, Vector2 velocity)
    {
        if (!_bodies.TryGetValue(handle.Id, out var meta))
        {
            return;
        }

        meta.Body.SetLinearVelocity(velocity);
    }

    public float GetAngularVelocity(BodyHandle handle)
    {
        if (!_bodies.TryGetValue(handle.Id, out var meta))
        {
            return 0f;
        }

        return meta.Body.AngularVelocity;
    }

    public void SetAngularVelocity(BodyHandle handle, float omega)
    {
        if (!_bodies.TryGetValue(handle.Id, out var meta))
        {
            return;
        }

        meta.Body.SetAngularVelocity(omega);
    }

    private BodyHandle Register(Body body, BodyShape shape, bool isStatic)
    {
        var handle = new BodyHandle(_nextId++);
        _bodies[handle.Id] = new BodyMeta(body, shape, isStatic);
        return handle;
    }

    private record BodyMeta(Body Body, BodyShape Shape, bool IsStatic);
}
