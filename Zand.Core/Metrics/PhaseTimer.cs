namespace Zand.Core.Metrics;

using System.Diagnostics;

public static class PhaseTimer
{
    private static readonly int PhaseCount = Enum.GetValues<SimPhase>().Length;
    private static readonly Stopwatch[] _watches;
    private static readonly double[] _sums;
    private static readonly double[] _mins;
    private static readonly double[] _maxs;
    private static readonly int[] _counts;

    static PhaseTimer()
    {
        _watches = new Stopwatch[PhaseCount];
        _sums = new double[PhaseCount];
        _mins = new double[PhaseCount];
        _maxs = new double[PhaseCount];
        _counts = new int[PhaseCount];
        for (int i = 0; i < PhaseCount; i++)
        {
            _watches[i] = new Stopwatch();
            _mins[i] = double.MaxValue;
        }
    }

    public static bool Enabled { get; set; }

    public static void Begin(SimPhase phase)
    {
        if (!Enabled)
        {
            return;
        }

        _watches[(int)phase].Restart();
    }

    public static void End(SimPhase phase)
    {
        if (!Enabled)
        {
            return;
        }

        int i = (int)phase;
        _watches[i].Stop();
        double ms = _watches[i].Elapsed.TotalMilliseconds;
        _sums[i] += ms;
        _counts[i]++;
        if (ms < _mins[i])
        {
            _mins[i] = ms;
        }

        if (ms > _maxs[i])
        {
            _maxs[i] = ms;
        }
    }

    public static void Reset()
    {
        if (!Enabled)
        {
            return;
        }

        for (int i = 0; i < PhaseCount; i++)
        {
            _sums[i] = 0;
            _counts[i] = 0;
            _mins[i] = double.MaxValue;
            _maxs[i] = 0;
        }
    }

    public static PhaseSnapshot Snapshot()
    {
        var stats = new PhaseStats[PhaseCount];
        for (int i = 0; i < PhaseCount; i++)
        {
            int c = _counts[i];
            stats[i] = new PhaseStats(
                TotalMs: _sums[i],
                AvgMs: c > 0 ? _sums[i] / c : 0,
                MinMs: c > 0 ? _mins[i] : 0,
                MaxMs: _maxs[i],
                Frames: c);
        }

        return new PhaseSnapshot(stats);
    }
}
