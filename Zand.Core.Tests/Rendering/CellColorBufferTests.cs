namespace Zand.Core.Tests.Rendering;

using Microsoft.Xna.Framework;
using Xunit;
using Zand.Core.CellularAutomata;
using Zand.Core.CellularAutomata.Algorithms;
using Zand.Rendering;

public class CellColorBufferTests
{
    private const int Width = 40;
    private const int Height = 30;

    [Theory]
    [InlineData(CellRenderSmoothing.Disabled)]
    [InlineData(CellRenderSmoothing.Smoothing)]
    [InlineData(CellRenderSmoothing.NeighborFill)]
    public void Fill_ParallelMatchesSequential(CellRenderSmoothing smoothing)
    {
        var grid = CreateBusyGrid();

        var sequential = Render(grid, smoothing, RenderParallelism.Sequential);
        var parallel = Render(grid, smoothing, RenderParallelism.Parallel);

        Assert.Equal(sequential, parallel);
    }

    [Fact]
    public void Fill_ParallelMatchesSequential_WhenViewHangsOffGridEdges()
    {
        var grid = CreateBusyGrid();

        var sequential = Render(grid, CellRenderSmoothing.Smoothing, RenderParallelism.Sequential, cellX0: -5, cellY0: -3);
        var parallel = Render(grid, CellRenderSmoothing.Smoothing, RenderParallelism.Parallel, cellX0: -5, cellY0: -3);

        Assert.Equal(sequential, parallel);
        Assert.Equal(Color.Red, sequential[0]);
    }

    [Fact]
    public void Fill_UsesSolidColorPerMaterial()
    {
        var grid = new CaGrid(Width, Height, new PerCellAlgorithm(1));
        grid.SetCell(2, 3, new Cell { Type = CellType.Sand });
        grid.SetCell(3, 3, new Cell { Type = CellType.Water });
        grid.SetCell(4, 3, new Cell { Type = CellType.Static });

        var pixels = Render(grid, CellRenderSmoothing.Disabled, RenderParallelism.Sequential);

        Assert.Equal(new Color(194, 178, 128), pixels[(3 * Width) + 2]);
        Assert.Equal(new Color(64, 164, 223), pixels[(3 * Width) + 3]);
        Assert.Equal(new Color(60, 60, 60), pixels[(3 * Width) + 4]);
    }

    [Fact]
    public void Fill_NeighborFill_PaintsGapBetweenMatchingNeighbors()
    {
        var grid = new CaGrid(Width, Height, new PerCellAlgorithm(1));
        grid.SetCell(5, 5, new Cell { Type = CellType.Water });
        grid.SetCell(7, 5, new Cell { Type = CellType.Water });

        var pixels = Render(grid, CellRenderSmoothing.NeighborFill, RenderParallelism.Sequential);

        Assert.Equal(new Color(64, 164, 223), pixels[(5 * Width) + 6]);
    }

    private static Color[] Render(
        CaGrid grid, CellRenderSmoothing smoothing, RenderParallelism parallelism, int cellX0 = 0, int cellY0 = 0)
    {
        var pixels = new Color[Width * Height];
        CellColorBuffer.Fill(pixels, Width, Height, grid, cellX0, cellY0, smoothing, parallelism);
        return pixels;
    }

    // Creates scattered particles with gaps of every kind (isolated cells, one-cell gaps, columns) so all
    // smoothing modes have something to blend, as well as fractional water fill from liquid pressure.
    private static CaGrid CreateBusyGrid()
    {
        var grid = new CaGrid(
            Width, Height, new PerCellAlgorithm(1), liquidPressureMode: LiquidPressureMode.Enabled);
        var random = new Random(7);
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                switch (random.Next(6))
                {
                    case 0:
                        grid.SetCell(x, y, new Cell { Type = CellType.Sand });
                        break;
                    case 1:
                        grid.SetCell(x, y, new Cell { Type = CellType.Static });
                        break;
                    case 2:
                        grid.SetCell(x, y, new Cell { Type = CellType.Water });
                        grid.SetLiquidFill(x, y, 0.25f + (random.NextSingle() * 0.75f));
                        break;
                }
            }
        }

        return grid;
    }
}
