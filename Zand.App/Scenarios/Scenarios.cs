namespace Zand.App.Scenarios;

public static class Scenarios
{
    /*[Scenario(200, ScenarioTags.Sand, ScenarioTags.Margolus, ScenarioTags.RigidBody)]
    public static void SandScatterTopAndTriangleBottom(ScenarioContext ctx)
    {
        ScenarioHelpers.AddFloor(ctx);
        ScenarioHelpers.AddTriangleBottom(ctx);
        ScenarioHelpers.AddSandRain(ctx);
    }

    [Scenario(200, ScenarioTags.Sand, ScenarioTags.Margolus, ScenarioTags.RigidBody)]
    public static void PileBottomAndTriangleTop(ScenarioContext ctx)
    {
        ScenarioHelpers.AddFloor(ctx);
        ScenarioHelpers.AddTriangleTop(ctx);
        ScenarioHelpers.AddSandPile(ctx);
    }

    [Scenario(200, ScenarioTags.Sand, ScenarioTags.Margolus, ScenarioTags.RigidBody)]
    public static void BallTopAndBoxBottom(ScenarioContext ctx)
    {
        ScenarioHelpers.AddFloor(ctx);
        ScenarioHelpers.AddBoxBottom(ctx);
        ScenarioHelpers.AddSandBall(ctx);
    }*/

    [Scenario(400, ScenarioTags.Sand, ScenarioTags.Water, ScenarioTags.RigidBody)]
    public static void WaterOntoSandWithTriangle(ScenarioContext ctx)
    {
        ScenarioHelpers.AddVerticalWalls(ctx);
        ScenarioHelpers.AddSandBase(ctx);
        ScenarioHelpers.AddTriangleInSand(ctx);
        ScenarioHelpers.AddWaterRain(ctx);
    }

    [Scenario(400, ScenarioTags.Sand, ScenarioTags.Water, ScenarioTags.RigidBody)]
    public static void SandOntoWaterWithTriangle(ScenarioContext ctx)
    {
        ScenarioHelpers.AddVerticalWalls(ctx);
        ScenarioHelpers.AddWaterPool(ctx);
        ScenarioHelpers.AddSandRain(ctx);
        ScenarioHelpers.AddTriangleTop(ctx);
    }

    [Scenario(400, ScenarioTags.Sand, ScenarioTags.RigidBody)]
    public static void CircleOntoSandSlope(ScenarioContext ctx)
    {
        ScenarioHelpers.AddVerticalWalls(ctx);
        ScenarioHelpers.AddSandSlope(ctx);
        ScenarioHelpers.AddCircleTop(ctx);
    }

    [Scenario(400, ScenarioTags.Water, ScenarioTags.RigidBody)]
    public static void WaterTowersAgainstWalls(ScenarioContext ctx)
    {
        ScenarioHelpers.AddVerticalWalls(ctx);
        ScenarioHelpers.AddWaterTowersAgainstWalls(ctx);
    }
}
