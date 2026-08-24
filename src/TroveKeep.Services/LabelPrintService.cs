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

        lines.Add(Code("qr", LabelCodes.ForPiece(piece.LegoId, piece.LegoColorId)));

        return Serialize(lines, copies, size ?? _settings.DefaultSize);
    }

    public string GetBulkPieceFileName(BulkPiece piece) =>
        $"piece-{Sanitize(piece.LegoId)}-{piece.LegoColorId}.json";

    // ---- Set ----

    public string BuildLegoSetLabel(LegoSet set, int? copies = null, string? size = null)
    {
        var lines = new List<object>
        {
            Title(set.SetNumber),
        };

        if (!string.IsNullOrWhiteSpace(set.Description))
            lines.Add(set.Description);

        lines.Add(Code("qr", LabelCodes.ForSet(set.SetNumber)));

        return Serialize(lines, copies, size ?? _settings.DefaultSize);
    }

    public string GetLegoSetFileName(LegoSet set) => $"set-{Sanitize(set.SetNumber)}.json";

    // ---- Box (summary) ----

    public string BuildBoxSummaryLabel(Box box, int? copies = null)
    {
        var setCount = box.Sets.Count;
        var totalPieces = box.BulkPieces.Sum(p => p.StorageAllocations.Sum(a => a.Quantity));

        var lines = new List<object>
        {
            box.Name,
            $"{setCount} {Pluralize(setCount, "set", "sets")}",
            $"{totalPieces} {Pluralize(totalPieces, "piece", "pieces")}",
        };

        // Optionally note the contained sets when few fit in the remaining label space.
        if (setCount > 0 && setCount <= 2)
        {
            foreach (var s in box.Sets.Take(2))
            {
                var qty = s.StorageAllocations.Sum(a => a.Quantity);
                lines.Add($"{s.SetNumber}" + (qty > 1 ? $" ×{qty}" : ""));
            }
        }

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

        // Note: image support is intentionally omitted until label-tool supports an
        // "image" field (label-tool issue #13). Item images can be added here once available.
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

    private static string Sanitize(string value) =>
        string.Concat(value.Where(char.IsLetterOrDigit)).ToLowerInvariant();

    private sealed record LabelCodeLine(
        [property: JsonPropertyName("code")] string Code,
        [property: JsonPropertyName("value")] string Value);

    private sealed record LabelFile(
        [property: JsonPropertyName("lines")] List<object> Lines,
        [property: JsonPropertyName("copies")] int Copies,
        [property: JsonPropertyName("size")] string Size);
}
