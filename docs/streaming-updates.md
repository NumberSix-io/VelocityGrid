# Streaming updates

`ApplyUpdates` snapshots a batch into parallel row, column, value, foreground, background, and icon arrays and crosses the ABI once.

```csharp
grid.ApplyUpdates(feedBatch.Select(change => new VelocityGridCellUpdate(
    change.RowIndex, change.ColumnIndex, change.DisplayValue, change.Format)));
```

## Semantics

- Coordinates are zero-based logical rows and configured source columns.
- Native processing prioritizes visible rows, then other cached rows.
- Uncached changes are ignored; the provider supplies authoritative future pages.
- Off-screen-only changes do not request presentation.
- Visible changes share a 16 ms one-shot coalescer.
- The grid applies supplied formatting exactly; it has no visual transition/timer policy.

Batch naturally, such as once per feed callback or dispatcher interval. Avoid one `ApplyUpdates` call per cell.

## Temporary formatting

For a 500 ms flash, send a coloured update, then a second update with the current value/icon and `Background.None`. If a newer value arrives first, replace the older scheduled clear so it cannot restore stale content. The sample demonstrates this replacement-safe pattern.

## Metrics

- `UpdateBatchCount`: native batches received.
- `UpdateCellCount`: changes that found cached cells.
- `UpdateRenderCount`: presentations containing visible changes.
- `LastUpdateLatencyMicroseconds`: oldest pending visible change to presentation.

The current sample sends a sparse irregular batch of 3–6 changes every 100 ms, weighted toward price cells. Historical high-rate Phase 7 results remain in [performance.md](performance.md); add a dedicated stress mode before comparing new throughput to that baseline.

This is viewport-level dirty invalidation. Pixel dirty rectangles remain deferred until profiling identifies full-viewport submission as a bottleneck.
