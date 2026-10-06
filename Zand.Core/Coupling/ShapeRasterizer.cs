namespace Zand.Core.Coupling;

using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Zand.Core.CellularAutomata.Chunking;
using Zand.Core.RigidBody;

// Helpers that iterate over the CA grid cells contained in a rigid body's footprint
// (sometimes with an additional margin on the edges).
// Note that only convex shapes are supported, but that concave shapes can be represented as series of connected convex shapes.
public class ShapeRasterizer
{
    // The minimum rows in a band to trigger parallel processing.
    private const int MinRowsPerBand = 4;

    // Helps to mitigate accumulating floating-point addition errors
    private const int ResyncIntervalCells = 64;

    private readonly float _cellsPerMeter;
    private readonly int _worldHeightCells;
    private readonly RasterizationCellParallelism _cellParallelism;

    public ShapeRasterizer(float cellsPerMeter, int worldHeightCells,
        RasterizationCellParallelism cellParallelism = RasterizationCellParallelism.Sequential)
    {
        _cellsPerMeter = cellsPerMeter;
        _worldHeightCells = worldHeightCells;
        _cellParallelism = cellParallelism;
    }

    public void Iterate(BodyState body, int marginCells, Action<int, int> visit)
    {
        switch (body.Shape)
        {
            case BoxShape box:
                IterateOrientedBoundingBox(body.Position, body.Angle, box.HalfWidth, box.HalfHeight, marginCells, visit);
                break;
            case CircleShape circle:
                IterateCircle(body.Position, circle.Radius, marginCells, visit);
                break;
            case PolygonShape polygon:
                IteratePolygon(body.Position, body.Angle, polygon.LocalVertices, marginCells, visit);
                break;
        }
    }

    public bool Contains(BodyState body, Vector2 point) => body.Shape switch
    {
        BoxShape box => IsInsideBox(point, body.Position, body.Angle, box.HalfWidth, box.HalfHeight),
        CircleShape circle => Vector2.DistanceSquared(point, body.Position) <= circle.Radius * circle.Radius,
        PolygonShape polygon => IsInsideConvexPolygon(ToWorldVertices(body.Position, body.Angle, polygon.LocalVertices), point.X, point.Y),
        _ => false
    };

    // Answers the question: Given a point and a rigid body, which edge of the body is closest to the shape
    // Answer is given as an outward normal (a vector originating at the center, pointing perpendicular to that edge)
    public Vector2? NearestFaceNormal(BodyState body, Vector2 point) => body.Shape switch
    {
        BoxShape box => NearestBoxFaceNormal(point, body.Position, body.Angle, box.HalfWidth, box.HalfHeight),
        CircleShape => SafeDirection(point - body.Position),
        PolygonShape polygon => NearestPolygonFaceNormal(ToWorldVertices(body.Position, body.Angle, polygon.LocalVertices), point),
        _ => null
    };

    // Gets an estimation of the smallest rectangle (aligned on the CA grid) that could be drawn around a shape (AKA the AABB)
    // Used to answer "is anything nearby worth doing more precise (and costly) calculations for"
    public ChunkBounds GetBounds(BodyState body, int marginCells) => body.Shape switch
    {
        BoxShape box => GetOrientedBoundingBoxBounds(body.Position, body.Angle, box.HalfWidth, box.HalfHeight, marginCells),
        CircleShape circle => GetCircleBounds(body.Position, circle.Radius, marginCells),
        PolygonShape polygon => GetPolygonBounds(body.Position, body.Angle, polygon.LocalVertices, marginCells),
        _ => new ChunkBounds(0, 0, 0, 0)
    };

    private static bool IsInsideBox(Vector2 point, Vector2 center, float angle, float hw, float hh)
    {
        float dx = point.X - center.X;
        float dy = point.Y - center.Y;
        float cos = MathF.Cos(angle);
        float sin = MathF.Sin(angle);
        float localX = (dx * cos) + (dy * sin);
        float localY = (-dx * sin) + (dy * cos);
        return MathF.Abs(localX) <= hw && MathF.Abs(localY) <= hh;
    }

    private static Vector2 NearestBoxFaceNormal(Vector2 point, Vector2 center, float angle, float hw, float hh)
    {
        float dx = point.X - center.X;
        float dy = point.Y - center.Y;
        float cos = MathF.Cos(angle);
        float sin = MathF.Sin(angle);
        float localX = (dx * cos) + (dy * sin);
        float localY = (-dx * sin) + (dy * cos);

        float xRatio = localX / hw;
        float yRatio = localY / hh;
        Vector2 localNormal = MathF.Abs(xRatio) > MathF.Abs(yRatio)
            ? new Vector2(MathF.Sign(xRatio), 0f)
            : new Vector2(0f, MathF.Sign(yRatio));

        return new Vector2(
            (localNormal.X * cos) - (localNormal.Y * sin),
            (localNormal.X * sin) + (localNormal.Y * cos));
    }

