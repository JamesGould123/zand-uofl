namespace Zand.Core.CellularAutomata;

public static class PhysicalProperties
{
    public static float DensityKgM3(CellType type) => type switch
    {
        CellType.Water => 800f,
        CellType.Sand => 1600f,
        _ => 0f
    };

    public static float CollisionProbability(CellType type) => type switch
    {
        CellType.Water => 0.4f,
        _ => 0f
    };

    public static float DampingStrength(CellType type) => type switch
    {
        CellType.Water => 0.05f,
        _ => 0f
    };
}
