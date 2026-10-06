namespace Zand.App.Scenarios;

using System;
using System.Numerics;
using Zand.App.Config;
using Zand.Core;
using Zand.Core.CellularAutomata;
using Zand.Core.Coupling;
using Zand.Core.RigidBody;

public static class ScenarioHelpers
{
    // Densities are relative to water's 800 kg/m^3 (PhysicalProperties.DensityKgM3):
    // the triangle floats (600 < 800), the box sinks (1000 > 800).
    internal const float TriangleDensity = 600f;
    internal const float BoxDensity = 1000f;

    // Same density as the box, so the circle sinks in water too.
    internal const float CircleDensity = 1000f;
    internal const float CircleRadius = 2f;

    internal const int FloorThicknessCells = 6;

    internal const int WallLeftOffsetCells = 25;
    internal const int WallRightOffsetCells = 175;

    internal static readonly float CellToMeter =
        SimConstants.CellSize / SimConstants.PixelsPerMeter;

    private const int SmallScenarioWidthCells = 200;
    private const int SmallScenarioHeightCells = 120;

    public static ScenarioGeometry BuildGeometry(WorldConfig worldConfig)
    {
        int originX = (worldConfig.Width - SmallScenarioWidthCells) / 2;
        int originY = worldConfig.Height - FloorThicknessCells - SmallScenarioHeightCells;
        float physWorldCenterX = worldConfig.Width * CellToMeter / 2f;
        float physScenarioLeftX = originX * CellToMeter;
        float physScenarioBottomY = (worldConfig.Height - originY - SmallScenarioHeightCells) * CellToMeter;
        float physScenarioTopY = (worldConfig.Height - originY) * CellToMeter;
        var rasterizer = new ShapeRasterizer(SimConstants.PixelsPerMeter / SimConstants.CellSize, worldConfig.Height);

        return new ScenarioGeometry(
            originX, originY, physWorldCenterX, physScenarioLeftX, physScenarioBottomY, physScenarioTopY, rasterizer);
    }

    [ScenarioElement]
    public static void AddSandPile(ScenarioContext ctx)
    {
        int originX = ctx.Geometry.OriginX;
        int originY = ctx.Geometry.OriginY;
        int cx = originX + (SmallScenarioWidthCells / 2);
        int maxHeight = SmallScenarioHeightCells / 3;
        float sigma = SmallScenarioWidthCells / 4f;
        var rng = new Random(42);

        for (int x = originX; x < originX + SmallScenarioWidthCells; x++)
        {
            float dx = x - cx;
            int h = (int)(maxHeight * Math.Exp(-0.5f * (dx / sigma) * (dx / sigma)));
            h += rng.Next(0, 4);

            for (int i = 0; i < h; i++)
            {
                int y = originY + SmallScenarioHeightCells - 1 - i;
                if (y >= originY)
                {
                    ctx.Grid.SetCell(x, y, new Cell { Type = CellType.Sand });
                }
            }
        }
    }

    [ScenarioElement]
    public static void AddSandRain(ScenarioContext ctx)
    {
        const int Count = 1000;
        var rng = new Random(42);
        int originX = ctx.Geometry.OriginX;
        int originY = ctx.Geometry.OriginY;
        int topThird = SmallScenarioHeightCells / 3;

        for (int i = 0; i < Count; i++)
        {
            int x = rng.Next(originX, originX + SmallScenarioWidthCells);
            int y = rng.Next(originY, originY + topThird);
            ctx.Grid.SetCell(x, y, new Cell { Type = CellType.Sand });
        }
    }

    [ScenarioElement]
    public static void AddSandBall(ScenarioContext ctx)
    {
        int originX = ctx.Geometry.OriginX;
        int originY = ctx.Geometry.OriginY;
        int radius = SmallScenarioHeightCells / 8;
        int cx = originX + (SmallScenarioWidthCells / 2);
        int cy = originY + (SmallScenarioHeightCells / 6);

        int x0 = Math.Max(originX, cx - radius);
        int x1 = Math.Min(originX + SmallScenarioWidthCells - 1, cx + radius);
        int y0 = Math.Max(originY, cy - radius);
        int y1 = Math.Min(originY + SmallScenarioHeightCells - 1, cy + radius);

        for (int y = y0; y <= y1; y++)
        {
            for (int x = x0; x <= x1; x++)
            {
                int dx = x - cx;
                int dy = y - cy;
                if ((dx * dx) + (dy * dy) <= radius * radius)
                {
                    ctx.Grid.SetCell(x, y, new Cell { Type = CellType.Sand });
                }
            }
        }
    }

