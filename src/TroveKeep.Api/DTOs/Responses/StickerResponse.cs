namespace TroveKeep.Api.DTOs.Responses;

/// <summary>
/// A sticker of the global library. <c>WidthMm</c>/<c>DepthMm</c> are the physical label size:
/// the studs × 8 mm the PDF engine uses, sent so the SPA does not duplicate the constant.
/// </summary>
public record StickerResponse(
    Guid Id,
    string Name,
    int WidthStuds,
    int DepthStuds,
    int WidthMm,
    int DepthMm,
    bool ImageCached,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    int Version);
