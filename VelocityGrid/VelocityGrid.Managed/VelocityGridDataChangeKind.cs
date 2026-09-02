namespace VelocityGrid.Managed;

/// <summary>Describes how a new logical row count relates to the existing dataset.</summary>
public enum VelocityGridDataChangeKind
{
    /// <summary>Rows were added only after the previous final row.</summary>
    Append = 0,

    /// <summary>Rows were removed only from the end of the dataset.</summary>
    TrimEnd = 1,

    /// <summary>Row identity or ordering may have changed; all cached data is invalid.</summary>
    Reset = 2
}