    [ScenarioElement]
    public static void AddSandBase(ScenarioContext ctx)
    {
        int originX = ctx.Geometry.OriginX;
        int originY = ctx.Geometry.OriginY;
        int innerOriginX = originX + 52;
        int innerEndX = originX + 147;
        int startY = originY + (SmallScenarioHeightCells * 3 / 4);
        for (int y = startY; y < originY + SmallScenarioHeightCells; y++)
        {
            for (int x = innerOriginX; x <= innerEndX; x++)
            {
                ctx.Grid.SetCell(x, y, new Cell { Type = CellType.Sand });
            }
        }
    }

    // A ramp of sand between the positions of the two walls:
    // high against the left wall, descending to near ground the right wall.
    [ScenarioElement]
    public static void AddSandSlope(ScenarioContext ctx)
    {
        const int MaxHeightCells = 45;
        int innerLeftX = ctx.Geometry.OriginX + WallLeftOffsetCells + 2;
        int innerRightX = ctx.Geometry.OriginX + WallRightOffsetCells - 2;
        int floorY = ctx.Grid.Height - FloorThicknessCells;

        for (int x = innerLeftX; x <= innerRightX; x++)
        {
            float t = (x - innerLeftX) / (float)(innerRightX - innerLeftX);
            int height = (int)MathF.Round(MaxHeightCells * (1f - t));
            for (int i = 1; i <= height; i++)
            {
                ctx.Grid.SetCell(x, floorY - i, new Cell { Type = CellType.Sand });
            }
        }
    }

    [ScenarioElement]
    public static void AddWaterPool(ScenarioContext ctx)
    {
        int originX = ctx.Geometry.OriginX;
        int originY = ctx.Geometry.OriginY;
        int innerOriginX = originX + 52;
        int innerEndX = originX + 147;
        int startY = originY + (SmallScenarioHeightCells * 9 / 10);
        for (int y = startY; y < originY + SmallScenarioHeightCells; y++)
        {
            for (int x = innerOriginX; x <= innerEndX; x++)
            {
                ctx.Grid.SetCell(x, y, new Cell { Type = CellType.Water });
            }
        }
    }

    [ScenarioElement]
    public static void AddWaterRain(ScenarioContext ctx)
    {
        const int Count = 1000;
        var rng = new Random(42);
        int originX = ctx.Geometry.OriginX;
        int originY = ctx.Geometry.OriginY;
        int innerOriginX = originX + 52;
        int innerEndX = originX + 147;
        int topHalf = SmallScenarioHeightCells / 2;

        for (int i = 0; i < Count; i++)
        {
            int x = rng.Next(innerOriginX, innerEndX + 1);
            int y = rng.Next(originY, originY + topHalf);
            ctx.Grid.SetCell(x, y, new Cell { Type = CellType.Water });
        }
    }

    [ScenarioElement]
    public static void AddBoxBottom(ScenarioContext ctx)
    {
        float x = ctx.Geometry.PhysWorldCenterX, y = ctx.Geometry.PhysScenarioBottomY + 2f;
        ClearCellsUnderBody(ctx, x, y, new BoxShape(2f, 2f));
        var handle = ctx.Physics.AddDynamicBox(x, y, 2f, 2f, BoxDensity);
        ApplyInitialVelocity(ctx, handle);
    }

    [ScenarioElement]
    public static void AddBoxTop(ScenarioContext ctx)
    {
        float x = ctx.Geometry.PhysWorldCenterX, y = ctx.Geometry.PhysScenarioTopY - 3f;
        ClearCellsUnderBody(ctx, x, y, new BoxShape(2f, 2f));
        var handle = ctx.Physics.AddDynamicBox(x, y, 2f, 2f, BoxDensity);
        ApplyInitialVelocity(ctx, handle);
    }

    [ScenarioElement]
    public static void AddCircleTop(ScenarioContext ctx)
    {
        float x = ctx.Geometry.PhysWorldCenterX, y = ctx.Geometry.PhysScenarioTopY - 3f;
        ClearCellsUnderBody(ctx, x, y, new CircleShape(CircleRadius));
        var handle = ctx.Physics.AddDynamicCircle(x, y, CircleRadius, CircleDensity);
        ApplyInitialVelocity(ctx, handle);
    }

    [ScenarioElement]
    public static void AddHeavySlantedBoxTop(ScenarioContext ctx)
    {
        const float ThirtyDegrees = MathF.PI / 6f;
        float x = ctx.Geometry.PhysWorldCenterX, y = ctx.Geometry.PhysScenarioTopY - 3f;
        ClearCellsUnderBody(ctx, x, y, new BoxShape(2f, 2f), angle: ThirtyDegrees);
        var handle = ctx.Physics.AddDynamicBox(x, y, 2f, 2f, BoxDensity * 2f, angle: ThirtyDegrees);
        ApplyInitialVelocity(ctx, handle);
    }

    [ScenarioElement]
    public static void AddTriangleBottom(ScenarioContext ctx)
    {
        float x = ctx.Geometry.PhysWorldCenterX, y = ctx.Geometry.PhysScenarioBottomY + 2f;
        ClearCellsUnderBody(ctx, x, y, new PolygonShape(TriangleVerts()));
        var handle = ctx.Physics.AddDynamicPolygon(x, y, TriangleVerts(), TriangleDensity);
        ApplyInitialVelocity(ctx, handle);
    }

