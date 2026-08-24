using System.Text.Json;
using System.Text.Json.Serialization;
using TroveKeep.Core.Interfaces.Services;
using TroveKeep.Core.Models;

namespace TroveKeep.Services;

/// <summary>
/// Builds the label JSON document for a bulk piece in the label-tool file format.
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

    public string BuildBulkPieceLabel(BulkPiece piece, int? copies = null, string? size = null)
    {
        var effectiveCopies = copies is > 0 ? copies.Value : _settings.DefaultCopies;
        var effectiveSize = string.IsNullOrWhiteSpace(size) ? _settings.DefaultSize : size;

        var lines = new List<object>
        {
            TitleFor(piece),
        };

        if (!string.IsNullOrWhiteSpace(piece.Description))
            lines.Add(piece.Description);

        lines.Add(new LabelCodeLine("qr", LabelCodes.ForPiece(piece.LegoId, piece.LegoColorId)));

        // Note: image support is intentionally omitted until label-tool supports an
        // "image" field (label-tool issue #13). The piece image (GET /api/bulkpieces/{id}/image)
        // can be added here once available.
        var labelFile = new LabelFile(lines, effectiveCopies, effectiveSize);
        return JsonSerializer.Serialize(labelFile, JsonOptions);
    }

    public string GetBulkPieceFileName(BulkPiece piece)
    {
        var safeLegoId = Sanitize(piece.LegoId);
        return $"piece-{safeLegoId}-{piece.LegoColorId}.json";
    }

    private string TitleFor(BulkPiece piece) =>
        string.IsNullOrWhiteSpace(_settings.Prefix)
            ? piece.LegoId
            : $"{_settings.Prefix} {piece.LegoId}";

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
