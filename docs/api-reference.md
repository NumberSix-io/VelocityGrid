# Managed API and configuration reference

This is the supported C# surface. The WinRT API is an implementation boundary until 1.0.

## `VelocityGridControl`

### Data and layout

- `DataProvider`: assigning a provider cancels current requests, adopts `RowCount`, and requests the loaded viewport.
- `RowCount`: 64-bit logical size; normally provider-owned.
- `RowHeight`: fixed DIPs for all rows. Native validation accepts finite values of at least 8.
- `Columns`: immutable snapshot of configured visible columns.
- `SetColumns(...)`: one to ten columns mapped to the same zero-based source slots in each ten-cell row payload.
- `FirstVisibleRow` / `LastVisibleRow`: inclusive logical viewport.

### Navigation and selection

- `ScrollToRow(long)`: random jump, clamped without traversing intermediate rows.
- `SelectedRow` / `SelectedColumn`: `-1` until selection.
- `SelectionChanged`: logical zero-based coordinates.
- Click selects. Arrows move one cell, Home/End cross columns, and Page Up/Page Down move approximately one viewport.

### Live updates

- `ApplyUpdates(...)`: snapshots and transfers one batch. Empty batches are ignored. Only resident cached cells change.
- The caller owns transient visuals and sends later updates to clear/change formatting.

### Diagnostics and errors

- `PerformanceMetrics`: frame, cache, request, and update snapshot.
- `ResetPerformanceMetrics()`: resets counters, not cache/data.
- `DataError`: provider exception plus requested range; also shown in diagnostics and announced accessibly.

## Provider/page contract

Providers must return the requested start, positive row count, exactly `RowCount * 10` values, and—if supplied—one format per value. They should observe cancellation, avoid blocking the UI thread before the first asynchronous yield, and return display-ready strings. Sorting/filtering/data access remain provider responsibilities.

`VelocityGridFetchContext` contains request ID and generation for correlation; neither is a persistent row identity.

## Columns

| Property | Default | Validation |
|---|---:|---|
| `Header` | required | non-null |
| `Width` | 130 DIPs | finite, at least 32 |
| `Alignment` | `Left` | `Left`, `Center`, `Right` |

Resizing, reordering, sorting gestures, frozen columns, and more than ten source slots are not public options yet.

## Formatting

- Foregrounds: `Default`, `Positive`, `Negative`, `Warning`, `Accent`, `Muted`.
- Backgrounds: `None`, `Positive`, `Negative`, `Warning`, `Accent`.
- Icons: `None`, `Up`, `Down`, `Warning`, `Information`.

High contrast overrides provider palettes for legibility. Never communicate meaning by colour alone.

## Metrics

- `FrameCount`: presentations since reset.
- `CacheHits` / `CacheMisses`: render-time row-cache lookups; `CacheHitPercent` is derived.
- `RequestCount`: external page requests.
- `UpdateBatchCount`: native update batches.
- `UpdateCellCount`: updates that found a cached cell.
- `UpdateRenderCount`: presentations containing visible changes.
- `LastUpdateLatencyMicroseconds`: oldest pending visible update to presentation.

Metrics are diagnostics, not synchronization primitives, and should be sampled on the UI thread.
