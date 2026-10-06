namespace Zand.App.Scenarios;

using System;
using System.Numerics;
using Zand.Core.CellularAutomata;
using Zand.Core.RigidBody;

// Chunking/active-rectangle/parallelism don't provide meaningful effects on the small "core" scenarios since they
// don't usually span more than a handful of chunks. This scenario aims to generate pockets of
// sand/water/rigid-body groupings across the full grid, separated by resting areas,
// to demonstrate the effects of these optimizations.
public static class HeavyScenarios
{
    private const int Columns = 4;
    private const int Rows = 3;
    private const int GapCells = 8;
    private const int TopMarginCells = 16;
    private const int LedgeThicknessCells = 3;
    private const int SandRainCountPerZone = 150;
    private const int FallBandCells = 20;

    private const float BoxHalfExtent = 2f;

    // Speed and launch angle (down-left) for the boxes
    private const float DiagonalSpeed = 25f;
    private const float DiagonalAngleRadians = 5f * MathF.PI / 4f;

    [Scenario(225, ScenarioTags.Sand, ScenarioTags.Water, ScenarioTags.RigidBody, ScenarioTags.Heavy)]
    [HeavyScenario]
    public static void HeavyChunkStressGrid(ScenarioContext ctx)
    {
        var rng = new Random(42);
        int usableTop = TopMarginCells;
        int usableBottom = ctx.Grid.Height - ScenarioHelpers.FloorThicknessCells;
        int usableHeight = usableBottom - usableTop;

        int zoneAreaWidth = ctx.Grid.Width - (GapCells * (Columns + 1));
        int zoneWidth = zoneAreaWidth / Columns;
        int zoneAreaHeight = usableHeight - (GapCells * (Rows + 1));
        int zoneHeight = zoneAreaHeight / Rows;

        for (int row = 0; row < Rows; row++)
        {
            for (int col = 0; col < Columns; col++)
            {
                int x0 = GapCells + (col * (zoneWidth + GapCells));
                int x1 = x0 + zoneWidth - 1;
                int y0 = usableTop + GapCells + (row * (zoneHeight + GapCells));
                int y1 = y0 + zoneHeight - 1;

                AddZone(ctx, x0, x1, y0, y1, (row * Columns) + col, rng);
            }
        }
    }

    // Notes on the approach.
    //  - Zones contain an off-center static ledge (helps to speed up partial settling)
    //  - Only one contains water (as it doesn't settle quickly or fully)
    //  - All contain sand/rigid bodies
    private static void AddZone(ScenarioContext ctx, int x0, int x1, int y0, int y1, int zoneIndex, Random rng)
    {
        int zoneHeight = y1 - y0 + 1;
        int zoneCenterX = (x0 + x1) / 2;

        int ledgeY = y0 + (zoneHeight * 3 / 5);
        bool ledgeOnLeft = zoneIndex % 2 == 0;
        int ledgeX0 = ledgeOnLeft ? x0 : zoneCenterX;
        int ledgeX1 = ledgeOnLeft ? zoneCenterX - 1 : x1;
        int ledgeCenterX = (ledgeX0 + ledgeX1) / 2;
        AddLedge(ctx, ledgeX0, ledgeX1, ledgeY);

        switch (zoneIndex % 4)
        {
            case 0:
                AddSandRain(ctx, ledgeX0, ledgeX1, ledgeY - FallBandCells, ledgeY - 1, rng);
                AddBox(ctx, ledgeCenterX, ledgeY - FallBandCells);
                break;
            case 1:
                AddSandRain(ctx, ledgeX0, ledgeX1, ledgeY - FallBandCells, ledgeY - 1, rng);
                AddTriangle(ctx, ledgeCenterX, ledgeY - FallBandCells);
                break;
            case 2:
                int waterTop = y1 - (zoneHeight / 4);
                AddWaterPool(ctx, x0, x1, waterTop, y1);
                AddTriangle(ctx, zoneCenterX, waterTop - FallBandCells);
                break;
            default:
                AddSandRain(ctx, ledgeX0, ledgeX1, ledgeY - FallBandCells, ledgeY - 1, rng);
                AddDiagonalBox(ctx, ledgeCenterX + FallBandCells, ledgeY - FallBandCells);
                break;
        }
    }

