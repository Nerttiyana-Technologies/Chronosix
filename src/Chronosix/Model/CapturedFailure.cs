namespace Chronosix.Model;

/// <summary>
/// A snapshot of an unhandled exception thrown inside a background worker loop or a
/// scheduled job. The failure tracer keeps a bounded, in-memory queue of these so a
/// developer can inspect what went wrong without scrolling through console logs.
/// </summary>
public sealed class CapturedFailure
{
    /// <summary>Unique id for this failure record.</summary>
    public required Guid Id { get; init; }

    /// <summary>Id of the job that failed.</summary>
    public required string JobId { get; init; }

    /// <summary>Name of the job that failed.</summary>
    public required string JobName { get; init; }

    /// <summary>How the failing job is wired into the host.</summary>
    public required JobKind Kind { get; init; }

    /// <summary>The execution that produced this failure, when known.</summary>
    public Guid? ExecutionId { get; init; }

    /// <summary>When the failure occurred (UTC).</summary>
    public required DateTimeOffset OccurredAtUtc { get; init; }

    /// <summary>CLR type name of the exception.</summary>
    public required string ExceptionType { get; init; }

    /// <summary>Exception message.</summary>
    public required string Message { get; init; }

    /// <summary>Full exception detail, including stack trace and inner exceptions.</summary>
    public string? Detail { get; init; }

    /// <summary>Why the failing execution had started.</summary>
    public TriggerReason Trigger { get; init; }

    /// <summary>Whether a developer has marked this failure as reviewed.</summary>
    public bool Acknowledged { get; set; }
}
