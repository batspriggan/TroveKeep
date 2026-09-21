namespace TroveKeep.Core.Models;

/// <summary>
/// Runtime label-printing settings, persisted in the <c>settings</c> collection
/// (key <c>label_print</c>). These override the appsettings defaults so the mode
/// and server URL can be changed from the UI without a redeploy.
/// </summary>
public class LabelPrintConfig
{
    /// <summary>How labels reach the printer: "client" (download) or "server" (HTTP).</summary>
    public string Mode { get; set; } = "client";

    /// <summary>Base URL of the remote label-tool server (no trailing slash).</summary>
    public string? ServerUrl { get; set; }

    /// <summary>Optional bearer token reserved for an authenticated reverse proxy.</summary>
    public string? ServerToken { get; set; }

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