    private static void AddLedge(ScenarioContext ctx, int x0, int x1, int y)
    {
        for (int cy = y; cy < y + LedgeThicknessCells; cy++)
        {
            for (int cx = x0; cx <= x1; cx++)
            {
                if (ctx.Grid.InBounds(cx, cy))
                {
                    ctx.Grid.SetCell(cx, cy, new Cell { Type = CellType.Static });
                }
            }
        }

        float halfWidth = (x1 - x0 + 1) / 2f * ScenarioHelpers.CellToMeter;
        float halfHeight = LedgeThicknessCells / 2f * ScenarioHelpers.CellToMeter;
        var center = CellToPhys(ctx, (x0 + x1) / 2f, y + ((LedgeThicknessCells - 1) / 2f));
        ctx.Physics.AddStaticBox(center.X, center.Y, halfWidth, halfHeight);
    }

    private static void AddSandRain(ScenarioContext ctx, int x0, int x1, int y0, int y1, Random rng)
    {
        for (int i = 0; i < SandRainCountPerZone; i++)
        {
            int x = rng.Next(x0, x1 + 1);
            int y = rng.Next(y0, y1 + 1);
            if (ctx.Grid.InBounds(x, y))
            {
                ctx.Grid.SetCell(x, y, new Cell { Type = CellType.Sand });
            }
        }
    }

    private static void AddWaterPool(ScenarioContext ctx, int x0, int x1, int y0, int y1)
    {
        for (int y = y0; y <= y1; y++)
        {
            for (int x = x0; x <= x1; x++)
            {
                if (ctx.Grid.InBounds(x, y))
                {
                    ctx.Grid.SetCell(x, y, new Cell { Type = CellType.Water });
                }
            }
        }
    }

    private static void AddBox(ScenarioContext ctx, int cellX, int cellY)
    {
        var pos = CellToPhys(ctx, cellX, cellY);
        var shape = new BoxShape(BoxHalfExtent, BoxHalfExtent);
        ScenarioHelpers.ClearCellsUnderBody(ctx, pos.X, pos.Y, shape);
        var handle = ctx.Physics.AddDynamicBox(pos.X, pos.Y, BoxHalfExtent, BoxHalfExtent, ScenarioHelpers.BoxDensity);
        ScenarioHelpers.ApplyInitialVelocity(ctx, handle);
    }

    private static void AddTriangle(ScenarioContext ctx, int cellX, int cellY)
    {
        var pos = CellToPhys(ctx, cellX, cellY);
        var verts = ScenarioHelpers.TriangleVerts();
        ScenarioHelpers.ClearCellsUnderBody(ctx, pos.X, pos.Y, new PolygonShape(verts));
        var handle = ctx.Physics.AddDynamicPolygon(pos.X, pos.Y, verts, ScenarioHelpers.TriangleDensity);
        ScenarioHelpers.ApplyInitialVelocity(ctx, handle);
    }

    private static void AddDiagonalBox(ScenarioContext ctx, int cellX, int cellY)
    {
        var pos = CellToPhys(ctx, cellX, cellY);
        var shape = new BoxShape(BoxHalfExtent, BoxHalfExtent);
        ScenarioHelpers.ClearCellsUnderBody(ctx, pos.X, pos.Y, shape);
        var handle = ctx.Physics.AddDynamicBox(pos.X, pos.Y, BoxHalfExtent, BoxHalfExtent, ScenarioHelpers.BoxDensity * 2f);

        var velocity = new Vector2(MathF.Cos(DiagonalAngleRadians), MathF.Sin(DiagonalAngleRadians)) * DiagonalSpeed;
        ctx.Physics.SetLinearVelocity(handle, velocity);
    }

    private static Vector2 CellToPhys(ScenarioContext ctx, float cellX, float cellY)
        => new(cellX * ScenarioHelpers.CellToMeter, (ctx.Grid.Height - cellY) * ScenarioHelpers.CellToMeter);
}
