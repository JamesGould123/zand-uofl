namespace Zand.Core.Tests.Coupling;

using System.Numerics;
using Xunit;
using Zand.Core.CellularAutomata.Chunking;
using Zand.Core.Coupling;
using Zand.Core.RigidBody;

public class ShapeRasterizerTests
{
    // ----- Circle -----
    [Fact]
    public void Contains_Circle_PointAtCenter_ReturnsTrue()
    {
        var rasterizer = CreateRasterizer();
        var body = CreateBody(new Vector2(10, 10), 0f, new CircleShape(2f));

        Assert.True(rasterizer.Contains(body, new Vector2(10, 10)));
    }

    [Fact]
    public void Contains_Circle_PointExactlyAtRadius_ReturnsTrue()
    {
        var rasterizer = CreateRasterizer();
        var body = CreateBody(new Vector2(10, 10), 0f, new CircleShape(2f));

        Assert.True(rasterizer.Contains(body, new Vector2(12, 10)));
    }

    [Fact]
    public void Contains_Circle_PointJustBeyondRadius_ReturnsFalse()
    {
        var rasterizer = CreateRasterizer();
        var body = CreateBody(new Vector2(10, 10), 0f, new CircleShape(2f));

        Assert.False(rasterizer.Contains(body, new Vector2(12.01f, 10)));
    }

    [Fact]
    public void NearestFaceNormal_Circle_PointToTheRight_ReturnsNormalizedDirectionFromCenter()
    {
        var rasterizer = CreateRasterizer();
        var body = CreateBody(new Vector2(10, 10), 0f, new CircleShape(2f));

        var normal = rasterizer.NearestFaceNormal(body, new Vector2(13, 10));

        AssertVectorApprox(1f, 0f, normal);
    }

    [Fact]
    public void NearestFaceNormal_Circle_PointAtCenter_ReturnsNull()
    {
        var rasterizer = CreateRasterizer();
        var body = CreateBody(new Vector2(10, 10), 0f, new CircleShape(2f));

        Assert.Null(rasterizer.NearestFaceNormal(body, new Vector2(10, 10)));
    }

    [Fact]
    public void GetBounds_Circle_MatchesRadiusInCellsAroundBodyCenter()
    {
        var rasterizer = CreateRasterizer();
        var body = CreateBody(new Vector2(10, 10), 0f, new CircleShape(2f));

        var bounds = rasterizer.GetBounds(body, marginCells: 0);

        AssertBounds(8, 8, 13, 13, bounds);
    }

    [Fact]
    public void GetBounds_Circle_MarginExpandsBoundsOnEachSide()
    {
        var rasterizer = CreateRasterizer();
        var body = CreateBody(new Vector2(10, 10), 0f, new CircleShape(2f));

        var bounds = rasterizer.GetBounds(body, marginCells: 3);

        AssertBounds(5, 5, 16, 16, bounds);
    }

    [Fact]
    public void Iterate_Circle_TinyRadiusAlignedToCellCenter_VisitsExactlyOneCell()
    {
        // Cell (10, 10)'s physical center under this grid/scale is exactly (10.5, 9.5).
        var rasterizer = CreateRasterizer();
        var body = CreateBody(new Vector2(10.5f, 9.5f), 0f, new CircleShape(0.1f));

        var visited = CollectVisited(rasterizer, body);

        Assert.Equal(new HashSet<(int, int)> { (10, 10) }, visited);
    }

    [Fact]
    public void Iterate_Circle_CellsNearBoundary_MatchDistanceCheck()
    {
        var rasterizer = CreateRasterizer();
        var body = CreateBody(new Vector2(10, 10), 0f, new CircleShape(2f));

        var visited = CollectVisited(rasterizer, body);

        // Cell (11, 10)'s center is distance-squared 2.5 from (10, 10): inside a radius of 2 (r^2 = 4).
        Assert.Contains((11, 10), visited);

        // Cell (12, 10)'s center is distance-squared 6.5: outside.
        Assert.DoesNotContain((12, 10), visited);
    }

    [Fact]
    public void Iterate_Circle_ParallelMatchesSequentialResults()
    {
        var sequential = CreateRasterizer(worldHeightCells: 80);
        var parallel = CreateRasterizer(worldHeightCells: 80, parallelism: RasterizationCellParallelism.Parallel);
        var body = CreateBody(new Vector2(30, 30), 0f, new CircleShape(10f));

        var sequentialResult = CollectVisited(sequential, body);
        var parallelResult = CollectVisited(parallel, body);

        Assert.Equal(sequentialResult, parallelResult);
        Assert.NotEmpty(sequentialResult);
    }

