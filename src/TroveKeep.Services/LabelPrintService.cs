using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using TroveKeep.Core.Interfaces.Services;
using TroveKeep.Core.Models;

namespace TroveKeep.Services;

/// <summary>
/// Builds the label JSON documents in the label-tool file format.
/// <para>
/// Two sinks are supported:
/// <list type="bullet">
///   <item><b>client</b> — no resolver: the label references its images by URL and the
///         API returns the text so the UI can download it into the watch folder;</item>
///   <item><b>server</b> — with an <see cref="ILabelImageResolver"/>: images are embedded
///         inline as base64 so the payload is self-sufficient when posted over HTTP.</item>
/// </list>
/// </para>
/// </summary>
public class LabelPrintService : ILabelPrintService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        // Labels contain large base64 image payloads: avoid escaping '+' as \u002B.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly LabelPrintSettings _settings;

    public LabelPrintService(LabelPrintSettings settings)
    {
        _settings = settings;
    }

    // ---- Bulk piece ----

    public async Task<string> BuildBulkPieceLabel(BulkPiece piece, ILabelImageResolver? images = null, int? copies = null, string? size = null)
    {
        var lines = new List<object>
        {
            Title(piece.LegoId),
        };

        if (!string.IsNullOrWhiteSpace(piece.Description))
            lines.Add(piece.Description);

        var image = piece.ImageCached && images is not null
            ? await images.ResolvePieceImageAsync(piece.Id, piece.LegoId, piece.LegoColorId)
            : null;
        AddQrLine(lines, LabelCodes.ForPiece(piece.LegoId, piece.LegoColorId), image);

        // Bulk-piece labels default to the small format (optional size query overrides).
        return Serialize(lines, copies, size ?? "small");
    }

    public string GetBulkPieceFileName(BulkPiece piece) =>
        $"piece-{Sanitize(piece.LegoId)}-{piece.LegoColorId}.json";

    /// <summary>
    /// Addresses a bulk-piece label to a specific storage location:
    /// 1) "{legoId} {colorName}", 2) the location line, 3) a row with QR + piece image.
    /// When <paramref name="qrValue"/> is provided (a neutral storage QR key) it is used as the
    /// QR payload instead of the per-piece code; otherwise falls back to the piece code.
    /// Always rendered as the small format.
    /// </summary>
    public async Task<string> BuildBulkPieceLocationLabel(BulkPiece piece, string? colorName, string? locationLine, ILabelImageResolver? images = null, int? copies = null, string? qrValue = null)
    {
        var displayColor = ShowColor(colorName) ? $" {colorName}" : "";
        var lines = new List<object>
        {
            $"{piece.LegoId}{displayColor}",
        };

        if (!string.IsNullOrWhiteSpace(locationLine))
            lines.Add(locationLine);

        var codeValue = qrValue ?? LabelCodes.ForPiece(piece.LegoId, piece.LegoColorId);
        var image = piece.ImageCached && images is not null
            ? await images.ResolvePieceImageAsync(piece.Id, piece.LegoId, piece.LegoColorId)
            : null;
        AddQrLine(lines, codeValue, image);

        return Serialize(lines, copies, "small");
    }

    public string GetBulkPieceLocationFileName(BulkPiece piece, int index) =>
        $"piece-{Sanitize(piece.LegoId)}-{piece.LegoColorId}-{index}.json";

    // ---- Set ----

    public async Task<string> BuildLegoSetLabel(LegoSet set, ILabelImageResolver? images = null, int? copies = null, string? size = null)
    {
        var lines = new List<object>
        {
            Title(set.SetNumber),
        };

        if (!string.IsNullOrWhiteSpace(set.Description))
            lines.Add(set.Description);

        var image = set.ImageCached && images is not null
            ? await images.ResolveSetImageAsync(set.Id, set.SetNumber)
            : null;
        AddQrLine(lines, LabelCodes.ForSet(set.SetNumber), image);

        return Serialize(lines, copies, size ?? _settings.DefaultSize);
    }

    public string GetLegoSetFileName(LegoSet set) => $"set-{Sanitize(set.SetNumber)}.json";

    // ---- Box (summary) ----

    public string BuildBoxSummaryLabel(Box box, int? copies = null)
    {
        var setCount = box.Sets.Count;
        var totalSetQty = box.Sets.Sum(s => s.StorageAllocations.Sum(a => a.Quantity));
        var pieceTypes = box.BulkPieces.Count;
        var totalPieces = box.BulkPieces.Sum(p => p.StorageAllocations.Sum(a => a.Quantity));

        var lines = new List<object>
        {
            box.Name,
            $"{setCount} {Pluralize(setCount, "set", "sets")} " +
                $"({totalSetQty} {Pluralize(totalSetQty, "pz", "pz")})",
            $"{pieceTypes} {Pluralize(pieceTypes, "tipo", "tipi")} — " +
                $"{totalPieces} {Pluralize(totalPieces, "pezzo", "pezzi")}",
        };

        return Serialize(lines, copies, _settings.DefaultSize);
    }

    public string GetBoxSummaryFileName(Box box) => $"box-{Sanitize(box.Name)}-summary.json";

    // ---- Box (qr) ----

    public string BuildBoxQrLabel(Box box, int? copies = null)
    {
        var lines = new List<object>
        {
            box.Name,
            Code("qr", LabelCodes.ForBox(box.Id)),
        };

        return Serialize(lines, copies, "small");
    }

    public string GetBoxQrFileName(Box box) => $"box-{Sanitize(box.Name)}-qr.json";

    // ---- Helpers ----

    private string Serialize(List<object> lines, int? copies, string size)
    {
        var effectiveCopies = copies is > 0 ? copies.Value : _settings.DefaultCopies;

        var labelFile = new LabelFile(lines, effectiveCopies, size);
        return JsonSerializer.Serialize(labelFile, JsonOptions);
    }

    private string Title(string id) =>
        string.IsNullOrWhiteSpace(_settings.Prefix)
            ? id
            : $"{_settings.Prefix} {id}";

    private static string Pluralize(int count, string singular, string plural) =>
        count == 1 ? singular : plural;

    private static LabelCodeLine Code(string code, string value) => new(code, value);

    /// <summary>
    /// Adds the QR code to the label, optionally alongside the piece/set image in a
    /// composite <c>row</c> when a resolved image is available.
    /// Without an image only the QR is emitted, as before.
    /// </summary>
    private static void AddQrLine(List<object> lines, string codeValue, LabelImage? image)
    {
        if (image is null)
        {
            lines.Add(Code("qr", codeValue));
            return;
        }

        lines.Add(new LabelRowLine(
        [
            new LabelCodeLine("qr", codeValue),
            ToImageLine(image),
        ]));
    }

    /// <summary>
    /// Picks the inline base64 form when present (server mode), otherwise the URL (client mode).
    /// </summary>
    private static LabelImageLine ToImageLine(LabelImage image) =>
        image.Base64 is not null
            ? new LabelImageLine(image.FileName, image.Base64, image.Mode)
            : new LabelImageLine(image.Url!, Mode: image.Mode);

    private static string Sanitize(string value) =>
        string.Concat(value.Where(char.IsLetterOrDigit)).ToLowerInvariant();

    /// <summary>
    /// True when the color name should be printed on the label. The Rebrickable import
    /// includes a default "Unknown" color; for those we print only the code, not the color.
    /// </summary>
    private static bool ShowColor(string? colorName) =>
        !string.IsNullOrWhiteSpace(colorName)
        && !IsUnknownColor(colorName);

    /// <summary>True when the color name denotes the undefined/default Rebrickable color.</summary>
    private static bool IsUnknownColor(string colorName)
    {
        var normalized = colorName.Trim().Trim('[', ']').Trim();
        return string.Equals(normalized, "unknown", StringComparison.OrdinalIgnoreCase);
    }

    private sealed record LabelCodeLine(
        [property: JsonPropertyName("code")] string Code,
        [property: JsonPropertyName("value")] string Value);

    /// <summary>Image element: <c>{"image": "URL|name", "data": "base64", "mode": "bw"}</c>.</summary>
    private sealed record LabelImageLine(
        [property: JsonPropertyName("image")] string Image,
        [property: JsonPropertyName("data")] string? Data = null,
        [property: JsonPropertyName("mode")] string Mode = "bw");

    /// <summary>Composite row: multiple graphic elements (<c>{"row": [...]}</c>).</summary>
    private sealed record LabelRowLine(
        [property: JsonPropertyName("row")] List<object> Row);

    private sealed record LabelFile(
        [property: JsonPropertyName("lines")] List<object> Lines,
        [property: JsonPropertyName("copies")] int Copies,
        [property: JsonPropertyName("size")] string Size);
}
