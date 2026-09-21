using TroveKeep.Core.Models;

namespace TroveKeep.Core.Interfaces.Services;

/// <summary>
/// The outcome of a print operation, as reported back to the UI. It normalizes the
/// two sinks (client download vs remote server queue) into one shape.
/// </summary>
public sealed record PrintOutcome(
    string Sink,
    int Sent,
    int Rejected,
    int Uncertain,
    IReadOnlyList<PrintItemResult> Items,
    string? ServerUrl,
    bool? PrinterAvailable,
    string? Message);

/// <summary>Per-label result (a batch prints one label per storage location).</summary>
public sealed record PrintItemResult(
    string FileName,
    string Status,
    string? JobId,
    string? Error);

/// <summary>
/// Builds labels and routes them to their sink: it returns the JSON for the client
/// to download, or posts it to the configured remote label-tool server.
/// </summary>
public interface ILabelDispatchService
{
    /// <summary>Reads the effective config (persisted value merged over appsettings defaults).</summary>
    Task<LabelPrintConfig> GetConfigAsync();

    /// <summary>Persists the config.</summary>
    Task<LabelPrintConfig> SaveConfigAsync(LabelPrintConfig config);

    /// <summary>Health of the configured remote server, or null when none is configured/reachable.</summary>
    Task<LabelServerHealth?> GetServerHealthAsync();

    Task<IReadOnlyList<LabelJob>?> GetJobsAsync(string? state = null, int limit = 100);

    Task<LabelJob?> RetryJobAsync(string jobId);

    Task<IReadOnlyList<LabelFormat>?> GetFormatsAsync();

    /// <summary>
    /// Builds the label(s) for a single set and either returns them (client mode) or
    /// posts them to the server (server mode).
    /// </summary>
    Task<PrintOutcome> PrintSetAsync(Guid setId, int? copies = null, string? size = null);

    /// <summary>
    /// Builds one label per storage location of a bulk piece and either returns them
    /// (client mode) or posts them (server mode).
    /// </summary>
    Task<PrintOutcome> PrintBulkPieceAsync(Guid pieceId, int? copies = null);

    /// <summary>Box summary label ("large" with the content overview).</summary>
    Task<PrintOutcome> PrintBoxSummaryAsync(Guid boxId, int? copies = null);

    /// <summary>Box QR label ("small" with the box code).</summary>
    Task<PrintOutcome> PrintBoxQrAsync(Guid boxId, int? copies = null);

    /// <summary>One label per bulk piece in the box.</summary>
    Task<PrintOutcome> PrintBoxPieceLabelsAsync(Guid boxId);

    /// <summary>One label per bulk piece in the drawer container (grouped by design and drawer).</summary>
    Task<PrintOutcome> PrintContainerPieceLabelsAsync(Guid containerId);
}