    private static Vector2 NearestPolygonFaceNormal(Vector2[] worldVerts, Vector2 point)
    {
        var centroid = Vector2.Zero;
        foreach (var v in worldVerts)
        {
            centroid += v;
        }

        centroid /= worldVerts.Length;

        var towardPoint = SafeDirection(point - centroid) ?? Vector2.UnitY;

        var best = Vector2.UnitY;
        float bestDot = float.NegativeInfinity;
        int n = worldVerts.Length;
        for (int i = 0; i < n; i++)
        {
            var a = worldVerts[i];
            var b = worldVerts[(i + 1) % n];
            var edge = b - a;
            var normal = SafeDirection(new Vector2(edge.Y, -edge.X)) ?? Vector2.UnitY;
            var mid = (a + b) * 0.5f;
            if (Vector2.Dot(normal, mid - centroid) < 0f)
            {
                normal = -normal;
            }

            float d = Vector2.Dot(normal, towardPoint);
            if (d > bestDot)
            {
                bestDot = d;
                best = normal;
            }
        }

        return best;
    }

    private static Vector2? SafeDirection(Vector2 v) => v.LengthSquared() > 1e-8f ? Vector2.Normalize(v) : null;

    private static Vector2[] ToWorldVertices(Vector2 position, float angle, Vector2[] localVertices)
    {
        float cos = MathF.Cos(angle);
        float sin = MathF.Sin(angle);
        var worldVerts = new Vector2[localVertices.Length];
        for (int i = 0; i < localVertices.Length; i++)
        {
            float lx = localVertices[i].X;
            float ly = localVertices[i].Y;
            worldVerts[i] = new Vector2(
                position.X + (lx * cos) - (ly * sin),
                position.Y + (lx * sin) + (ly * cos));
        }

        return worldVerts;
    }

    // Point-in-convex-polygon using the edge function: a point is inside if it lies on
    // the same side of every directed edge.
    // Technique: Pineda, J. (1988). A parallel algorithm for polygon rasterization. SIGGRAPH '88.
    private static bool IsInsideConvexPolygon(Vector2[] verts, float px, float py)
    {
        int n = verts.Length;
        bool? positive = null;
        for (int i = 0; i < n; i++)
        {
            Vector2 a = verts[i];
            Vector2 b = verts[(i + 1) % n];
            float e = ((b.X - a.X) * (py - a.Y)) - ((b.Y - a.Y) * (px - a.X));
            if (e == 0f)
            {
                continue;
            }

            bool isPositive = e > 0f;
            if (positive == null)
            {
                positive = isPositive;
            }
            else if (positive != isPositive)
            {
                return false;
            }
        }

        return true;
    }

    // Iterates every cell within a body's "oriented bounding box," (including a margin of marginCells on each side).
    // Should iterate over the exact OBB (not just the AABB),
    // avoiding visiting any cells outside the rigid body (and a margin around it, if marginCells > 0).
    // Citation: Ericson, C. (2005). Real-time collision detection. Morgan Kaufmann. ISBN 978-1-55860-732-3.
    private void IterateOrientedBoundingBox(Vector2 center, float angle, float hw, float hh, int marginCells, Action<int, int> visit)
    {
        var bounds = GetOrientedBoundingBoxBounds(center, angle, hw, hh, marginCells);

        float localHW = hw + (marginCells / _cellsPerMeter);
        float localHH = hh + (marginCells / _cellsPerMeter);
        float cosAngle = MathF.Cos(angle);
        float sinAngle = MathF.Sin(angle);
        float step = 1f / _cellsPerMeter;

        // localX/localY at grid cell (0, 0), plus their constant per-cx/per-cy increments - lets any
        // row seed its own starting value in O(1) via LocalXAt/LocalYAt rather than walking there.
        float physX0 = 0.5f * step;
        float physY0 = (_worldHeightCells - 0.5f) * step;
        float dx0 = physX0 - center.X;
        float dy0 = physY0 - center.Y;
        float localX0 = (dx0 * cosAngle) + (dy0 * sinAngle);
        float localY0 = (-dx0 * sinAngle) + (dy0 * cosAngle);
        float dLocalXdCx = cosAngle * step;
        float dLocalXdCy = -sinAngle * step;
        float dLocalYdCx = -sinAngle * step;
        float dLocalYdCy = -cosAngle * step;

        void TestRow(int cy, Action<int, int> emit)
        {
            float localX = 0f;
            float localY = 0f;
            for (int cx = bounds.MinX; cx < bounds.MaxX; cx++)
            {
                if ((cx - bounds.MinX) % ResyncIntervalCells == 0)
                {
                    localX = localX0 + (cx * dLocalXdCx) + (cy * dLocalXdCy);
                    localY = localY0 + (cx * dLocalYdCx) + (cy * dLocalYdCy);
                }

                if (MathF.Abs(localX) <= localHW && MathF.Abs(localY) <= localHH)
                {
                    emit(cx, cy);
                }

                localX += dLocalXdCx;
                localY += dLocalYdCx;
            }
        }

        RunRows(bounds.MinY, bounds.MaxY, TestRow, visit);
    }

