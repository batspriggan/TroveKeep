namespace TroveKeep.Api.DTOs.Responses;

/// <summary>Effective label-printing configuration exposed to the UI.</summary>
public sealed record LabelConfigResponse(string Mode, string? ServerUrl, bool HasServerToken);

/// <summary>Result of a print operation, normalized across the two sinks.</summary>
public sealed record PrintOutcomeResponse(
    string Sink,
    int Sent,
    int Rejected,
    int Uncertain,
    IReadOnlyList<PrintItemResponse> Items,
    string? ServerUrl,
    bool? PrinterAvailable,
    string? Message);

public sealed record PrintItemResponse(string FileName, string Status, string? JobId, string? Error);

public sealed record ServerHealthResponse(bool Ok, string? Printer, int Pending, int Printing, int Failed);

public sealed record ServerJobResponse(string JobId, string Status, int Attempts, string? Error, bool Uncertain);

public sealed record ServerFormatResponse(string Name, string Label);
