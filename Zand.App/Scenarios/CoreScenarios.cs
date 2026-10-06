namespace Zand.App.Scenarios;

using System;
using System.Numerics;
using Zand.Core.CellularAutomata;
using Zand.Core.RigidBody;

public static class CoreScenarios
{
    private const int WallEdgeMarginCells = 2;
    private const int WaterDepthCells = 10;
    private const int SandDepthCells = 10;
    private const int SpawnHeightAboveStackCells = 10;
    private const int WaterBallRadiusCells = 8;

    private const int PyramidHeightCells = 25;

    private const float PyramidSlopeRatio = 1.5f;

    private const int WaterPoolDepthCells = 40;
    private const int BodyDropCount = 5;
    private const int BodySpawnAboveWaterCells = 15;

    // The speed and launch angle (down and left) for the box in HeavyDiagonalBoxIntoSandPyramid.
    private const float DiagonalBoxSpeed = 25f;
    private const float DiagonalBoxAngleRadians = 5f * MathF.PI / 4f;

    // Box's starting location relative to the pyramid's tip -
    // the intention is for the box to collide near the tip of the pyramid,
    // as this is the easiest simple setup where ballistic particles are always demonstrated (when enabled).
    private const int DiagonalBoxOffsetXCells = 35;
    private const int DiagonalBoxOffsetYCells = 40;

    [Scenario(100, ScenarioTags.Sand, ScenarioTags.Water, ScenarioTags.RigidBody)]
    [CoreScenario]
    public static void TriangleAndWaterBallOntoSandPool(ScenarioContext ctx)
    {
        ScenarioHelpers.AddVerticalWalls(ctx);

        int originX = ctx.Geometry.OriginX;
        int floorSurfaceY = ctx.Grid.Height - ScenarioHelpers.FloorThicknessCells;
        int fillLeftX = originX + ScenarioHelpers.WallLeftOffsetCells + WallEdgeMarginCells;
        int fillRightX = originX + ScenarioHelpers.WallRightOffsetCells - WallEdgeMarginCells;

        int sandTopY = floorSurfaceY - SandDepthCells;
        FillRect(ctx, fillLeftX, fillRightX, sandTopY, floorSurfaceY - 1, CellType.Sand);

        int waterTopY = sandTopY - WaterDepthCells;
        FillRect(ctx, fillLeftX, fillRightX, waterTopY, sandTopY - 1, CellType.Water);

        int spawnY = waterTopY - SpawnHeightAboveStackCells;
        int wallSpanCells = ScenarioHelpers.WallRightOffsetCells - ScenarioHelpers.WallLeftOffsetCells;
        int triangleX = originX + ScenarioHelpers.WallLeftOffsetCells + (wallSpanCells / 4);
        int waterBallX = originX + ScenarioHelpers.WallLeftOffsetCells + (wallSpanCells * 3 / 4);

        AddTriangleAtCell(ctx, triangleX, spawnY);
        AddWaterBallAtCell(ctx, waterBallX, spawnY, WaterBallRadiusCells);
    }

    [Scenario(150, ScenarioTags.Sand, ScenarioTags.RigidBody)]
    [CoreScenario]
    public static void SandBallOntoBoxedFloor(ScenarioContext ctx)
    {
        ScenarioHelpers.AddBoxBottom(ctx);
        ScenarioHelpers.AddSandBall(ctx);
    }

    [Scenario(150, ScenarioTags.Sand, ScenarioTags.RigidBody)]
    [CoreScenario]
    public static void HeavyDiagonalBoxIntoSandPyramid(ScenarioContext ctx)
    {
        AddSandPyramid(ctx, PyramidHeightCells);

        int floorSurfaceY = ctx.Grid.Height - ScenarioHelpers.FloorThicknessCells;
        int apexRow = floorSurfaceY - PyramidHeightCells;
        int cx = ctx.Grid.Width / 2;

        AddHeavyDiagonalBox(ctx, cx + DiagonalBoxOffsetXCells, apexRow - DiagonalBoxOffsetYCells);
    }

    // Several bodies (sinking boxes, floating triangles, a sinking circle) land in the same pool at once,
    // so buoyancy/damping compute stats for multiple bodies every tick instead of just one or two.
    [Scenario(150, ScenarioTags.Water, ScenarioTags.RigidBody)]
    [CoreScenario]
    public static void MixedBodiesIntoWaterPool(ScenarioContext ctx)
    {
        ScenarioHelpers.AddVerticalWalls(ctx);

        int originX = ctx.Geometry.OriginX;
        int floorSurfaceY = ctx.Grid.Height - ScenarioHelpers.FloorThicknessCells;
        int fillLeftX = originX + ScenarioHelpers.WallLeftOffsetCells + WallEdgeMarginCells;
        int fillRightX = originX + ScenarioHelpers.WallRightOffsetCells - WallEdgeMarginCells;

        int waterTopY = floorSurfaceY - WaterPoolDepthCells;
        FillRect(ctx, fillLeftX, fillRightX, waterTopY, floorSurfaceY - 1, CellType.Water);

        int spawnY = waterTopY - BodySpawnAboveWaterCells;
        int wallSpanCells = ScenarioHelpers.WallRightOffsetCells - ScenarioHelpers.WallLeftOffsetCells;
        int spacing = wallSpanCells / (BodyDropCount + 1);

        for (int i = 0; i < BodyDropCount; i++)
        {
            int bodyX = originX + ScenarioHelpers.WallLeftOffsetCells + (spacing * (i + 1));
            switch (i % 3)
            {
                case 0:
                    AddBoxAtCell(ctx, bodyX, spawnY);
                    break;
                case 1:
                    AddTriangleAtCell(ctx, bodyX, spawnY);
                    break;
                default:
                    AddCircleAtCell(ctx, bodyX, spawnY);
                    break;
            }
        }
    }

