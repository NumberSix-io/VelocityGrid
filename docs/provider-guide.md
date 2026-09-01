# Managed data providers

Implement `IVelocityGridDataProvider`; do not expose a giant collection. The grid requests a contiguous logical range and expects one flat row-major page with ten source values per row.

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
        var values = new string[range.RowCount * VelocityGridControl.ColumnCount];
        var formats = new VelocityGridCellFormat[values.Length];
        FlattenDisplayData(records, values, formats);
        return new VelocityGridPage(range.StartRow, range.RowCount, values, formats);
    }
}
```

Assign it to `DataProvider`. The grid adopts its `RowCount`; assignment before load is supported because activation waits until request handlers are attached.

## Page contract

- `StartRow` is non-negative; `RowCount` is positive.
- `Values.Length == RowCount * 10`.
- Optional formats contain exactly one entry per value.
- Index with `rowOffset * 10 + columnIndex`.
- Configuring fewer visible columns changes layout, not the ten-slot ABI.

The provider remains authoritative. Uncached streaming changes are ignored, so later fetches must contain current state.

## Cancellation and stale work

The adapter creates a token per page request. Generation changes and pages leaving the predictive window cancel it. Pass it to database/HTTP/delay operations and do not swallow `OperationCanceledException`.

Completions return request ID and generation. Native code rejects unknown, canceled, old-generation, wrong-range, and no-longer-wanted results. Context IDs are useful for logging, not durable identities.

Avoid synchronous I/O or expensive transformation before the first `await`; callbacks originate on the UI thread.

## Errors

Non-cancellation exceptions are caught. The control calls native `FailPage`, raises `DataError`, and announces the error through its live region. The host owns retry or replacement UI. Throw a descriptive exception instead of returning a partial page.

## Sorting and filtering

Perform them at the source for large/remote datasets. Change provider query state and logical ordering rather than materializing all rows locally. A public refresh/sort descriptor contract is a post-1.0 candidate.

`SyntheticDataProvider` supplies immediate deterministic data. `SimulatedRemoteDataProvider` adds cancellable latency. The basic sample demonstrates the complete integration.

One request and completion occur per page, never per cell. Cached rendering requires no managed callback.
