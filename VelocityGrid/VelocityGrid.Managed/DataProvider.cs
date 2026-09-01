using System;
using System.Threading;
using System.Threading.Tasks;

namespace VelocityGrid.Managed;

public readonly record struct VelocityGridRange(long StartRow, int RowCount);

public readonly record struct VelocityGridFetchContext(ulong RequestId, ulong Generation);

public sealed class VelocityGridPage
{
    public VelocityGridPage(long startRow, int rowCount, string[] values, VelocityGridCellFormat[]? formats = null)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (startRow < 0) throw new ArgumentOutOfRangeException(nameof(startRow));
        if (rowCount <= 0) throw new ArgumentOutOfRangeException(nameof(rowCount));
        if (values.Length != checked(rowCount * VelocityGridControl.ColumnCount))
            throw new ArgumentException("A page must contain exactly ten values per row.", nameof(values));
        if (formats is not null && formats.Length != values.Length)
            throw new ArgumentException("Cell formatting must contain one entry per value.", nameof(formats));
        StartRow = startRow;
        RowCount = rowCount;
        Values = values;
        Formats = formats ?? new VelocityGridCellFormat[values.Length];
    }

    public long StartRow { get; }
    public int RowCount { get; }
    public string[] Values { get; }
    public VelocityGridCellFormat[] Formats { get; }
}

public interface IVelocityGridDataProvider
{
    long RowCount { get; }

    ValueTask<VelocityGridPage> GetRowsAsync(
        VelocityGridRange range,
        VelocityGridFetchContext context,
        CancellationToken cancellationToken);
}

public sealed class SyntheticDataProvider(long rowCount = 10_000_000) : IVelocityGridDataProvider
{
    public long RowCount { get; } = rowCount >= 0 ? rowCount : throw new ArgumentOutOfRangeException(nameof(rowCount));

    public ValueTask<VelocityGridPage> GetRowsAsync(
        VelocityGridRange range,
        VelocityGridFetchContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var values = new string[checked(range.RowCount * VelocityGridControl.ColumnCount)];
        var formats = new VelocityGridCellFormat[values.Length];
        for (var rowOffset = 0; rowOffset < range.RowCount; rowOffset++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var row = range.StartRow + rowOffset;
            for (var column = 0; column < VelocityGridControl.ColumnCount; column++)
            {
                var index = rowOffset * VelocityGridControl.ColumnCount + column;
                values[index] = $"R{row}  C{column + 1}";
                if (column == 5)
                    formats[index] = (row % 3) switch
                    {
                        0 => new(VelocityGridForeground.Positive, VelocityGridBackground.Positive, VelocityGridIcon.Up),
                        1 => new(VelocityGridForeground.Negative, VelocityGridBackground.Negative, VelocityGridIcon.Down),
                        _ => new(VelocityGridForeground.Warning, VelocityGridBackground.Warning, VelocityGridIcon.Warning)
                    };
            }
        }
        return ValueTask.FromResult(new VelocityGridPage(range.StartRow, range.RowCount, values, formats));
    }
}

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
