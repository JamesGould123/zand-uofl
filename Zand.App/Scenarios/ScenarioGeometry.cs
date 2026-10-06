namespace Zand.App.Scenarios;

using Zand.Core.Coupling;

public record ScenarioGeometry(
    int OriginX,
    int OriginY,
    float PhysWorldCenterX,
    float PhysScenarioLeftX,
    float PhysScenarioBottomY,
    float PhysScenarioTopY,
    ShapeRasterizer Rasterizer);
