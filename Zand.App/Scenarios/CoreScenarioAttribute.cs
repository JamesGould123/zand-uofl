namespace Zand.App.Scenarios;

using System;

// Marks a scenario as one of the scenarios to be used by "Core scenarios with all configs"
// These scenarios intend to provide an interesting, but minimal range of demonstrations
// for the main interactions that can occur in the RB + CA coupling system.
[AttributeUsage(AttributeTargets.Method)]
public sealed class CoreScenarioAttribute : Attribute
{
}
