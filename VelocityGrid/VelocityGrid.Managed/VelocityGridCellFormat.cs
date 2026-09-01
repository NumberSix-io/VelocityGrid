namespace VelocityGrid.Managed;

/// <summary>Semantic foreground colours resolved by the native palette.</summary>
public enum VelocityGridForeground : byte
{
    Default, Positive, Negative, Warning, Accent, Muted
}

/// <summary>Semantic background colours resolved by the native palette.</summary>
public enum VelocityGridBackground : byte
{
    None, Positive, Negative, Warning, Accent
}

/// <summary>Icons from the bounded native catalogue.</summary>
public enum VelocityGridIcon : byte
{
    None, Up, Down, Warning, Information
}

/// <summary>Compact immutable visual state stored beside a cached cell value.</summary>
public readonly record struct VelocityGridCellFormat(
    VelocityGridForeground Foreground = VelocityGridForeground.Default,
    VelocityGridBackground Background = VelocityGridBackground.None,
    VelocityGridIcon Icon = VelocityGridIcon.None);
