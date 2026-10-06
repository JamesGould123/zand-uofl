namespace Zand.App.Metrics;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Zand.App.Scenarios;
using Zand.Core.Metrics;

// Metrics are collected in two files:
// - manifest.csv: A description of the general configuration and metrics for each run of the simulation,
//      one line per run
// - frames: details about every frame for each individual run,
//      can be used to get a more detailed understanding of the performance over time
public class MetricsCollector
{
    // The first frames of a run are much slower than the rest
    // (theory: methods being JIT-compiled on first use, GPU resources being set up by the first draws).
    // For this reason they're kept in the per-frame files but excluded from the manifest's averages,
    // max and spike count.
    public const int WarmupFrames = 5;

    private static readonly string ManifestHeader =
        string.Join(
            ",",
            "run_id", "scenario", "repeat_index", "world_width", "world_height", "ca_algorithm", "rb_simulation",
            "coupling", "liquid_physics", "gravity", "temp_body_lifetime", "temp_body_merging", "chunk_size",
            "active_rectangle", "parallelism", "liquid_pressure", "same_tick_move_guard", "row_sweep_order",
            "cell_render_smoothing", "ballistics", "ejection_resistance",
            "particle_collision", "landing_search", "impulse_target", "deflection", "reattachment",
            "rasterization_body_parallelism", "rasterization_cell_parallelism",
            "ballistic_resolution_parallelism", "render_parallelism", "real_time_playback", "frames", "warmup_frames",
            "avg_frame_ms", "avg_sim_step_ms", "max_frame_ms", "spike_count", "avg_memory_mb", "avg_cpu_pct")
        + "," + string.Join(",", Enum.GetValues<SimPhase>().Select(p => $"phase_{p.ToString().ToLowerInvariant()}_avg_ms"))
        + ",frames_file";

    private readonly Process _process = Process.GetCurrentProcess();
    private readonly bool _realTimePlayback;
    private readonly List<RunData> _completed = [];
    private readonly List<(int RunId, PhaseSnapshot Phases)> _completedPhases = [];
    private readonly List<(int RunId, string Row)> _finalStates = [];
    private RunData? _current;
    private int _nextRunId = 1;

    private int _frameCount;
    private float _cpuPct;
    private DateTime _lastCpuSampleTime = DateTime.UtcNow;
    private TimeSpan _lastCpuTime;

    // realTimePlayback marks every run in the session as capped at 60 FPS (for watching),
    // prevents runs that could have more external variables from being mistaken for benchmark data.
    public MetricsCollector(bool realTimePlayback = false)
    {
        _realTimePlayback = realTimePlayback;
    }

    public int CurrentRunFrameCount => _frameCount;

    public void AddPhaseSnapshot(int runId, PhaseSnapshot snapshot)
    {
        _completedPhases.Add((runId, snapshot));
    }

    // Row is produced by FinalStateRecorder.Capture and belongs to the run currently being recorded.
    public void RecordFinalState(string row)
    {
        if (_current == null)
        {
            return;
        }

        _finalStates.Add((_current.RunId, row));
    }

    public int BeginScenario(RunConfig config)
    {
        if (_current != null)
        {
            _completed.Add(_current);
        }

        int runId = _nextRunId++;
        _current = new RunData(runId, config, []);
        _frameCount = 0;
        _lastCpuSampleTime = DateTime.UtcNow;
        _lastCpuTime = _process.TotalProcessorTime;
        return runId;
    }

    public void RecordFrame(double frameTimeMs, double simStepMs)
    {
        if (_current == null)
        {
            return;
        }

        _frameCount++;
        if (_frameCount % 60 == 0)
        {
            RefreshCpuSample();
        }

        // Environment.WorkingSet only queries this process.
        // compared to Process.Refresh() which snapshots every process on the machine,
        // which would cost several ms per frame and affect the frametime.
        float memMb = Environment.WorkingSet / (1024f * 1024f);
        _current.Frames.Add(new FrameRecord(_frameCount, frameTimeMs, simStepMs, memMb, _cpuPct));
    }