    [Fact]
    public void Iterate_Circle_WiderThanResyncInterval_StaysGeometricallyCorrectPastTheResyncPoint()
    {
        var rasterizer = CreateRasterizer(worldHeightCells: 250);
        var body = CreateBody(new Vector2(100, 100), 0f, new CircleShape(50f));

        var visited = CollectVisited(rasterizer, body);

        // Cell (140, 150)'s center is distance-squared 1640.5 from (100, 100): inside (r^2 = 2500).
        Assert.Contains((140, 150), visited);

        // Cell (150, 150)'s center is distance-squared 2550.5: outside.
        Assert.DoesNotContain((150, 150), visited);
    }

    // ----- Box -----
    [Fact]
    public void Contains_Box_PointAtCenter_ReturnsTrue()
    {
        var rasterizer = CreateRasterizer();
        var body = CreateBody(new Vector2(10, 10), 0f, new BoxShape(2f, 1f));

        Assert.True(rasterizer.Contains(body, new Vector2(10, 10)));
    }

    [Fact]
    public void Contains_Box_PointOnEdgeUnrotated_ReturnsTrue()
    {
        var rasterizer = CreateRasterizer();
        var body = CreateBody(new Vector2(10, 10), 0f, new BoxShape(2f, 1f));

        Assert.True(rasterizer.Contains(body, new Vector2(12, 10)));
    }

    [Fact]
    public void Contains_Box_PointJustOutsideUnrotated_ReturnsFalse()
    {
        var rasterizer = CreateRasterizer();
        var body = CreateBody(new Vector2(10, 10), 0f, new BoxShape(2f, 1f));

        Assert.False(rasterizer.Contains(body, new Vector2(12.01f, 10)));
    }

    [Fact]
    public void Contains_Box_RespectsRotation()
    {
        var rasterizer = CreateRasterizer();
        var point = new Vector2(10, 11.5f);
        var unrotated = CreateBody(new Vector2(10, 10), 0f, new BoxShape(2f, 1f));
        var rotated = CreateBody(new Vector2(10, 10), MathF.PI / 2f, new BoxShape(2f, 1f));

        Assert.False(rasterizer.Contains(unrotated, point));
        Assert.True(rasterizer.Contains(rotated, point));
    }

    [Fact]
    public void NearestFaceNormal_Box_PointAlongLocalXAxisUnrotated_ReturnsXNormal()
    {
        var rasterizer = CreateRasterizer();
        var body = CreateBody(new Vector2(10, 10), 0f, new BoxShape(2f, 1f));

        var normal = rasterizer.NearestFaceNormal(body, new Vector2(15, 10));

        AssertVectorApprox(1f, 0f, normal);
    }

    [Fact]
    public void NearestFaceNormal_Box_RotatesWithBody()
    {
        var rasterizer = CreateRasterizer();

        // A point along the body's local +X axis, but the body itself is rotated 90 degrees, so
        // the outward normal (still along local +X) now points along world +Y.
        var body = CreateBody(new Vector2(10, 10), MathF.PI / 2f, new BoxShape(2f, 1f));

        var normal = rasterizer.NearestFaceNormal(body, new Vector2(10, 15));

        AssertVectorApprox(0f, 1f, normal);
    }

    [Fact]
    public void GetBounds_Box_Unrotated_MatchesHalfExtentsInCells()
    {
        var rasterizer = CreateRasterizer();
        var body = CreateBody(new Vector2(10, 10), 0f, new BoxShape(2f, 1f));

        var bounds = rasterizer.GetBounds(body, marginCells: 0);

        AssertBounds(8, 9, 13, 12, bounds);
    }

    [Fact]
    public void GetBounds_Box_RotatedNinetyDegrees_SwapsWidthAndHeight()
    {
        var rasterizer = CreateRasterizer();
        var unrotated = CreateBody(new Vector2(10, 10), 0f, new BoxShape(2.3f, 1.1f));
        var rotated = CreateBody(new Vector2(10, 10), MathF.PI / 2f, new BoxShape(2.3f, 1.1f));

        var unrotatedBounds = rasterizer.GetBounds(unrotated, marginCells: 0);
        var rotatedBounds = rasterizer.GetBounds(rotated, marginCells: 0);

        Assert.Equal(unrotatedBounds.MaxX - unrotatedBounds.MinX, rotatedBounds.MaxY - rotatedBounds.MinY);
        Assert.Equal(unrotatedBounds.MaxY - unrotatedBounds.MinY, rotatedBounds.MaxX - rotatedBounds.MinX);
    }

