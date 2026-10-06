namespace Zand.App.Metrics;

public record FrameRecord(int Frame, double FrameTimeMs, double SimStepMs, float MemoryMb, float CpuPct);
