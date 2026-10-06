namespace Zand.Core.Tests.CellularAutomata;

using Xunit;
using Zand.Core.CellularAutomata;
using Zand.Core.CellularAutomata.Algorithms;
using Zand.Core.CellularAutomata.Chunking;

public class LiquidPressureTests
{
    [Theory]
    [InlineData(0.25f)]
    [InlineData(0.5f)]
    [InlineData(0.75f)]
    [InlineData(1f)]
    public void Equalize_CellAboveFullyEmptyCell_DrainsCompletelyDownward(float sourceMass)
    {
        var grid = CreateGrid(3, 3);
        grid.SetLiquidFill(1, 1, sourceMass);
        grid.SetCell(0, 1, new Cell { Type = CellType.Static });
        grid.SetCell(2, 1, new Cell { Type = CellType.Static });

        LiquidPressure.Equalize(grid, new ChunkBounds(0, 0, 3, 3));

        AssertEmpty(1, 1, grid);
        AssertWater(sourceMass, 1, 2, grid);
    }

    [Fact]
    public void Equalize_TwoFullStackedCells_OnlySmallCompressionFlowsDownward()
    {
        var grid = CreateGrid(3, 3);
        grid.SetLiquidFill(1, 1, 1f);
        grid.SetLiquidFill(1, 2, 1f);
        grid.SetCell(0, 1, new Cell { Type = CellType.Static });
        grid.SetCell(2, 1, new Cell { Type = CellType.Static });
        grid.SetCell(0, 2, new Cell { Type = CellType.Static });
        grid.SetCell(2, 2, new Cell { Type = CellType.Static });

        LiquidPressure.Equalize(grid, new ChunkBounds(0, 0, 3, 3));

        AssertWaterInRange(0.9f, 0.999f, 1, 1, grid);
        AssertWaterInRange(1.001f, 1.1f, 1, 2, grid);
    }

    [Fact]
    public void Equalize_FullyEnclosedCell_MassUnchanged()
    {
        var grid = CreateGrid(3, 3);
        grid.SetLiquidFill(1, 1, 1f);
        grid.SetCell(0, 1, new Cell { Type = CellType.Static });
        grid.SetCell(2, 1, new Cell { Type = CellType.Static });
        grid.SetCell(1, 2, new Cell { Type = CellType.Static });

        LiquidPressure.Equalize(grid, new ChunkBounds(0, 0, 3, 3));

        AssertWater(1f, 1, 1, grid);
    }

    [Fact]
    public void Equalize_ExcessMassPushedUpwardWhenBlockedBelowAndSides()
    {
        var grid = CreateGrid(3, 3);
        grid.SetLiquidFill(1, 1, 1.5f);
        grid.SetCell(0, 1, new Cell { Type = CellType.Static });
        grid.SetCell(2, 1, new Cell { Type = CellType.Static });
        grid.SetCell(1, 2, new Cell { Type = CellType.Static });

        LiquidPressure.Equalize(grid, new ChunkBounds(0, 0, 3, 3));

        // With all other exits blocked, mass above MaxMass is pushed upwards.
        // The source can never drop below MaxMass, and the cell above can never receive more than
        // the excess (mass - MaxMass).
        AssertWaterInRange(1f, 1.4f, 1, 1, grid);
        AssertWaterInRange(0.1f, 0.5f, 1, 0, grid);
    }

    [Fact]
    public void Equalize_ConservesTotalMassAcrossARegion()
    {
        var grid = CreateGrid(6, 5);
        for (int x = 0; x < 6; x++)
        {
            grid.SetCell(x, 4, new Cell { Type = CellType.Static });
        }

        grid.SetCell(3, 3, new Cell { Type = CellType.Static });
        grid.SetCell(4, 3, new Cell { Type = CellType.Static });
        grid.SetLiquidFill(1, 1, 1f);
        grid.SetLiquidFill(1, 2, 0.5f);
        grid.SetLiquidFill(3, 1, 1.25f);
        grid.SetLiquidFill(4, 2, 0.75f);

        float totalBefore = SumFill(grid);

        LiquidPressure.Equalize(grid, new ChunkBounds(0, 0, 6, 5));

        float totalAfter = SumFill(grid);
        Assert.Equal(totalBefore, totalAfter, precision: 3);
    }

    private static CaGrid CreateGrid(int width, int height) =>
        new(width, height, new PerCellAlgorithm(1), liquidPressureMode: LiquidPressureMode.Enabled);

    private static float SumFill(CaGrid grid)
    {
        float total = 0f;
        for (int y = 0; y < grid.Height; y++)
        {
            for (int x = 0; x < grid.Width; x++)
            {
                total += grid.GetFill(x, y);
            }
        }

        return total;
    }

    private static void AssertWater(float expectedFill, int x, int y, CaGrid grid)
    {
        Assert.Equal(CellType.Water, grid.GetCell(x, y).Type);
        Assert.Equal(expectedFill, grid.GetFill(x, y));
    }

    private static void AssertWaterInRange(float minFill, float maxFill, int x, int y, CaGrid grid)
    {
        Assert.Equal(CellType.Water, grid.GetCell(x, y).Type);
        Assert.InRange(grid.GetFill(x, y), minFill, maxFill);
    }

    private static void AssertEmpty(int x, int y, CaGrid grid)
    {
        Assert.Equal(CellType.Empty, grid.GetCell(x, y).Type);
        Assert.Equal(0f, grid.GetFill(x, y));
    }
}
