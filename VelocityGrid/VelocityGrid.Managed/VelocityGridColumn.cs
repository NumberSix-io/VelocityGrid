using System;

namespace VelocityGrid.Managed;

/// <summary>Horizontal alignment used by the native DirectWrite cell format.</summary>
public enum VelocityGridTextAlignment
{
    Left,
    Center,
    Right
}

/// <summary>Immutable header and layout metadata for one visible source column.</summary>
public sealed class VelocityGridColumn
{
    public VelocityGridColumn(string header, double width = 130,
        VelocityGridTextAlignment alignment = VelocityGridTextAlignment.Left)
    {
        Header = header ?? throw new ArgumentNullException(nameof(header));
        if (!double.IsFinite(width) || width < 32) throw new ArgumentOutOfRangeException(nameof(width));
        Width = width;
        Alignment = alignment;
    }

    /// <summary>Text displayed in the fixed header row.</summary>
    public string Header { get; }
    /// <summary>Column width in device-independent pixels.</summary>
    public double Width { get; }
    /// <summary>Header and cell text alignment.</summary>
    public VelocityGridTextAlignment Alignment { get; }
}

/// <summary>Describes the newly selected logical cell using zero-based coordinates.</summary>
public sealed class VelocityGridSelectionChangedEventArgs(long rowIndex, int columnIndex) : EventArgs
{
    public long RowIndex { get; } = rowIndex;
    public int ColumnIndex { get; } = columnIndex;
}
    /// <summary>Creates a column. Width is measured in device-independent pixels.</summary>
