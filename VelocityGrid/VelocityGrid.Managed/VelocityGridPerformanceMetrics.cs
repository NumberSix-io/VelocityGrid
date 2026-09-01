namespace VelocityGrid.Managed;

public readonly record struct VelocityGridPerformanceMetrics(
    ulong FrameCount,
    ulong CacheHits,
    ulong CacheMisses,
    ulong RequestCount,
    ulong UpdateBatchCount,
    ulong UpdateCellCount,
    ulong UpdateRenderCount,
    ulong LastUpdateLatencyMicroseconds)
{
    public double CacheHitPercent => CacheHits + CacheMisses == 0
        ? 0
        : CacheHits * 100.0 / (CacheHits + CacheMisses);
}
