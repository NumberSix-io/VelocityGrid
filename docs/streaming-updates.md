# Streaming updates

`VelocityGridControl.ApplyUpdates` accepts a batch of `VelocityGridCellUpdate` values. The managed projection flattens the batch into three arrays and calls native code once, so a batch of hundreds of changes does not create one ABI call per cell.

Native processing applies changes to rows in the visible viewport first, then mutates any other cached rows. Updates for rows that are not currently cached are ignored because the data provider remains authoritative when those pages are fetched later. Off-screen-only cache mutations do not invalidate the viewport.

Visible changes use the same 16 ms one-shot render coalescer as page completions. Multiple update batches arriving before the timer fires produce one presentation. This is viewport-level dirty invalidation: a render is requested only when at least one cached visible cell changed. Pixel-level dirty rectangles are intentionally deferred until profiling demonstrates that full-viewport Direct2D submission is a bottleneck.

The performance snapshot exposes:

- `UpdateBatchCount`: native batch calls received;
- `UpdateCellCount`: cached cells mutated;
- `UpdateRenderCount`: presentations containing one or more streaming changes;
- `LastUpdateLatencyMicroseconds`: time from the oldest pending visible mutation to presentation completion.

The sample's **Start market stream** mode submits 250 price, status, and timestamp changes every 16 ms—approximately 15,000 requested cell changes per second. Its live status reports batching, cached mutations, renders, updates per render, and visible update latency. Scroll while the stream runs to exercise visible-row prioritization and page churn, then select **Stop** to retain the final measurement.

Updates must use logical row indexes and source column indexes from 0 through 9. Applications should batch changes naturally (for example, per feed callback or dispatcher interval) rather than calling `ApplyUpdates` for individual cells.

## Release baseline — 2026-09-01

The market stream was run for approximately ten seconds while scrolling the grid:

| Metric | Result |
|---|---:|
| Batches received | 568 |
| Cached cells mutated | 100,000 |
| Update-bearing renders | 399 |
| Cached updates per render | 250.6 |
| Last visible update latency | 30.77 ms |

The sample requested 142,000 changes (568 batches × 250); 100,000 targeted cells that were cached when processed. Page churn while scrolling accounts for the remaining requests. Only 399 presentations were needed, so rendering occurred once per approximately 1.42 batches and once per 250.6 cached cell changes—not once per changed cell. The 30.77 ms visible latency is about 1.85 frames on the 60 Hz test display and includes the coalescing interval plus synchronized swap-chain presentation.

Phase 7 exit criteria are satisfied. A future latency optimization may align update presentation directly with the composition frame callback, but the current result already provides bounded, measured latency and eliminates per-cell ABI and rendering work.
