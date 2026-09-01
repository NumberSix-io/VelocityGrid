namespace VelocityGrid.Managed;

/// <summary>Snapshot of native cache, request, presentation, and update counters.</summary>
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
    /// <summary>Percentage of render-time row lookups that found a cached page.</summary>
    public double CacheHitPercent => CacheHits + CacheMisses == 0
        ? 0
        : CacheHits * 100.0 / (CacheHits + CacheMisses);
}
