namespace Zand.Core.Tests.CellularAutomata;

using Xunit;
using Zand.Core.CellularAutomata;

public class ElementPropertiesTests
{
    [Theory]
    [InlineData(CellType.Empty, 0)]
    [InlineData(CellType.Water, 50)]
    [InlineData(CellType.Sand, 100)]
    [InlineData(CellType.Static, int.MaxValue)]
    public void GetDensity_ReturnsExpectedDensityForEachType(CellType type, int expectedDensity)
    {
        Assert.Equal(expectedDensity, ElementProperties.GetDensity(type));
    }

    [Theory]
    [InlineData(CellType.Sand, ParticleCategory.Powder)]
    [InlineData(CellType.Water, ParticleCategory.Liquid)]
    [InlineData(CellType.Static, ParticleCategory.Static)]
    public void GetCategory_ReturnsExpectedCategoryForParticleTypes(CellType type, ParticleCategory expectedCategory)
    {
        Assert.Equal(expectedCategory, ElementProperties.GetCategory(type));
    }

    [Fact]
    public void GetCategory_EmptyCell_ReturnsNull()
    {
        Assert.Null(ElementProperties.GetCategory(CellType.Empty));
    }
}
