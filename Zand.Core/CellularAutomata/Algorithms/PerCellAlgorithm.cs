namespace Zand.Core.CellularAutomata.Algorithms;

using Zand.Core.CellularAutomata.Elements;

public class PerCellAlgorithm : PerCellAlgorithmBase
{
    public PerCellAlgorithm(int seed, RowSweepOrder rowSweepOrder = RowSweepOrder.BottomUp)
        : base(seed, rowSweepOrder)
    {
    }

    protected override void UpdateCell(CaGrid grid, int x, int y)
    {
        switch (ElementProperties.GetCategory(grid.GetCell(x, y).Type))
        {
            case ParticleCategory.Powder:
                PowderElement.Update(grid, x, y);
                break;
            case ParticleCategory.Liquid:
                LiquidElement.Update(grid, x, y);
                break;
        }
    }
}
