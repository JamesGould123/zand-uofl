namespace Zand.App.Scenarios;

using System;

// Marks a scenario as one of the scenarios to be used by "Heavy Benchmark"
// These scenarios intend to stress the limits of the system, demonstrating more clearly the benefits of
// optimizations such as Chunking, Active Rectangle, and Parallelization.
[AttributeUsage(AttributeTargets.Method)]
public sealed class HeavyScenarioAttribute : Attribute
{
}
