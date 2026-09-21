using TroveKeep.Core.Interfaces.Services;
using TroveKeep.Core.Models;

namespace TroveKeep.Services;

/// <summary>
/// Resolves cached label images into the form the sink needs:
/// <list type="bullet">
///   <item><b>client</b> — an absolute URL under <see cref="LabelPrintSettings.PublicBaseUrl"/>,
///         which <c>label-tool</c> downloads before printing;</item>
///   <item><b>server</b> — inline base64, so the payload is self-sufficient and the
///         label-tool server needs no inbound connectivity to this API.</item>
/// </list>
/// </summary>
public class LabelImageResolver : ILabelImageResolver
{
    public const string ServerKey = "label-server";
    public const string ClientKey = "label-client";

    private readonly IImageService _imageService;
    private readonly LabelPrintSettings _settings;
    private readonly bool _inline;

    public LabelImageResolver(IImageService imageService, LabelPrintSettings settings, bool inline)
    {
        _imageService = imageService;
        _settings = settings;
        _inline = inline;
    }

    public async Task<LabelImage?> ResolvePieceImageAsync(Guid pieceId, string legoId, int legoColorId)
    {
        // Part images are keyed by LegoId + color id.
        var image = _inline ? await _imageService.GetImageAsync(legoId, ImageReferenceType.Part, legoColorId) : null;
        return Build(image, $"piece-{Sanitize(legoId)}-{legoColorId}", $"/api/bulkpieces/{pieceId}/image");
    }

    public async Task<LabelImage?> ResolveSetImageAsync(Guid setId, string setNumber)
    {
        // Set images are keyed by the set id (unique per set; SetNumber is shared by MOCs).
        var image = _inline ? await _imageService.GetImageAsync(setId.ToString(), ImageReferenceType.Set) : null;
        return Build(image, $"set-{Sanitize(setNumber)}", $"/api/sets/{setId}/image");
    }

    private LabelImage? Build(Image? image, string fileStem, string relativePath)
    {
        if (_inline)
        {
            if (image is null) return null;
            var extension = image.ContentType switch
            {
                "image/jpeg" => ".jpg",
                "image/webp" => ".webp",
                _ => ".png",
            };
            return new LabelImage(Url: null, Base64: Convert.ToBase64String(image.Data), FileName: fileStem + extension);
        }

        var url = Url(relativePath);
        return url is null ? null : new LabelImage(Url: url, Base64: null, FileName: fileStem);
    }

    private string? Url(string relativePath) =>
        string.IsNullOrWhiteSpace(_settings.PublicBaseUrl)
            ? null
            : $"{_settings.PublicBaseUrl.TrimEnd('/')}{relativePath}";

    private static string Sanitize(string value) =>
        string.Concat(value.Where(char.IsLetterOrDigit)).ToLowerInvariant();
}
