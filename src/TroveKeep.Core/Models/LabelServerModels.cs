namespace TroveKeep.Core.Models;

/// <summary>Result of posting a label to the remote label-tool server.</summary>
public enum LabelDispatchOutcome
{
    /// <summary>The server accepted the job (HTTP 202).</summary>
    Accepted,

    /// <summary>The label or its image was rejected (HTTP 422). Retrying will not help.</summary>
    Invalid,

    /// <summary>The queue was not writable (HTTP 503) or the server failed (5xx). Safe to retry.</summary>
    Transient,

    /// <summary>
    /// Transport failure or timeout: it is unknown whether the server accepted the job.
    /// Must NOT be retried automatically (would risk a duplicate print).
    /// </summary>
    Uncertain,

    /// <summary>The server is not configured/reachable at all (no URL, connection refused).</summary>
    Unreachable,
}

/// <summary>Outcome of a single print request against the remote server.</summary>
public sealed record LabelDispatchResult(
    LabelDispatchOutcome Outcome,
    string? JobId,
    string? Error)
{
    public bool IsAccepted => Outcome == LabelDispatchOutcome.Accepted;
}

/// <summary>Health snapshot of the remote label-tool server.</summary>
public sealed record LabelServerHealth(
    bool Ok,
    string? Printer,
    int Pending,
    int Printing,
    int Failed);

/// <summary>State of a job in the remote server's queue.</summary>
public sealed record LabelJob(
    string JobId,
    string Status,
    int Attempts,
    string? Error,
    bool Uncertain);

/// <summary>A label format advertised by the remote server (for the size dropdown).</summary>
public sealed record LabelFormat(string Name, string Label);
