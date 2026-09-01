using System;

namespace VelocityGrid.Managed;

public readonly record struct VelocityGridCellUpdate
{
    public VelocityGridCellUpdate(long rowIndex, int columnIndex, string value, VelocityGridCellFormat format = default)
    {
        if (rowIndex < 0) throw new ArgumentOutOfRangeException(nameof(rowIndex));
        if (columnIndex is < 0 or >= VelocityGridControl.ColumnCount)
            throw new ArgumentOutOfRangeException(nameof(columnIndex));
        RowIndex = rowIndex;
        ColumnIndex = columnIndex;
        Value = value ?? throw new ArgumentNullException(nameof(value));
        Format = format;
    }

    public long RowIndex { get; }
    public int ColumnIndex { get; }
    public string Value { get; }
    public VelocityGridCellFormat Format { get; }
}
