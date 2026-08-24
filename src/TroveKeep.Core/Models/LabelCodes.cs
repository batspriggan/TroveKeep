namespace TroveKeep.Core.Models;

/// <summary>
/// Payload format for the QR code printed on bulk-piece labels.
/// Format: <c>TK:BP:&lt;LegoId&gt;:&lt;LegoColorId&gt;</c>.
/// </summary>
public static class LabelCodes
{
    public const string Prefix = "TK:BP:";

    public static string ForPiece(string legoId, int legoColorId) => $"{Prefix}{legoId}:{legoColorId}";

    /// <summary>
    /// Parses a scanned code back into the piece business key.
    /// The color id is always the last ':'-separated token, so a LegoId containing ':' still parses.
    /// </summary>
    public static bool TryParsePieceCode(string? code, out string legoId, out int legoColorId)
    {
        legoId = string.Empty;
        legoColorId = 0;

        if (string.IsNullOrWhiteSpace(code) || !code.StartsWith(Prefix, StringComparison.Ordinal))
            return false;

        var payload = code[Prefix.Length..];
        var sep = payload.LastIndexOf(':');
        if (sep <= 0 || sep == payload.Length - 1)
            return false;

        legoId = payload[..sep];
        return int.TryParse(payload[(sep + 1)..], out legoColorId);
    }
}
