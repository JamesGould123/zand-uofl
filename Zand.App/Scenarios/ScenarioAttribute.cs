namespace Zand.App.Scenarios;

using System;

[AttributeUsage(AttributeTargets.Method)]
public sealed class ScenarioAttribute : Attribute
{
    public ScenarioAttribute(int frameBudget, params string[] tags)
    {
        FrameBudget = frameBudget;
        Tags = tags;
    }

    public int FrameBudget { get; }

    public string[] Tags { get; }
}
