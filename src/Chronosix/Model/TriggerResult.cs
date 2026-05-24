namespace Chronosix.Model;

/// <summary>The outcome of a "Trigger Now" request.</summary>
/// <param name="Success">True when the job was queued for an immediate run.</param>
/// <param name="Message">A short, human-readable explanation suitable for display.</param>
public sealed record TriggerResult(bool Success, string Message);
