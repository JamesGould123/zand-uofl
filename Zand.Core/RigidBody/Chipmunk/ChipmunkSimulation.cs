namespace Zand.Core.RigidBody.Chipmunk;

using System.Numerics;
using ChipmunkSharp;

public class ChipmunkSimulation : IRigidBodySimulation
{
    private readonly cpSpace _space;
    private readonly Dictionary<int, BodyMeta> _bodies = new();
    private int _nextId;

    public ChipmunkSimulation(float gravityX = 0f, float gravityY = -10f)
    {
        _space = new cpSpace();
        _space.SetGravity(new cpVect(gravityX, gravityY));
    }

    public void Step(float deltaTime) => _space.Step(deltaTime);

    public BodyHandle AddStaticBox(float x, float y, float halfWidth, float halfHeight)
    {
        var body = cpBody.NewStatic();
        body.SetPosition(new cpVect(x, y));
        _space.AddBody(body);
        var shape = cpPolyShape.BoxShape(body, halfWidth * 2, halfHeight * 2, 0f);
        shape.SetFriction(ShapeFriction(RigidBodyDefaults.StaticFriction));
        _space.AddShape(shape);
        int id = _nextId++;
        _bodies[id] = new BodyMeta(body, shape, new BoxShape(halfWidth, halfHeight), true);
        return new BodyHandle(id);
    }

    public BodyHandle AddDynamicBox(float x, float y, float halfWidth, float halfHeight,
        float density = 1f, float friction = 0.3f, float angle = 0f)
    {
        float mass = density * halfWidth * 2 * halfHeight * 2;
        float moment = cp.MomentForBox(mass, halfWidth * 2, halfHeight * 2);
        var body = new cpBody(mass, moment);
        body.SetPosition(new cpVect(x, y));
        body.SetAngle(angle);
        _space.AddBody(body);
        var shape = cpPolyShape.BoxShape(body, halfWidth * 2, halfHeight * 2, 0f);
        shape.SetFriction(ShapeFriction(friction));
        _space.AddShape(shape);
        int id = _nextId++;
        _bodies[id] = new BodyMeta(body, shape, new BoxShape(halfWidth, halfHeight), false);
        return new BodyHandle(id);
    }

    public BodyHandle AddDynamicPolygon(float x, float y, Vector2[] localVertices,
        float density = 1f, float friction = 0.3f)
    {
        var verts = localVertices.Select(v => new cpVect(v.X, v.Y)).ToArray();
        float area = ComputePolygonArea(verts);
        float mass = density * area;
        float moment = cp.MomentForPoly(mass, verts.Length, verts, cpVect.Zero, 0f);
        var body = new cpBody(mass, moment);
        body.SetPosition(new cpVect(x, y));
        _space.AddBody(body);
        var shape = new cpPolyShape(body, verts.Length, verts, cpTransform.Identity, 0f);
        shape.SetFriction(ShapeFriction(friction));
        _space.AddShape(shape);
        int id = _nextId++;
        _bodies[id] = new BodyMeta(body, shape, new PolygonShape(localVertices), false);
        return new BodyHandle(id);
    }

    public BodyHandle AddDynamicCircle(float x, float y, float radius, float density = 1f, float friction = 0.3f)
    {
        float mass = density * MathF.PI * radius * radius;
        float moment = cp.MomentForCircle(mass, 0f, radius, cpVect.Zero);
        var body = new cpBody(mass, moment);
        body.SetPosition(new cpVect(x, y));
        _space.AddBody(body);
        var shape = new cpCircleShape(body, radius, cpVect.Zero);
        shape.SetFriction(ShapeFriction(friction));
        _space.AddShape(shape);
        int id = _nextId++;
        _bodies[id] = new BodyMeta(body, shape, new CircleShape(radius), false);
        return new BodyHandle(id);
    }

    public void RemoveBody(BodyHandle handle)
    {
        if (!_bodies.TryGetValue(handle.Id, out var meta))
        {
            return;
        }

        _space.RemoveShape(meta.PhysicsShape);
        _space.RemoveBody(meta.Body);
        _bodies.Remove(handle.Id);
    }

    public IReadOnlyList<BodyState> GetBodies()
    {
        var result = new List<BodyState>(_bodies.Count);
        foreach (var (id, meta) in _bodies)
        {
            var pos = meta.Body.GetPosition();
            result.Add(new BodyState
            {
                Handle = new BodyHandle(id),
                Position = new Vector2(pos.x, pos.y),
                Angle = meta.Body.GetAngle(),
                IsStatic = meta.IsStatic,
                Shape = meta.Shape
            });
        }

        return result;
    }

    public void ApplyForce(BodyHandle handle, float forceX, float forceY)
    {
        if (!_bodies.TryGetValue(handle.Id, out var meta))
        {
            return;
        }

        meta.Body.ApplyForce(new cpVect(forceX, forceY), cpVect.Zero);
    }

    public void ApplyLinearImpulseAtPoint(BodyHandle handle, Vector2 impulse, Vector2 worldPoint)
    {
        if (!_bodies.TryGetValue(handle.Id, out var meta))
        {
            return;
        }

        meta.Body.ApplyImpulseAtWorldPoint(new cpVect(impulse.X, impulse.Y), new cpVect(worldPoint.X, worldPoint.Y));
    }

    public float GetMass(BodyHandle handle)
    {
        if (!_bodies.TryGetValue(handle.Id, out var meta))
        {
            return 0f;
        }

        return meta.Body.GetMass();
    }

    public Vector2 GetLinearVelocity(BodyHandle handle)
    {
        if (!_bodies.TryGetValue(handle.Id, out var meta))
        {
            return Vector2.Zero;
        }

        var v = meta.Body.GetVelocity();
        return new Vector2(v.x, v.y);
    }

    public void SetLinearVelocity(BodyHandle handle, Vector2 velocity)
    {
        if (!_bodies.TryGetValue(handle.Id, out var meta))
        {
            return;
        }

        meta.Body.SetVelocity(new cpVect(velocity.X, velocity.Y));
    }

    public float GetAngularVelocity(BodyHandle handle)
    {
        if (!_bodies.TryGetValue(handle.Id, out var meta))
        {
            return 0f;
        }

        return meta.Body.GetAngularVelocity();
    }

    public void SetAngularVelocity(BodyHandle handle, float omega)
    {
        if (!_bodies.TryGetValue(handle.Id, out var meta))
        {
            return;
        }

        meta.Body.SetAngularVelocity(omega);
    }

    // Shoelace formula AKA Gauss's area formula:
    // sums areas of signed triangles from the origin to each of the edges;
    // cancellation of overlapping regions returns the exact area for any simple polygon (whether convex or concave).
    private static float ComputePolygonArea(cpVect[] verts)
    {
        float area = 0f;
        int n = verts.Length;
        for (int i = 0; i < n; i++)
        {
            var a = verts[i];
            var b = verts[(i + 1) % n];
            area += (a.x * b.y) - (b.x * a.y);
        }

        return MathF.Abs(area) * 0.5f;
    }

    // Box2D gives two touching shapes a friction of sqrt(a * b), while Chipmunk uses a * b.
    // Storing the square root of the friction each shape is meant to have makes Chipmunk's product come out
    // equal to Box2D's mix for every pair of shapes, so both backends grip the same.
    private static float ShapeFriction(float friction) => MathF.Sqrt(friction);

    private record BodyMeta(cpBody Body, cpShape PhysicsShape, BodyShape Shape, bool IsStatic);
}
