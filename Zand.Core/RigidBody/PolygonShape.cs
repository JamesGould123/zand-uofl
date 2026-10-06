namespace Zand.Core.RigidBody;

using System.Numerics;

public sealed record PolygonShape(Vector2[] LocalVertices) : BodyShape;