    [ScenarioElement]
    public static void AddTriangleTop(ScenarioContext ctx)
    {
        float x = ctx.Geometry.PhysWorldCenterX, y = ctx.Geometry.PhysScenarioTopY - 3f;
        ClearCellsUnderBody(ctx, x, y, new PolygonShape(TriangleVerts()));
        var handle = ctx.Physics.AddDynamicPolygon(x, y, TriangleVerts(), TriangleDensity);
        ApplyInitialVelocity(ctx, handle);
    }

    [ScenarioElement]
    public static void AddTriangleInSand(ScenarioContext ctx)
    {
        float x = ctx.Geometry.PhysWorldCenterX, y = ctx.Geometry.PhysScenarioBottomY + 4f;

        ClearCellsUnderBody(ctx, x, y, new PolygonShape(TriangleVerts()));
        var handle = ctx.Physics.AddDynamicPolygon(x, y, TriangleVerts(), TriangleDensity);
        ApplyInitialVelocity(ctx, handle);
    }

    // todo: I don't think this is working correctly? I think that the margin on ClearCellsUnderBody might be wiping it out?
    [ScenarioElement]
    public static void AddWaterTowersAgainstWalls(ScenarioContext ctx)
    {
        const int TowerHeightCells = 24;
        int originX = ctx.Geometry.OriginX;
        int floorY = ctx.Grid.Height - FloorThicknessCells - 1;
        AddWaterTower(ctx, originX + 27, floorY, TowerHeightCells);
        AddWaterTower(ctx, originX + 173, floorY, TowerHeightCells);
    }

    [ScenarioElement]
    public static void AddVerticalWalls(ScenarioContext ctx)
    {
        float halfW = 1f * CellToMeter;
        float halfH = 35f * CellToMeter;

        // Bottom edge (centerY - halfH) sits flush on PhysScenarioBottomY, i.e. the floor's top surface.
        float centerY = ctx.Geometry.PhysScenarioBottomY + halfH;
        float leftX = ctx.Geometry.PhysScenarioLeftX + (WallLeftOffsetCells * CellToMeter);
        float rightX = ctx.Geometry.PhysScenarioLeftX + (WallRightOffsetCells * CellToMeter);
        var shape = new BoxShape(halfW, halfH);
        ClearCellsUnderBody(ctx, leftX, centerY, shape);
        ClearCellsUnderBody(ctx, rightX, centerY, shape);
        ctx.Physics.AddStaticBox(leftX, centerY, halfW, halfH);
        ctx.Physics.AddStaticBox(rightX, centerY, halfW, halfH);
    }

    // Always used in ScenarioRunner, for all scenarios.
    // Sets the same floor for both CA and RB.
    public static void AddFloor(ScenarioContext ctx)
    {
        for (int y = ctx.Grid.Height - FloorThicknessCells; y < ctx.Grid.Height; y++)
        {
            for (int x = 0; x < ctx.Grid.Width; x++)
            {
                ctx.Grid.SetCell(x, y, new Cell { Type = CellType.Static });
            }
        }

        const float floorHalfHeight = 10f;
        float physWorldCenterX = ctx.Geometry.PhysWorldCenterX;
        ctx.Physics.AddStaticBox(
            physWorldCenterX, ctx.Geometry.PhysScenarioBottomY - floorHalfHeight, physWorldCenterX, floorHalfHeight);
    }

    internal static Vector2[] TriangleVerts() => new[]
    {
        new Vector2(0f, 2f),
        new Vector2(-2f, -1f),
        new Vector2(2f, -1f)
    };

    internal static void ApplyInitialVelocity(ScenarioContext ctx, BodyHandle handle)
    {
        if (ctx.InitialFallVelocity != 0f)
        {
            ctx.Physics.SetLinearVelocity(handle, new Vector2(0f, ctx.InitialFallVelocity));
        }
    }

    internal static void ClearCellsUnderBody(ScenarioContext ctx, float x, float y, BodyShape shape, int margin = 2, float angle = 0f)
    {
        var state = new BodyState { Position = new Vector2(x, y), Angle = angle, Shape = shape };
        ctx.Geometry.Rasterizer.Iterate(state, margin, (cx, cy) =>
        {
            if (ctx.Grid.InBounds(cx, cy))
            {
                ctx.Grid.SetCell(cx, cy, new Cell { Type = CellType.Empty });
            }
        });
    }

    private static void AddWaterTower(ScenarioContext ctx, int x, int floorY, int heightCells)
    {
        for (int i = 0; i < heightCells; i++)
        {
            ctx.Grid.SetCell(x, floorY - i, new Cell { Type = CellType.Water });
        }
    }
}
