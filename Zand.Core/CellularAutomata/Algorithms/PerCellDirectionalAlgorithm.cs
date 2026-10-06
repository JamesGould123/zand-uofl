namespace Zand.Core.CellularAutomata.Algorithms;

using Zand.Core.CellularAutomata.Elements;

public class PerCellDirectionalAlgorithm : PerCellAlgorithmBase
{
    private readonly bool[,] _flowsRight;

    public PerCellDirectionalAlgorithm(int width, int height, int seed, RowSweepOrder rowSweepOrder = RowSweepOrder.BottomUp)
        : base(seed, rowSweepOrder)
    {
        _flowsRight = new bool[width, height];
    }

    protected override void UpdateCell(CaGrid grid, int x, int y)
    {
        switch (ElementProperties.GetCategory(grid.GetCell(x, y).Type))
        {
            case ParticleCategory.Powder:
                PowderElement.Update(grid, x, y);
                break;
            case ParticleCategory.Liquid:
                DirectionalLiquidElement.Update(grid, x, y, _flowsRight);
                break;
        }
    }
}
