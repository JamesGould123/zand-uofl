namespace Zand.Core.CellularAutomata;

public static class ElementProperties
{
    // Uses (int)CellType rather than a switch statement:
    // a switch with enum compiles to a jump table,
    // which costs an indirect jump on every call.
    // This approach was faster in testing even for a small enum,
    // improvement would likely be larger with more supported CellTypes.
    // Citation: Cagigas-Muñiz, D., Diaz-del-Rio, F., Sevillano-Ramos, J. L., & Guisado-Lizar, J.-L. (2022).
    // Efficient simulation execution of cellular automata on GPU. Simulation Modelling Practice and Theory,
    // 118, 102519. Sec. 5.1 (Look-up tables).
    private static readonly int[] Densities = BuildDensities();
    private static readonly ParticleCategory?[] Categories = BuildCategories();

    public static int GetDensity(CellType type) => Densities[(int)type];

    public static ParticleCategory? GetCategory(CellType type) => Categories[(int)type];

    private static int[] BuildDensities()
    {
        var table = new int[4];
        table[(int)CellType.Empty] = 0;
        table[(int)CellType.Water] = 50;
        table[(int)CellType.Sand] = 100;
        table[(int)CellType.Static] = int.MaxValue;
        return table;
    }

    private static ParticleCategory?[] BuildCategories()
    {
        var table = new ParticleCategory?[4];
        table[(int)CellType.Sand] = ParticleCategory.Powder;
        table[(int)CellType.Water] = ParticleCategory.Liquid;
        table[(int)CellType.Static] = ParticleCategory.Static;
        return table;
    }
}
