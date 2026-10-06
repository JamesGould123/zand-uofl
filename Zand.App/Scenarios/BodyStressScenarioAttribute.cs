namespace Zand.App.Scenarios;

using System;

// Marks a scenario as the one used when a CoreScenariosReasonable run is switched to the body stress scenario.
// These scenarios are built around a large number of rigid bodies rather than a large number of particles,
// demonstrating more clearly the effects of the per-body optimizations such as rasterization and ballistic
// resolution parallelism.
[AttributeUsage(AttributeTargets.Method)]
public sealed class BodyStressScenarioAttribute : Attribute
{
}
