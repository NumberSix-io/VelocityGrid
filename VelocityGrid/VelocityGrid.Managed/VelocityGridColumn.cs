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
    /// <summary>Creates a column whose application key defaults to its header.</summary>
    public VelocityGridColumn(string header, double width = 130,
        VelocityGridTextAlignment alignment = VelocityGridTextAlignment.Left)
        : this(header, header, width, alignment)
    {
    }

    /// <summary>Creates a column with a stable application-defined data key.</summary>
    public VelocityGridColumn(string key, string header, double width = 130,
        VelocityGridTextAlignment alignment = VelocityGridTextAlignment.Left)
    {
        if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("A column key is required.", nameof(key));
        Key = key;
        Header = header ?? throw new ArgumentNullException(nameof(header));
        if (!double.IsFinite(width) || width < 32) throw new ArgumentOutOfRangeException(nameof(width));
        Width = width;
        Alignment = alignment;
    }

    /// <summary>Stable application-defined key used to map provider fields to this visible column.</summary>
    public string Key { get; }
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
