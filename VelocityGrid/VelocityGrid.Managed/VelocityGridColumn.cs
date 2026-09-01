using System;

namespace VelocityGrid.Managed;

public enum VelocityGridTextAlignment
{
    Left,
    Center,
    Right
}

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

    public string Header { get; }
    public double Width { get; }
    public VelocityGridTextAlignment Alignment { get; }
}

public sealed class VelocityGridSelectionChangedEventArgs(long rowIndex, int columnIndex) : EventArgs
{
    public long RowIndex { get; } = rowIndex;
    public int ColumnIndex { get; } = columnIndex;
}
