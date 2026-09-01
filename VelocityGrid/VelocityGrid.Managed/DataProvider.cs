using System;
using System.Threading;
using System.Threading.Tasks;

namespace VelocityGrid.Managed;

/// <summary>A contiguous logical row range.</summary>
public readonly record struct VelocityGridRange(long StartRow, int RowCount);

/// <summary>Correlation metadata and the active row shape for one viewport request.</summary>
public readonly record struct VelocityGridFetchContext(
    ulong RequestId,
    ulong Generation,
    int ColumnCount = VelocityGridControl.DefaultColumnCount);

/// <summary>A display-ready row-major page and optional compact cell formatting.</summary>
public sealed class VelocityGridPage
{
    /// <summary>Creates a validated page containing a consistent positive number of values per row.</summary>
    public VelocityGridPage(long startRow, int rowCount, string[] values, VelocityGridCellFormat[]? formats = null)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (startRow < 0) throw new ArgumentOutOfRangeException(nameof(startRow));
        if (rowCount <= 0) throw new ArgumentOutOfRangeException(nameof(rowCount));
        if (values.Length == 0 || values.Length % rowCount != 0)
            throw new ArgumentException("A page must contain the same positive number of values for every row.", nameof(values));
        if (formats is not null && formats.Length != values.Length)
            throw new ArgumentException("Cell formatting must contain one entry per value.", nameof(formats));
        StartRow = startRow;
        RowCount = rowCount;
        ColumnCount = values.Length / rowCount;
        Values = values;
        Formats = formats ?? new VelocityGridCellFormat[values.Length];
    }

    public long StartRow { get; }
    public int RowCount { get; }
    public int ColumnCount { get; }
    public string[] Values { get; }
    public VelocityGridCellFormat[] Formats { get; }
}

/// <summary>Supplies cancellable pages for the grid's current viewport and prefetch window.</summary>
public interface IVelocityGridDataProvider
{
    long RowCount { get; }

    ValueTask<VelocityGridPage> GetRowsAsync(
        VelocityGridRange range,
        VelocityGridFetchContext context,
        CancellationToken cancellationToken);
}

/// <summary>Deterministic in-memory provider used by samples, tests, and benchmarks.</summary>
public sealed class SyntheticDataProvider(long rowCount = 10_000_000) : IVelocityGridDataProvider
{
    public long RowCount { get; } = rowCount >= 0 ? rowCount : throw new ArgumentOutOfRangeException(nameof(rowCount));

    public ValueTask<VelocityGridPage> GetRowsAsync(
        VelocityGridRange range,
        VelocityGridFetchContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (context.ColumnCount <= 0) throw new ArgumentOutOfRangeException(nameof(context));
        var values = new string[checked(range.RowCount * context.ColumnCount)];
        var formats = new VelocityGridCellFormat[values.Length];
        for (var rowOffset = 0; rowOffset < range.RowCount; rowOffset++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var row = range.StartRow + rowOffset;
            for (var column = 0; column < context.ColumnCount; column++)
            {
                var index = rowOffset * context.ColumnCount + column;
                values[index] = $"R{row}  C{column + 1}";
                if (column == 5)
                    formats[index] = (row % 3) switch
                    {
                        0 => new(VelocityGridColor.Green, VelocityGridColor.LightGreen, VelocityGridIcon.UpArrow),
                        1 => new(VelocityGridColor.Red, VelocityGridColor.LightRed, VelocityGridIcon.DownArrow),
                        _ => new(VelocityGridColor.Amber, VelocityGridColor.Yellow, VelocityGridIcon.Warning)
                    };
            }
        }
        return ValueTask.FromResult(new VelocityGridPage(range.StartRow, range.RowCount, values, formats));
    }
}

/// <summary>Adds cancellable latency to another provider for loading/stale-request testing.</summary>
public sealed class SimulatedRemoteDataProvider(IVelocityGridDataProvider inner, TimeSpan latency)
    : IVelocityGridDataProvider
{
    private readonly IVelocityGridDataProvider _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    private readonly TimeSpan _latency = latency >= TimeSpan.Zero ? latency : throw new ArgumentOutOfRangeException(nameof(latency));

    public long RowCount => _inner.RowCount;

    public async ValueTask<VelocityGridPage> GetRowsAsync(
        VelocityGridRange range,
        VelocityGridFetchContext context,
        CancellationToken cancellationToken)
    {
        await Task.Delay(_latency, cancellationToken);
        return await _inner.GetRowsAsync(range, context, cancellationToken);
    }
}