    private static void FillRect(ScenarioContext ctx, int x0, int x1, int y0, int y1, CellType type)
    {
        for (int y = y0; y <= y1; y++)
        {
            for (int x = x0; x <= x1; x++)
            {
                if (ctx.Grid.InBounds(x, y))
                {
                    ctx.Grid.SetCell(x, y, new Cell { Type = type });
                }
            }
        }
    }

    private static void AddTriangleAtCell(ScenarioContext ctx, int cellX, int cellY)
    {
        float x = cellX * ScenarioHelpers.CellToMeter;
        float y = (ctx.Grid.Height - cellY) * ScenarioHelpers.CellToMeter;

        ScenarioHelpers.ClearCellsUnderBody(ctx, x, y, new PolygonShape(ScenarioHelpers.TriangleVerts()));
        var handle = ctx.Physics.AddDynamicPolygon(x, y, ScenarioHelpers.TriangleVerts(), ScenarioHelpers.TriangleDensity);
        ScenarioHelpers.ApplyInitialVelocity(ctx, handle);
    }

    private static void AddBoxAtCell(ScenarioContext ctx, int cellX, int cellY)
    {
        float x = cellX * ScenarioHelpers.CellToMeter;
        float y = (ctx.Grid.Height - cellY) * ScenarioHelpers.CellToMeter;
        var shape = new BoxShape(2f, 2f);

        ScenarioHelpers.ClearCellsUnderBody(ctx, x, y, shape);
        var handle = ctx.Physics.AddDynamicBox(x, y, 2f, 2f, ScenarioHelpers.BoxDensity);
        ScenarioHelpers.ApplyInitialVelocity(ctx, handle);
    }

    private static void AddCircleAtCell(ScenarioContext ctx, int cellX, int cellY)
    {
        float x = cellX * ScenarioHelpers.CellToMeter;
        float y = (ctx.Grid.Height - cellY) * ScenarioHelpers.CellToMeter;
        var shape = new CircleShape(ScenarioHelpers.CircleRadius);

        ScenarioHelpers.ClearCellsUnderBody(ctx, x, y, shape);
        var handle = ctx.Physics.AddDynamicCircle(x, y, ScenarioHelpers.CircleRadius, ScenarioHelpers.CircleDensity);
        ScenarioHelpers.ApplyInitialVelocity(ctx, handle);
    }

    private static void AddWaterBallAtCell(ScenarioContext ctx, int centerX, int centerY, int radius)
    {
        for (int y = centerY - radius; y <= centerY + radius; y++)
        {
            for (int x = centerX - radius; x <= centerX + radius; x++)
            {
                int dx = x - centerX;
                int dy = y - centerY;
                if (ctx.Grid.InBounds(x, y) && (dx * dx) + (dy * dy) <= radius * radius)
                {
                    ctx.Grid.SetCell(x, y, new Cell { Type = CellType.Water });
                }
            }
        }
    }

    private static void AddSandPyramid(ScenarioContext ctx, int apexHeightCells)
    {
        int floorSurfaceY = ctx.Grid.Height - ScenarioHelpers.FloorThicknessCells;
        int cx = ctx.Grid.Width / 2;
        int halfBase = (int)(apexHeightCells * PyramidSlopeRatio);

        for (int x = cx - halfBase; x <= cx + halfBase; x++)
        {
            int dx = Math.Abs(x - cx);
            int height = apexHeightCells - (int)(dx / PyramidSlopeRatio);
            if (height <= 0)
            {
                continue;
            }

            for (int i = 0; i < height; i++)
            {
                int y = floorSurfaceY - 1 - i;
                if (ctx.Grid.InBounds(x, y))
                {
                    ctx.Grid.SetCell(x, y, new Cell { Type = CellType.Sand });
                }
            }
        }
    }

    private static void AddHeavyDiagonalBox(ScenarioContext ctx, int startCellX, int startCellY)
    {
        float x = startCellX * ScenarioHelpers.CellToMeter;
        float y = (ctx.Grid.Height - startCellY) * ScenarioHelpers.CellToMeter;
        var shape = new BoxShape(2f, 2f);

        ScenarioHelpers.ClearCellsUnderBody(ctx, x, y, shape);
        var handle = ctx.Physics.AddDynamicBox(x, y, 2f, 2f, ScenarioHelpers.BoxDensity * 2f);

        var velocity = new Vector2(MathF.Cos(DiagonalBoxAngleRadians), MathF.Sin(DiagonalBoxAngleRadians)) * DiagonalBoxSpeed;
        ctx.Physics.SetLinearVelocity(handle, velocity);
    }
}
