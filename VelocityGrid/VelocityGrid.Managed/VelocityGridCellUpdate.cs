using System;

namespace VelocityGrid.Managed;

/// <summary>A caller-supplied value and complete visual state for one logical cell.</summary>
public readonly record struct VelocityGridCellUpdate
{
    /// <summary>Creates an update using zero-based logical row and source-column coordinates.</summary>
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

    /// <summary>Zero-based logical row index.</summary>
    public long RowIndex { get; }
    /// <summary>Zero-based source-column index (0–9).</summary>
    public int ColumnIndex { get; }
    /// <summary>Display-ready text copied into native cache storage.</summary>
    public string Value { get; }
    /// <summary>Complete formatting state that replaces the cached format.</summary>
    public VelocityGridCellFormat Format { get; }
}
