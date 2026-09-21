namespace TroveKeep.Services;

/// <summary>
/// Configuration for the label-tool integration (appsettings.json section "LabelTool").
/// These are the *defaults*; the effective values (mode, server URL) are persisted at
/// runtime in the <c>settings</c> collection and can be changed from the UI.
/// </summary>
public class LabelPrintSettings
{
    /// <summary>label-tool executable: a name on PATH or an absolute path.</summary>
    public string Binary { get; set; } = "label-tool";

    /// <summary>Default label size: "small" (40x30mm) or "large" (48x80mm roll).</summary>
    public string DefaultSize { get; set; } = "large";

    /// <summary>Default number of copies.</summary>
    public int DefaultCopies { get; set; } = 1;

    /// <summary>Prefix used for the label title (e.g. "LEGO" -> "LEGO 40469").</summary>
    public string Prefix { get; set; } = "LEGO";

    /// <summary>
    /// Public base URL of the TroveKeep API (no trailing slash), used to build the
    /// absolute image URL embedded in a label. Configured via environment (LabelTool__PublicBaseUrl).
    /// </summary>
    public string? PublicBaseUrl { get; set; }

    /// <summary>Timeout in seconds for the print subprocess.</summary>
    public int TimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// Initial sink mode: <c>client</c> (download the JSON files) or <c>server</c>
    /// (send the label to a remote label-tool server over HTTP). Used only as the
    /// default when no runtime setting has been persisted yet.
    /// </summary>
    public LabelSinkMode Mode { get; set; } = LabelSinkMode.Client;

    /// <summary>Base URL of the remote label-tool server, e.g. <c>http://192.168.10.14:9898</c>.</summary>
    public string? ServerUrl { get; set; }

    /// <summary>
    /// Optional bearer token for the remote server. The current label-tool API has no
    /// authentication; the field is reserved for a future reverse-proxy setup.
    /// </summary>
    public string? ServerToken { get; set; }

    /// <summary>Timeout in seconds for a single request to the remote server.</summary>
    public int ServerTimeoutSeconds { get; set; } = 15;
}

/// <summary>How a built label reaches the printer.</summary>
public enum LabelSinkMode
{
    /// <summary>Client mode: the API returns the label JSON, the user downloads and manages it.</summary>
    Client,

    /// <summary>Server mode: the API posts the label to a remote label-tool server, which prints it.</summary>
    Server,
}
