# Managed API and configuration reference

This is the supported C# surface. The WinRT API is an implementation boundary until 1.0.

## `VelocityGridControl`

### Data and layout

- `DataProvider`: assigning a provider cancels current requests, adopts `RowCount`, and requests the loaded viewport.
- `RowCount`: 64-bit logical size; normally provider-owned.
- `NotifyDataChanged(long, VelocityGridDataChangeKind)`: changes the extent with explicit append, end-trim, or full-reset cache semantics.
- `NotifyDataChanged(long, VelocityGridDataChangeKind, bool)`: additionally chooses whether the reset returns to row zero.
- `Refresh(bool resetScrollPosition = false)`: clears all cached pages and reloads the current provider snapshot.
- `InvalidateRows(long, long)`: evicts pages intersecting an in-range logical row span and reloads them when needed.
- `RowHeight`: fixed DIPs for all rows. Native validation accepts finite values of at least 8.
- `Columns`: immutable snapshot of configured visible columns.
- `SetColumns(...)`: one or more uniquely keyed columns. Replacing the snapshot cancels requests, clears cached pages, and recalculates horizontal layout.
- `FirstVisibleRow` / `LastVisibleRow`: inclusive logical viewport.

### Navigation and selection

- `ScrollToRow(long)`: random jump, clamped without traversing intermediate rows.
- `SelectedRow` / `SelectedColumn`: `-1` until selection.
- `SelectionChanged`: logical zero-based coordinates.
- Click selects. Arrows move one cell, Home/End cross columns, and Page Up/Page Down move approximately one viewport.

### Live updates

- `ApplyUpdates(...)`: snapshots and transfers one batch. Empty batches are ignored. Only resident cached cells change.
- The caller owns transient visuals and sends later updates to clear/change formatting.

### Changing datasets

- `Append`: requires a count greater than or equal to the current count. Full cached pages and selection are retained; a formerly partial final page is reloaded.
- `TrimEnd`: requires a count less than or equal to the current count. Pages crossing the new end are discarded, scrolling is clamped, and selection beyond the end moves to the new final row.
- `Reset`: accepts any non-negative count, cancels outstanding requests, clears every cached page and selection, and reloads the current clamped viewport. Use it for sorting, filtering, replacement, and insertions/deletions that shift row indices.

Assigning `RowCount` directly automatically uses `Append` or `TrimEnd`. Use `NotifyDataChanged(..., Reset)` when the count is unchanged but row content or ordering has changed.

### Diagnostics and errors

- `PerformanceMetrics`: frame, cache, request, and update snapshot.
- `ResetPerformanceMetrics()`: resets counters, not cache/data.
- `DataError`: provider exception plus requested range; also shown in diagnostics and announced accessibly.

## Provider/page contract

Providers must return the requested start, positive row count, exactly `RowCount * context.ColumnCount` values, and—if supplied—one format per value. They should observe cancellation, avoid blocking the UI thread before the first asynchronous yield, and return display-ready strings. Sorting/filtering/data access remain provider responsibilities.

`VelocityGridFetchContext` contains request ID and generation for correlation plus the active `ColumnCount` required in each returned row; the IDs are not persistent row identities.
Its `Columns` property is the exact immutable visible-column snapshot captured for the request. Use each column's `Key` to map application data; do not read a separate mutable column-chooser collection during asynchronous work.

## Columns

| Property | Default | Validation |
|---|---:|---|
| `Key` | header in compatibility constructor | non-empty and unique in the configured snapshot |
| `Header` | required | non-null |
| `Width` | 130 DIPs | finite, at least 32 |
| `Alignment` | `Left` | `Left`, `Center`, `Right` |

Resizing, reordering, sorting gestures, and frozen columns are not public options yet. The number of configured source columns is not capped; horizontal rendering is limited to columns intersecting the viewport.

## Formatting

- Foreground and background: the shared `VelocityGridColor` catalogue contains `None` plus 25 neutral named colours. `None` selects normal theme text for foreground and no fill for background.
- Icons: `VelocityGridIcon` contains `None` plus 28 caller-selected glyphs including arrows, triangles, status marks, shapes, media controls, and common interface symbols.

The grid assigns no semantic role to a colour or icon. Calling code owns their meaning and should not communicate important state by colour alone.

## Metrics

- `FrameCount`: presentations since reset.
- `CacheHits` / `CacheMisses`: render-time row-cache lookups; `CacheHitPercent` is derived.
- `RequestCount`: external page requests.
- `UpdateBatchCount`: native update batches.
- `UpdateCellCount`: updates that found a cached cell.
- `UpdateRenderCount`: presentations containing visible changes.
- `LastUpdateLatencyMicroseconds`: oldest pending visible update to presentation.

Metrics are diagnostics, not synchronization primitives, and should be sampled on the UI thread.