    [Fact]
    public void Iterate_Box_AxisAlignedOnCellGrid_VisitsExactRectangleOfCells()
    {
        var rasterizer = CreateRasterizer();
        var body = CreateBody(new Vector2(10.5f, 9.5f), 0f, new BoxShape(1.5f, 1.5f));

        var visited = CollectVisited(rasterizer, body);

        var expected = new HashSet<(int, int)>();
        for (int cx = 9; cx <= 11; cx++)
        {
            for (int cy = 9; cy <= 11; cy++)
            {
                expected.Add((cx, cy));
            }
        }

        Assert.Equal(expected, visited);
    }

    // ----- Polygon -----
    [Fact]
    public void Contains_Polygon_InteriorPoint_ReturnsTrue()
    {
        var rasterizer = CreateRasterizer();
        var body = CreateBody(new Vector2(10, 10), 0f, CreateRightTriangle());

        Assert.True(rasterizer.Contains(body, new Vector2(11.333f, 11f)));
    }

    [Fact]
    public void Contains_Polygon_PointOutsideTriangle_ReturnsFalse()
    {
        var rasterizer = CreateRasterizer();
        var body = CreateBody(new Vector2(10, 10), 0f, CreateRightTriangle());

        Assert.False(rasterizer.Contains(body, new Vector2(20, 20)));
    }

    [Fact]
    public void GetBounds_Polygon_MatchesAxisAlignedBoundingBoxOfVertices()
    {
        var rasterizer = CreateRasterizer();
        var body = CreateBody(new Vector2(10, 10), 0f, CreateRightTriangle());

        var bounds = rasterizer.GetBounds(body, marginCells: 0);

        AssertBounds(10, 7, 15, 11, bounds);
    }

    [Fact]
    public void NearestFaceNormal_Polygon_PicksEdgeFacingTheQueryPoint()
    {
        var rasterizer = CreateRasterizer();
        var body = CreateBody(new Vector2(10, 10), 0f, CreateRightTriangle());

        // Far below the triangle: should pick the bottom edge's outward normal over the other two.
        var normal = rasterizer.NearestFaceNormal(body, new Vector2(12, 0));

        AssertVectorApprox(0f, -1f, normal, tolerance: 1e-2f);
    }

    [Fact]
    public void Iterate_Polygon_SquareMatchesEquivalentBoxShape()
    {
        var rasterizer = CreateRasterizer();
        Vector2[] squareVertices =
        [
            new Vector2(-1.5f, -1.5f), new Vector2(1.5f, -1.5f), new Vector2(1.5f, 1.5f), new Vector2(-1.5f, 1.5f)
        ];
        var boxBody = CreateBody(new Vector2(10.5f, 9.5f), 0f, new BoxShape(1.5f, 1.5f));
        var polygonBody = CreateBody(new Vector2(10.5f, 9.5f), 0f, new PolygonShape(squareVertices));

        var boxVisited = CollectVisited(rasterizer, boxBody);
        var polygonVisited = CollectVisited(rasterizer, polygonBody);

        Assert.Equal(boxVisited, polygonVisited);
    }

    private static PolygonShape CreateRightTriangle() =>
        new([Vector2.Zero, new Vector2(4, 0), new Vector2(0, 3)]);

    private static ShapeRasterizer CreateRasterizer(
        float cellsPerMeter = 1f, int worldHeightCells = 20, RasterizationCellParallelism parallelism = RasterizationCellParallelism.Sequential) =>
        new(cellsPerMeter, worldHeightCells, parallelism);

    private static BodyState CreateBody(Vector2 position, float angle, BodyShape shape) =>
        new() { Position = position, Angle = angle, Shape = shape };

    private static HashSet<(int X, int Y)> CollectVisited(ShapeRasterizer rasterizer, BodyState body, int marginCells = 0)
    {
        var visited = new HashSet<(int, int)>();
        rasterizer.Iterate(body, marginCells, (x, y) => visited.Add((x, y)));
        return visited;
    }

    private static void AssertBounds(int minX, int minY, int maxX, int maxY, ChunkBounds actual)
    {
        Assert.Equal(minX, actual.MinX);
        Assert.Equal(minY, actual.MinY);
        Assert.Equal(maxX, actual.MaxX);
        Assert.Equal(maxY, actual.MaxY);
    }

    private static void AssertVectorApprox(float expectedX, float expectedY, Vector2? actual, float tolerance = 1e-4f)
    {
        Assert.NotNull(actual);
        Assert.True(MathF.Abs(actual!.Value.X - expectedX) < tolerance, $"X: expected {expectedX}, got {actual.Value.X}");
        Assert.True(MathF.Abs(actual.Value.Y - expectedY) < tolerance, $"Y: expected {expectedY}, got {actual.Value.Y}");
    }
}
