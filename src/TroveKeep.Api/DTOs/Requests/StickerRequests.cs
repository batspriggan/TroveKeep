namespace TroveKeep.Api.DTOs.Requests;

/// <summary>A new entry in the global sticker image library: artwork plus a display name.</summary>
public record CreateStickerImageRequest(string Name);

/// <summary>
/// Renames an image. <c>Version</c> is the one the client read: a mismatch answers 409 so two tabs
/// cannot overwrite each other silently.
/// </summary>
public record UpdateStickerImageRequest(string Name, int Version = 0);

/// <summary>
/// One item of a sticker project. <c>Mount</c> is the enum NAME ("Tile" | "BrickStack") and defaults
/// to <c>Tile</c> when omitted, so a client that predates the mounts keeps producing tiles:
/// <c>WidthStuds × DepthStuds</c> studs for a tile, <c>WidthStuds</c> studs × <c>BrickHeight</c>
/// bricks for a stack (a stack ignores <c>DepthStuds</c>: the server forces it to 1). <c>Fit</c> is
/// the enum NAME ("Contain" | "Cover" | "Fill" | "Center") and defaults to <c>Contain</c> when
/// omitted; <c>Orientation</c> is the enum NAME ("Auto" | "Landscape" | "Portrait") and defaults to
/// <c>Auto</c> when omitted — it turns the artwork inside the label, never the label itself. <c>Id</c>
/// is optional and, when present, is kept so a client can track the row across an update.
/// </summary>
public record StickerItemRequest(
    Guid? Id,
    Guid ImageId,
    int WidthStuds,
    int DepthStuds,
    int Copies = 1,
    string? Fit = null,
    string? Orientation = null,
    string? Notes = null,
    string? Mount = null,
    decimal? BrickHeight = null);

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
