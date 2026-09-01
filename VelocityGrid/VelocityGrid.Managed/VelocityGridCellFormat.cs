namespace VelocityGrid.Managed;

public enum VelocityGridForeground : byte
{
    Default, Positive, Negative, Warning, Accent, Muted
}

public enum VelocityGridBackground : byte
{
    None, Positive, Negative, Warning, Accent
}

public enum VelocityGridIcon : byte
{
    None, Up, Down, Warning, Information
}

public readonly record struct VelocityGridCellFormat(
    VelocityGridForeground Foreground = VelocityGridForeground.Default,
    VelocityGridBackground Background = VelocityGridBackground.None,
    VelocityGridIcon Icon = VelocityGridIcon.None);
