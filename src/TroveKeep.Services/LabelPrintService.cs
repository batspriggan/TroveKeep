using System.Text.Json;
using System.Text.Json.Serialization;
using TroveKeep.Core.Interfaces.Services;
using TroveKeep.Core.Models;

namespace TroveKeep.Services;

/// <summary>
/// Builds the label JSON documents in the label-tool file format.
/// No I/O happens here: the API returns the text (and a suggested file name) and the
/// UI downloads it to the folder monitored by <c>label-tool watch</c>.
/// </summary>
public class LabelPrintService : ILabelPrintService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly LabelPrintSettings _settings;

    public LabelPrintService(LabelPrintSettings settings)
    {
        _settings = settings;
    }

    // ---- Bulk piece ----

    public string BuildBulkPieceLabel(BulkPiece piece, int? copies = null, string? size = null)
    {
        var lines = new List<object>
        {
            Title(piece.LegoId),
        };

        if (!string.IsNullOrWhiteSpace(piece.Description))
            lines.Add(piece.Description);

        var pieceImageUrl = PieceImageUrl(piece.Id);
        AddQrLine(lines, LabelCodes.ForPiece(piece.LegoId, piece.LegoColorId), piece.ImageCached ? pieceImageUrl : null);

        // Bulk-piece labels default to the small format (optional size query overrides).
        return Serialize(lines, copies, size ?? "small");
    }

    public string GetBulkPieceFileName(BulkPiece piece) =>
        $"piece-{Sanitize(piece.LegoId)}-{piece.LegoColorId}.json";

    /// <summary>
    /// Addresses a bulk-piece label to a specific storage location:
    /// 1) "{legoId} {colorName}", 2) the location line, 3) a row with QR + piece image.
    /// Always rendered as the small format.
    /// </summary>
    public string BuildBulkPieceLocationLabel(BulkPiece piece, string? colorName, string? locationLine, int? copies = null)
    {
        var displayColor = ShowColor(colorName) ? $" {colorName}" : "";
        var lines = new List<object>
        {
            $"{piece.LegoId}{displayColor}",
        };

        if (!string.IsNullOrWhiteSpace(locationLine))
            lines.Add(locationLine);

        var pieceImageUrl = PieceImageUrl(piece.Id);
        AddQrLine(lines, LabelCodes.ForPiece(piece.LegoId, piece.LegoColorId), piece.ImageCached ? pieceImageUrl : null);

        return Serialize(lines, copies, "small");
    }

    public string GetBulkPieceLocationFileName(BulkPiece piece, int index) =>
        $"piece-{Sanitize(piece.LegoId)}-{piece.LegoColorId}-{index}.json";

    // ---- Set ----

    public string BuildLegoSetLabel(LegoSet set, int? copies = null, string? size = null)
    {
        var lines = new List<object>
        {
            Title(set.SetNumber),
        };

        if (!string.IsNullOrWhiteSpace(set.Description))
            lines.Add(set.Description);

        var setImageUrl = SetImageUrl(set.Id);
        AddQrLine(lines, LabelCodes.ForSet(set.SetNumber), set.ImageCached ? setImageUrl : null);

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
    /// composite <c>row</c> when the image is cached and a public base URL is configured.
    /// Without an image (or base URL) only the QR is emitted, as before.
    /// </summary>
    private void AddQrLine(List<object> lines, string codeValue, string? imageUrl)
    {
        if (string.IsNullOrEmpty(imageUrl))
        {
            lines.Add(Code("qr", codeValue));
            return;
        }

        lines.Add(new LabelRowLine(
        [
            new LabelCodeLine("qr", codeValue),
            new LabelImageLine(imageUrl),
        ]));
    }

    private string? PieceImageUrl(Guid pieceId) =>
        ImageUrl($"/api/bulkpieces/{pieceId}/image");

    private string? SetImageUrl(Guid setId) =>
        ImageUrl($"/api/sets/{setId}/image");

    private string? ImageUrl(string relativePath)
    {
        var baseUrl = _settings.PublicBaseUrl;
        return string.IsNullOrWhiteSpace(baseUrl)
            ? null
            : $"{baseUrl.TrimEnd('/')}{relativePath}";
    }

    private static string Sanitize(string value) =>
        string.Concat(value.Where(char.IsLetterOrDigit)).ToLowerInvariant();

    /// <summary>
    /// True when the color name should be printed on the label. The Rebrickable import
    /// includes a default "Unknown" color; for those we print only the code, not the color.
    /// </summary>
    private static bool ShowColor(string? colorName) =>
        !string.IsNullOrWhiteSpace(colorName)
        && !string.Equals(colorName, "unknown", StringComparison.OrdinalIgnoreCase);

    private sealed record LabelCodeLine(
        [property: JsonPropertyName("code")] string Code,
        [property: JsonPropertyName("value")] string Value);

    /// <summary>Image element (<c>{"image": "URL|path", "mode": "bw"}</c>).</summary>
    private sealed record LabelImageLine(
        [property: JsonPropertyName("image")] string Image,
        [property: JsonPropertyName("mode")] string Mode = "bw");

    /// <summary>Composite row: multiple graphic elements (<c>{"row": [...]}</c>).</summary>
    private sealed record LabelRowLine(
        [property: JsonPropertyName("row")] List<object> Row);

    private sealed record LabelFile(
        [property: JsonPropertyName("lines")] List<object> Lines,
        [property: JsonPropertyName("copies")] int Copies,
        [property: JsonPropertyName("size")] string Size);
}
