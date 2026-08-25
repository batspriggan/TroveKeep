namespace TroveKeep.Services;

/// <summary>
/// Configuration for the label-tool subprocess integration (appsettings.json section "LabelTool").
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
    /// absolute image URL embedded in a label. label-tool downloads the image from
    /// this URL before printing. Configured via environment (LabelTool__PublicBaseUrl).
    /// </summary>
    public string? PublicBaseUrl { get; set; }

    /// <summary>Timeout in seconds for the print subprocess.</summary>
    public int TimeoutSeconds { get; set; } = 60;
}
