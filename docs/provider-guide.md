# Managed data providers

Implement `IVelocityGridDataProvider`; do not expose a giant collection. The grid requests a contiguous logical range and expects one flat row-major page using the column count supplied in the fetch context.

```csharp
public sealed class DatabaseProvider : IVelocityGridDataProvider
{
    public long RowCount { get; private set; }

    public async ValueTask<VelocityGridPage> GetRowsAsync(
        VelocityGridRange range,
        VelocityGridFetchContext context,
        CancellationToken cancellationToken)
    {
        var records = await QueryAsync(range.StartRow, range.RowCount, cancellationToken);
        var values = new string[range.RowCount * context.ColumnCount];
        var formats = new VelocityGridCellFormat[values.Length];
        FlattenDisplayData(records, context.Columns, values, formats);
        return new VelocityGridPage(range.StartRow, range.RowCount, values, formats);
    }
}
```

Assign it to `DataProvider`. The grid adopts its `RowCount`; assignment before load is supported because activation waits until request handlers are attached.

## Page contract

- `StartRow` is non-negative; `RowCount` is positive.
- `Values.Length == RowCount * context.ColumnCount`.
- Optional formats contain exactly one entry per value.
- Index with `rowOffset * context.ColumnCount + columnIndex`.
- `context.Columns` is the exact immutable snapshot for that request. Map fields using `VelocityGridColumn.Key` so column chooser changes are race-free.
- Changing the configured columns cancels outstanding requests and invalidates cached pages. New requests contain the new `context.ColumnCount`.

The provider remains authoritative. Uncached streaming changes are ignored, so later fetches must contain current state.

## Changing the row count

Update the provider's underlying snapshot/count first, then notify the grid on its UI thread:

```csharp
provider.ApplyNewSnapshot(records);
grid.NotifyDataChanged(provider.RowCount, VelocityGridDataChangeKind.Reset);
```

Use `Append` when new records are added strictly after every existing row, and `TrimEnd` when records are removed strictly from the end. These modes preserve unaffected cache entries. Use `Reset` whenever sorting, filtering, insertion, deletion, or replacement can change the meaning of an existing row index—even when `RowCount` stays the same.

For a same-count query or projection refresh, use `grid.Refresh()`. Pass `resetScrollPosition: true` after a sort or filter when the expected experience is to return to the first result. The default preserves and clamps the existing viewport.

When a contiguous set of logical rows changed without changing ordering or count, call `grid.InvalidateRows(startRow, rowCount)`. Cached pages intersecting the range are discarded; unrelated pages remain hot. Use `ApplyUpdates` instead when the caller already has display-ready values and formats for specific resident cells.

`IVelocityGridDataProvider.RowCount` is a getter and has no notification event. Changing the provider alone does not update the grid. This deliberate explicit notification keeps threading, batching, and invalidation under the host application's control.

## Cancellation and stale work

The adapter creates a token per page request. Generation changes and pages leaving the predictive window cancel it. Pass it to database/HTTP/delay operations and do not swallow `OperationCanceledException`.

Completions return request ID and generation. Native code rejects unknown, canceled, old-generation, wrong-range, and no-longer-wanted results. Context IDs are useful for logging, not durable identities.

Avoid synchronous I/O or expensive transformation before the first `await`; callbacks originate on the UI thread.

## Errors

Non-cancellation exceptions are caught. The control calls native `FailPage`, raises `DataError`, and announces the error through its live region. The host owns retry or replacement UI. Throw a descriptive exception instead of returning a partial page.

## Sorting and filtering

Perform them at the source for large/remote datasets. Change provider query state and logical ordering rather than materializing all rows locally, then notify the grid with `Reset`.

For a column chooser, build one new immutable column array and pass it to `SetColumns`. Each column should have a stable key independent of its display header:

```csharp
grid.SetColumns(new[]
{
    new VelocityGridColumn("trade.symbol", "Symbol", 140),
    new VelocityGridColumn("trade.price", "Price", 110, VelocityGridTextAlignment.Right)
});
```

`SetColumns` invalidates the previous row shape automatically. Subsequent provider requests contain those two descriptors in that order.

`SyntheticDataProvider` supplies immediate deterministic data. `SimulatedRemoteDataProvider` adds cancellable latency. The basic sample demonstrates the complete integration.

One request and completion occur per page, never per cell. Cached rendering requires no managed callback.