    private ChunkBounds GetOrientedBoundingBoxBounds(Vector2 center, float angle, float hw, float hh, int marginCells)
    {
        float cos = MathF.Abs(MathF.Cos(angle));
        float sin = MathF.Abs(MathF.Sin(angle));
        float aabbHW = (hw * cos) + (hh * sin);
        float aabbHH = (hw * sin) + (hh * cos);

        int centerCx = (int)(center.X * _cellsPerMeter);
        int centerCy = _worldHeightCells - (int)(center.Y * _cellsPerMeter);

        int hwCells = (int)Math.Ceiling(aabbHW * _cellsPerMeter) + marginCells;
        int hhCells = (int)Math.Ceiling(aabbHH * _cellsPerMeter) + marginCells;

        return new ChunkBounds(centerCx - hwCells, centerCy - hhCells, centerCx + hwCells + 1, centerCy + hhCells + 1);
    }

    private void IterateCircle(Vector2 center, float radius, int marginCells, Action<int, int> visit)
    {
        var bounds = GetCircleBounds(center, radius, marginCells);
        float expandedRadius = radius + (marginCells / _cellsPerMeter);
        float r2 = expandedRadius * expandedRadius;
        float step = 1f / _cellsPerMeter;

        float dx0 = (0.5f * step) - center.X;
        float dy0 = ((_worldHeightCells - 0.5f) * step) - center.Y;
        float dDxdCx = step;
        float dDydCy = -step;

        void TestRow(int cy, Action<int, int> emit)
        {
            float dx = 0f;
            float dy = dy0 + (cy * dDydCy);
            for (int cx = bounds.MinX; cx < bounds.MaxX; cx++)
            {
                if ((cx - bounds.MinX) % ResyncIntervalCells == 0)
                {
                    dx = dx0 + (cx * dDxdCx);
                }

                if ((dx * dx) + (dy * dy) <= r2)
                {
                    emit(cx, cy);
                }

                dx += dDxdCx;
            }
        }

        RunRows(bounds.MinY, bounds.MaxY, TestRow, visit);
    }

    private ChunkBounds GetCircleBounds(Vector2 center, float radius, int marginCells)
    {
        float expandedRadius = radius + (marginCells / _cellsPerMeter);
        int centerCx = (int)(center.X * _cellsPerMeter);
        int centerCy = _worldHeightCells - (int)(center.Y * _cellsPerMeter);
        int rCells = (int)Math.Ceiling(expandedRadius * _cellsPerMeter);

        return new ChunkBounds(centerCx - rCells, centerCy - rCells, centerCx + rCells + 1, centerCy + rCells + 1);
    }

