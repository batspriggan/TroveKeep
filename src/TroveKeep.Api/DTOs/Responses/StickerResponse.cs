namespace TroveKeep.Api.DTOs.Responses;

/// <summary>
/// An entry of the global sticker image library. <c>ImageCached</c> tells the SPA whether fetching
/// the artwork is worth a request.
/// </summary>
public record StickerImageResponse(
    Guid Id,
    string Name,
    bool ImageCached,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    int Version);

/// <summary>
/// One item of a sticker project. <c>WidthMm</c>/<c>DepthMm</c> are the physical label size
/// (studs × 8 mm) and <c>ImageName</c>/<c>ImageCached</c> are resolved from the image library, so the
/// SPA never does the maths or the join itself. <c>ImageName</c> is null when the image vanished,
/// <c>Fit</c> is the enum name ("Contain" | "Cover" | "Fill" | "Center") and <c>Orientation</c> the
/// item's artwork orientation as a name ("Auto" | "Landscape" | "Portrait").
/// </summary>
public record StickerItemResponse(
    Guid Id,
    Guid ImageId,
    string? ImageName,
    bool ImageCached,
    int WidthStuds,
    int DepthStuds,
    int WidthMm,
    int DepthMm,
    int Copies,
    string Fit,
    string Orientation,
    string? Notes);

/// <summary>A sticker project with its embedded items.</summary>
public record StickerProjectResponse(
    Guid Id,
    string Name,
    IEnumerable<StickerItemResponse> Items,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    int Version);
