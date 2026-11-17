namespace TroveKeep.Api.DTOs.Requests;

/// <summary>A new entry in the global sticker image library: artwork plus a display name.</summary>
public record CreateStickerImageRequest(string Name);

/// <summary>
/// Renames an image. <c>Version</c> is the one the client read: a mismatch answers 409 so two tabs
/// cannot overwrite each other silently.
/// </summary>
public record UpdateStickerImageRequest(string Name, int Version = 0);

/// <summary>
/// One item of a sticker project. <c>Fit</c> is the enum NAME ("Contain" | "Cover" | "Fill" |
/// "Center") and defaults to <c>Contain</c> when omitted; <c>Id</c> is optional and, when present, is
/// kept so a client can track the row across an update.
/// </summary>
public record StickerItemRequest(
    Guid? Id,
    Guid ImageId,
    int WidthStuds,
    int DepthStuds,
    int Copies = 1,
    string? Fit = null,
    string? Notes = null);

/// <summary>A new sticker project. The item list may be empty: a project is filled later.</summary>
public record CreateStickerProjectRequest(string Name, List<StickerItemRequest>? Items = null);

/// <summary>
/// Edits a sticker project; the item list is replaced wholesale (supplied item ids are kept).
/// <c>Version</c> guards against a silent overwrite from a second tab (409 on mismatch).
/// </summary>
public record UpdateStickerProjectRequest(
    string Name,
    List<StickerItemRequest>? Items = null,
    int Version = 0);
