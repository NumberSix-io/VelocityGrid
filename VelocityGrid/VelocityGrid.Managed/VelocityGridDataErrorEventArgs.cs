using System;

namespace VelocityGrid.Managed;

/// <summary>Reports a failed provider request without swallowing its original exception.</summary>
public sealed class VelocityGridDataErrorEventArgs(
    VelocityGridRange requestedRange, Exception exception) : EventArgs
{
    public VelocityGridRange RequestedRange { get; } = requestedRange;
    public Exception Exception { get; } = exception ?? throw new ArgumentNullException(nameof(exception));
}
