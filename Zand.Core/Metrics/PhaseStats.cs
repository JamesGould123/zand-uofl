namespace Zand.Core.Metrics;

public readonly record struct PhaseStats(
    double TotalMs,
    double AvgMs,
    double MinMs,
    double MaxMs,
    int Frames);

public sealed class PhaseSnapshot
{
    private readonly PhaseStats[] _stats;

    internal PhaseSnapshot(PhaseStats[] stats) => _stats = stats;

    public PhaseStats Get(SimPhase phase) => _stats[(int)phase];
}
