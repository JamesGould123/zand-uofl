namespace Zand.Core.Tests.CellularAutomata.Algorithms;

using Xunit;
using Zand.Core.CellularAutomata;
using Zand.Core.CellularAutomata.Algorithms;
using Zand.Core.CellularAutomata.Chunking;

public class PerCellRandomizedAlgorithmTests
{
    [Fact]
    public void Update_PowderCell_DispatchesToPowderElementUnaffectedByRandomization()
    {
        var grid = CreateGrid(3, 3);
        grid.SetCell(1, 1, new Cell { Type = CellType.Sand });

        new PerCellRandomizedAlgorithm(seed: 1).Update(grid, new ChunkBounds(0, 0, 3, 3));

        Assert.Equal(CellType.Empty, grid.GetCell(1, 1).Type);
        Assert.Equal(CellType.Sand, grid.GetCell(1, 2).Type);
    }

    [Fact]
    public void Update_LiquidCell_DispatchesToRandomizedLiquidElement()
    {
        var grid = CreateGrid(3, 3);
        grid.SetCell(1, 1, new Cell { Type = CellType.Water });

        new PerCellRandomizedAlgorithm(seed: 1).Update(grid, new ChunkBounds(0, 0, 3, 3));

        Assert.Equal(CellType.Empty, grid.GetCell(1, 1).Type);
        Assert.Equal(CellType.Water, grid.GetCell(1, 2).Type);
    }

    [Fact]
    public void Update_UnderParallelChunkedGrid_ManyTicksRunWithoutCorruptionOrException()
    {
        const int width = 64;
        const int height = 16;
        var algorithm = new PerCellRandomizedAlgorithm(seed: 1);
        var grid = new CaGrid(width, height, algorithm,
            chunkSize: ChunkSize.Eight,
            parallelismMode: ParallelismMode.Parallel);

        for (int x = 0; x < width; x++)
        {
            grid.SetCell(x, 0, new Cell { Type = CellType.Water });
        }

        var exception = Record.Exception(() =>
        {
            for (int tick = 0; tick < 20; tick++)
            {
                grid.Update();
            }
        });

        Assert.Null(exception);

        int waterCount = 0;
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (grid.GetCell(x, y).Type == CellType.Water)
                {
                    waterCount++;
                }
            }
        }

        Assert.Equal(width, waterCount);
    }

    [Fact]
    public void LeftFirst_SameInputs_GivesSameChoice()
    {
        Assert.Equal(
            PerCellRandomizedAlgorithm.LeftFirst(42, 10, 20, 7),
            PerCellRandomizedAlgorithm.LeftFirst(42, 10, 20, 7));
    }

    [Fact]
    public void LeftFirst_AcrossCellsAndTicks_IsRoughlyEvenlySplit()
    {
        int left = 0;
        int total = 0;
        for (uint tick = 0; tick < 8; tick++)
        {
            for (int x = 0; x < 64; x++)
            {
                for (int y = 0; y < 64; y++)
                {
                    total++;
                    if (PerCellRandomizedAlgorithm.LeftFirst(42, x, y, tick))
                    {
                        left++;
                    }
                }
            }
        }

        double share = (double)left / total;
        Assert.InRange(share, 0.45, 0.55);
    }

    [Fact]
    public void LeftFirst_NeighbouringCellsAndTicks_DoNotAllMatch()
    {
        bool first = PerCellRandomizedAlgorithm.LeftFirst(42, 0, 0, 0);
        bool anyDifferentCell = false;
        bool anyDifferentTick = false;
        for (int i = 1; i < 16; i++)
        {
            anyDifferentCell |= PerCellRandomizedAlgorithm.LeftFirst(42, i, 0, 0) != first;
            anyDifferentTick |= PerCellRandomizedAlgorithm.LeftFirst(42, 0, 0, (uint)i) != first;
        }

        Assert.True(anyDifferentCell);
        Assert.True(anyDifferentTick);
    }

    private static CaGrid CreateGrid(int width, int height) => new(width, height, new PerCellAlgorithm(1));
}