    // Iterates every cell in a convex polygon's footprint, (including a margin of marginCells on each side).
    // Citation: Pineda (1988).
    private void IteratePolygon(Vector2 position, float angle, Vector2[] localVertices, int marginCells, Action<int, int> visit)
    {
        var worldVerts = ToWorldVertices(position, angle, localVertices);
        var bounds = GetPolygonBounds(worldVerts, marginCells);
        int n = worldVerts.Length;
        float step = 1f / _cellsPerMeter;
        float physX0 = 0.5f * step;
        float physY0 = (_worldHeightCells - 0.5f) * step;

        var edgeValue0 = new float[n];
        var edgeDCx = new float[n];
        var edgeDCy = new float[n];
        for (int i = 0; i < n; i++)
        {
            Vector2 a = worldVerts[i];
            Vector2 b = worldVerts[(i + 1) % n];
            float edgeDx = b.X - a.X;
            float edgeDy = b.Y - a.Y;

            edgeValue0[i] = (edgeDx * (physY0 - a.Y)) - (edgeDy * (physX0 - a.X));
            edgeDCx[i] = -edgeDy * step;
            edgeDCy[i] = -edgeDx * step;
        }

        void TestRow(int cy, Action<int, int> emit)
        {
            Span<float> edgeValues = n <= 16 ? stackalloc float[n] : new float[n];

            for (int cx = bounds.MinX; cx < bounds.MaxX; cx++)
            {
                if ((cx - bounds.MinX) % ResyncIntervalCells == 0)
                {
                    for (int i = 0; i < n; i++)
                    {
                        edgeValues[i] = edgeValue0[i] + (cx * edgeDCx[i]) + (cy * edgeDCy[i]);
                    }
                }

                bool? positive = null;
                bool inside = true;
                for (int i = 0; i < n; i++)
                {
                    float e = edgeValues[i];
                    if (e != 0f)
                    {
                        bool isPositive = e > 0f;
                        if (positive == null)
                        {
                            positive = isPositive;
                        }
                        else if (positive != isPositive)
                        {
                            inside = false;
                            break;
                        }
                    }
                }

                if (inside)
                {
                    emit(cx, cy);
                }

                for (int i = 0; i < n; i++)
                {
                    edgeValues[i] += edgeDCx[i];
                }
            }
        }

        RunRows(bounds.MinY, bounds.MaxY, TestRow, visit);
    }

    private ChunkBounds GetPolygonBounds(Vector2 position, float angle, Vector2[] localVertices, int marginCells) =>
        GetPolygonBounds(ToWorldVertices(position, angle, localVertices), marginCells);

    private ChunkBounds GetPolygonBounds(Vector2[] worldVerts, int marginCells)
    {
        float minPhysX = worldVerts[0].X, maxPhysX = worldVerts[0].X;
        float minPhysY = worldVerts[0].Y, maxPhysY = worldVerts[0].Y;
        for (int i = 1; i < worldVerts.Length; i++)
        {
            if (worldVerts[i].X < minPhysX)
            {
                minPhysX = worldVerts[i].X;
            }

            if (worldVerts[i].X > maxPhysX)
            {
                maxPhysX = worldVerts[i].X;
            }

            if (worldVerts[i].Y < minPhysY)
            {
                minPhysY = worldVerts[i].Y;
            }

            if (worldVerts[i].Y > maxPhysY)
            {
                maxPhysY = worldVerts[i].Y;
            }
        }

        float margin = marginCells / _cellsPerMeter;
        int minCx = (int)((minPhysX - margin) * _cellsPerMeter);
        int maxCx = (int)((maxPhysX + margin) * _cellsPerMeter);
        int minCy = _worldHeightCells - (int)((maxPhysY + margin) * _cellsPerMeter);
        int maxCy = _worldHeightCells - (int)((minPhysY - margin) * _cellsPerMeter);

        return new ChunkBounds(minCx, minCy, maxCx + 1, maxCy + 1);
    }

    // Shared for all shapes above.
    // With parallelism enabled, rows are split into up to Environment.ProcessorCount row-bands.
    private void RunRows(int minY, int maxY, Action<int, Action<int, int>> testRow, Action<int, int> visit)
    {
        int totalRows = maxY - minY;
        if (totalRows <= 0)
        {
            return;
        }

        int bandCount = _cellParallelism == RasterizationCellParallelism.Parallel
            ? Math.Min(Environment.ProcessorCount, totalRows / MinRowsPerBand)
            : 1;

        if (bandCount <= 1)
        {
            for (int cy = minY; cy < maxY; cy++)
            {
                testRow(cy, visit);
            }

            return;
        }

        var bandResults = new List<(int X, int Y)>[bandCount];
        int rowsPerBand = (totalRows + bandCount - 1) / bandCount;

        Parallel.For(0, bandCount, band =>
        {
            int bandStart = minY + (band * rowsPerBand);
            int bandEnd = Math.Min(maxY, bandStart + rowsPerBand);
            var results = new List<(int X, int Y)>();
            void Emit(int cx, int cy) => results.Add((cx, cy));
            Action<int, int> emit = Emit;
            for (int cy = bandStart; cy < bandEnd; cy++)
            {
                testRow(cy, emit);
            }

            bandResults[band] = results;
        });

        foreach (var results in bandResults)
        {
            foreach (var (cx, cy) in results)
            {
                visit(cx, cy);
            }
        }
    }
}
