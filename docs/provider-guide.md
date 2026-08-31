# Managed data providers

Applications provide data by implementing `IVelocityGridDataProvider`. The grid requests a contiguous range and expects one flat page containing ten values per row in row-major order.

```csharp
public sealed class MyProvider : IVelocityGridDataProvider
{
    public long RowCount => 1_000_000;

    public async ValueTask<VelocityGridPage> GetRowsAsync(
        VelocityGridRange range,
        VelocityGridFetchContext context,
        CancellationToken cancellationToken)
    {
        var rows = await LoadRowsAsync(range.StartRow, range.RowCount, cancellationToken);
        return new VelocityGridPage(range.StartRow, range.RowCount, Flatten(rows));
    }
}
```

Assign the provider to `VelocityGridControl.DataProvider`. Its `RowCount` becomes the grid's logical row count.

## Cancellation and stale work

The adapter creates one cancellation token per native page request. A viewport generation change or a request moving outside the prefetch window cancels that token. Providers should pass it through every asynchronous operation and should not catch `OperationCanceledException` unless they rethrow it.

Every completion returns its request ID and generation to native code. Native validation rejects unknown, canceled, old-generation, or no-longer-wanted pages before cache insertion. Provider exceptions are reported to the native diagnostics overlay without blocking the UI thread.

## ABI and rendering

One request callback and one completion call occur per page, never per cell. Page strings are copied into native cache storage on completion. Rendering, scrolling over cached rows, formatting lookup, and hit testing therefore require no managed callback.

`SyntheticDataProvider` is an immediate in-memory example. `SimulatedRemoteDataProvider` wraps any provider with cancellable latency for testing loading, rapid scrolling, and stale-request behavior.
