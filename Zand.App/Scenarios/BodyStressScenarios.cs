namespace Zand.App.Scenarios;

using System;
using System.Numerics;
using Zand.Core.CellularAutomata;
using Zand.Core.RigidBody;

// The heavy scenario stresses the CA with hundreds of thousands of particles but only a dozen bodies,
// so the per-body work (rasterizing footprints, temp bodies, ballistic ejection) is a small part of each frame.
// This scenario flips that around: a hundred bodies of mixed shapes and sizes, a few of them very large,
// dropped a short distance onto piles of sand that start at rest.
public static class BodyStressScenarios
{
    private const int BodyCount = 100;
    private const int LargeBodyCount = 4;

    // 100 x 100 cells at 5 cells per meter.
    private const float LargeBoxHalfExtent = 10f;

    private const float MinSmallHalfExtent = 1f;
    private const float MaxSmallHalfExtent = 3.5f;

    private const int SandBaseCells = 8;
    private const int PileHalfWidthCells = 64;

    private const int HorizontalGapCells = 4;
    private const int DropGapCells = 8;
    private const int WallThicknessCells = 4;
    private const int SideMarginCells = WallThicknessCells + 4;

    private enum BodyKind
    {
        Box,
        Circle,
        Triangle
    }

    [Scenario(240, ScenarioTags.Sand, ScenarioTags.RigidBody, ScenarioTags.Heavy)]
    [BodyStressScenario]
    public static void ManyBodiesOntoSandPiles(ScenarioContext ctx)
    {
        int floorY = ctx.Grid.Height - ScenarioHelpers.FloorThicknessCells;
        AddSideWalls(ctx);
        int[] surfaceY = AddSandPiles(ctx, floorY);
        AddBodies(ctx, surfaceY);
    }

    // Full-height static walls at both edges of the world, prevents RBs from escaping
    private static void AddSideWalls(ScenarioContext ctx)
    {
        int width = ctx.Grid.Width;
        int height = ctx.Grid.Height;
        for (int y = 0; y < height; y++)
        {
            for (int i = 0; i < WallThicknessCells; i++)
            {
                ctx.Grid.SetCell(i, y, new Cell { Type = CellType.Static });
                ctx.Grid.SetCell(width - 1 - i, y, new Cell { Type = CellType.Static });
            }
        }

        float halfWidth = WallThicknessCells / 2f * ScenarioHelpers.CellToMeter;
        float halfHeight = height / 2f * ScenarioHelpers.CellToMeter;
        ctx.Physics.AddStaticBox(halfWidth, halfHeight, halfWidth, halfHeight);
        ctx.Physics.AddStaticBox((width * ScenarioHelpers.CellToMeter) - halfWidth, halfHeight, halfWidth, halfHeight);
    }

    private static int[] AddSandPiles(ScenarioContext ctx, int floorY)
    {
        int width = ctx.Grid.Width;
        int[] surfaceY = new int[width];
        for (int x = WallThicknessCells; x < width - WallThicknessCells; x++)
        {
            int distanceFromPeak = Math.Abs((x % (PileHalfWidthCells * 2)) - PileHalfWidthCells);
            int height = SandBaseCells + (PileHalfWidthCells - distanceFromPeak);
            for (int i = 1; i <= height; i++)
            {
                ctx.Grid.SetCell(x, floorY - i, new Cell { Type = CellType.Sand });
            }

            surfaceY[x] = floorY - height;
        }

        return surfaceY;
    }

    private static void AddBodies(ScenarioContext ctx, int[] surfaceY)
    {
        var rng = new Random(42);
        int width = ctx.Grid.Width;
        int[] skylineY = (int[])surfaceY.Clone();
        float cellsPerMeter = 1f / ScenarioHelpers.CellToMeter;
        int cursorX = SideMarginCells;

        for (int i = 0; i < BodyCount; i++)
        {
            var body = NextBody(rng, i);
            int widthCells = (int)MathF.Ceiling(body.HalfWidth * 2f * cellsPerMeter);
            int heightCells = (int)MathF.Ceiling(body.HalfHeight * 2f * cellsPerMeter);

            if (cursorX + widthCells > width - SideMarginCells)
            {
                cursorX = SideMarginCells;
            }

            int highestBelow = int.MaxValue;
            for (int x = cursorX; x < cursorX + widthCells; x++)
            {
                highestBelow = Math.Min(highestBelow, skylineY[x]);
            }

            int bottomY = highestBelow - DropGapCells;
            int topY = bottomY - heightCells;
            for (int x = cursorX; x < cursorX + widthCells; x++)
            {
                skylineY[x] = topY;
            }

            float centerCellX = cursorX + (widthCells / 2f);
            float centerCellY = topY + (heightCells / 2f);
            AddBody(ctx, body, centerCellX, centerCellY);
            cursorX += widthCells + HorizontalGapCells;
        }
    }

    private static BodySpec NextBody(Random rng, int index)
    {
        if (index % 6 == 1 && index / 6 < LargeBodyCount)
        {
            return new BodySpec(BodyKind.Box, LargeBoxHalfExtent, LargeBoxHalfExtent);
        }

        float size = MinSmallHalfExtent + ((float)rng.NextDouble() * (MaxSmallHalfExtent - MinSmallHalfExtent));
        return rng.Next(3) switch
        {
            0 => new BodySpec(BodyKind.Box, size, size * (0.5f + (float)rng.NextDouble())),
            1 => new BodySpec(BodyKind.Circle, size, size),
            _ => new BodySpec(BodyKind.Triangle, size, size * 0.75f)
        };
    }

    private static void AddBody(ScenarioContext ctx, BodySpec body, float centerCellX, float centerCellY)
    {
        float x = centerCellX * ScenarioHelpers.CellToMeter;
        float y = (ctx.Grid.Height - centerCellY) * ScenarioHelpers.CellToMeter;

        BodyHandle handle;
        switch (body.Kind)
        {
            case BodyKind.Box:
                ScenarioHelpers.ClearCellsUnderBody(ctx, x, y, new BoxShape(body.HalfWidth, body.HalfHeight));
                handle = ctx.Physics.AddDynamicBox(x, y, body.HalfWidth, body.HalfHeight, ScenarioHelpers.BoxDensity);
                break;
            case BodyKind.Circle:
                ScenarioHelpers.ClearCellsUnderBody(ctx, x, y, new CircleShape(body.HalfWidth));
                handle = ctx.Physics.AddDynamicCircle(x, y, body.HalfWidth, ScenarioHelpers.CircleDensity);
                break;
            default:
                var verts = TriangleVerts(body.HalfWidth, body.HalfHeight);
                ScenarioHelpers.ClearCellsUnderBody(ctx, x, y, new PolygonShape(verts));
                handle = ctx.Physics.AddDynamicPolygon(x, y, verts, ScenarioHelpers.TriangleDensity);
                break;
        }

        ScenarioHelpers.ApplyInitialVelocity(ctx, handle);
    }

    // An upward-pointing triangle filling its bounding box, centered on that box.
    private static Vector2[] TriangleVerts(float halfWidth, float halfHeight) =>
    [
        new(0f, halfHeight),
        new(-halfWidth, -halfHeight),
        new(halfWidth, -halfHeight)
    ];

    private readonly record struct BodySpec(BodyKind Kind, float HalfWidth, float HalfHeight);
}