    public void Save(string outputDir)
    {
        if (_current != null)
        {
            _completed.Add(_current);
            _current = null;
        }

        Directory.CreateDirectory(outputDir);
        string framesDir = Path.Combine(outputDir, "frames");
        Directory.CreateDirectory(framesDir);

        var phasesByRun = _completedPhases.ToDictionary(p => p.RunId, p => p.Phases);

        using var manifest = new StreamWriter(Path.Combine(outputDir, "manifest.csv"));
        manifest.WriteLine(ManifestHeader);

        foreach (var run in _completed)
        {
            string frameFileRelative = $"frames/run_{run.RunId:D5}.csv";
            WriteFrameFile(Path.Combine(outputDir, frameFileRelative), run.Frames);

            phasesByRun.TryGetValue(run.RunId, out var phases);
            manifest.WriteLine(BuildManifestRow(run, phases, frameFileRelative, _realTimePlayback));
        }

        using var finalState = new StreamWriter(Path.Combine(outputDir, "final-state.csv"));
        finalState.WriteLine(FinalStateRecorder.Header);
        foreach (var (runId, row) in _finalStates)
        {
            finalState.WriteLine($"{runId},{row}");
        }
    }

    private static void WriteFrameFile(string path, List<FrameRecord> frames)
    {
        using var writer = new StreamWriter(path);
        writer.WriteLine("frame,frame_time_ms,sim_step_ms,memory_mb,cpu_pct,warmup");
        foreach (var f in frames)
        {
            bool warmup = f.Frame <= WarmupFrames;
            writer.WriteLine($"{f.Frame},{f.FrameTimeMs:F4},{f.SimStepMs:F4},{f.MemoryMb:F1},{f.CpuPct:F1},{warmup}");
        }
    }

    private static string BuildManifestRow(RunData run, PhaseSnapshot? phases, string frameFileRelative, bool realTimePlayback)
    {
        double avg = 0;
        double avgSim = 0;
        double max = 0;
        int spikes = 0;
        double avgMem = 0;
        double avgCpu = 0;
        var measured = run.Frames.Skip(WarmupFrames).ToList();
        if (measured.Count > 0)
        {
            avg = measured.Average(f => f.FrameTimeMs);
            avgSim = measured.Average(f => f.SimStepMs);
            max = measured.Max(f => f.FrameTimeMs);
            spikes = measured.Count(f => f.FrameTimeMs > avg * 2.0);
            avgMem = measured.Average(f => f.MemoryMb);
            avgCpu = measured.Average(f => f.CpuPct);
        }

        var c = run.Config;
        string configCols =
            string.Join(
                ",",
                run.RunId, c.ScenarioName, c.RepeatIndex, c.WorldWidth, c.WorldHeight, c.CaAlgorithm, c.RbSimulation,
                c.Coupling, c.LiquidPhysics, c.Gravity, c.TempBodyLifetime, c.TempBodyMerging, c.ChunkSize,
                c.ActiveRectangleMode, c.ParallelismMode, c.LiquidPressureMode, c.SameTickMoveGuard, c.RowSweepOrder,
                c.CellRenderSmoothing, c.BallisticParticleMode,
                c.EjectionResistanceMode, c.ParticleCollisionMode, c.LandingSearchMode,
                c.EjectionImpulseTargetMode, c.EjectionDeflectionMode, c.ReattachmentMode,
                c.RasterizationBodyParallelism, c.RasterizationCellParallelism,
                c.BallisticResolutionParallelism, c.RenderParallelism, realTimePlayback, measured.Count, WarmupFrames,
                avg.ToString("F4"), avgSim.ToString("F4"), max.ToString("F4"), spikes, avgMem.ToString("F1"), avgCpu.ToString("F1"));

        string phaseCols = string.Join(",", Enum.GetValues<SimPhase>().Select(p =>
        {
            var s = phases?.Get(p) ?? default;
            return s.Frames > 0 ? s.AvgMs.ToString("F4") : string.Empty;
        }));

        return $"{configCols},{phaseCols},{frameFileRelative}";
    }

    private void RefreshCpuSample()
    {
        var now = DateTime.UtcNow;
        var wallSeconds = (now - _lastCpuSampleTime).TotalSeconds;
        if (wallSeconds > 0)
        {
            var cpuDelta = (_process.TotalProcessorTime - _lastCpuTime).TotalSeconds;
            _cpuPct = (float)(cpuDelta / wallSeconds / Environment.ProcessorCount * 100.0);
        }

        _lastCpuSampleTime = now;
        _lastCpuTime = _process.TotalProcessorTime;
    }

    private record RunData(int RunId, RunConfig Config, List<FrameRecord> Frames);
}
