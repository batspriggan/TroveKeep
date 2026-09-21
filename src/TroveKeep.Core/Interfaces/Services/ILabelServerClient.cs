namespace TroveKeep.Core.Interfaces.Services;

/// <summary>
/// A fully-built label payload, ready to be serialized and either downloaded
/// (client mode) or posted to the remote label-tool server (server mode).
/// </summary>
public sealed record BuiltLabel(string Json, string FileName, string Size);

/// <summary>An image ready to be embedded in a label line.</summary>
/// <param name="Mode">
/// Binarization mode for the thermal printer. Defaults to <c>dither</c>
/// (Floyd-Steinberg): the catalog images are colour POV-Ray renders with
/// gradients, and a fixed 50% threshold (<c>bw</c>) flattens them into a solid
/// silhouette (a dark-coloured piece becomes an all-black blob).
/// </param>
public sealed record LabelImage(string? Url, string? Base64, string FileName, string Mode = "dither");

/// <summary>
/// Resolves a cached image into the two forms a label can embed:
/// an absolute URL (client mode — label-tool downloads it) or inline base64
/// (server mode — the payload is self-sufficient, no inbound connectivity needed).
/// </summary>
public interface ILabelImageResolver
{
    /// <summary>Returns the image element for a piece image, or null when no image is cached.</summary>
    Task<LabelImage?> ResolvePieceImageAsync(Guid pieceId, string legoId, int legoColorId);

    /// <summary>Returns the image element for a set image, or null when no image is cached.</summary>
    Task<LabelImage?> ResolveSetImageAsync(Guid setId, string setNumber);
}

/// <summary>
/// Talks to a remote label-tool server over HTTP (see the server's
/// <c>docs/server-api.md</c>). Implementations must never retry a POST
/// automatically: a transport failure is reported as
/// <see cref="Models.LabelDispatchOutcome.Uncertain"/>.
/// </summary>
public interface ILabelServerClient
{
    Task<Models.LabelDispatchResult> PrintAsync(string baseUrl, string? token, string labelJson, CancellationToken ct = default);

    Task<Models.LabelServerHealth?> GetHealthAsync(string baseUrl, string? token, CancellationToken ct = default);

    Task<IReadOnlyList<Models.LabelJob>?> GetJobsAsync(string baseUrl, string? token, string? state = null, int limit = 100, CancellationToken ct = default);

    Task<Models.LabelJob?> RetryJobAsync(string baseUrl, string? token, string jobId, CancellationToken ct = default);

    Task<IReadOnlyList<Models.LabelFormat>?> GetFormatsAsync(string baseUrl, string? token, CancellationToken ct = default);
}
