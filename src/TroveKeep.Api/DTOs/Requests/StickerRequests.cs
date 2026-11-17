namespace TroveKeep.Api.DTOs.Requests;

/// <summary>A new sticker in the global library: name plus the LEGO tile footprint in studs.</summary>
public record CreateStickerRequest(string Name, int WidthStuds, int DepthStuds);

/// <summary>
/// Edits a sticker. <c>Version</c> is the one the client read: a mismatch answers 409 so two tabs
/// cannot overwrite each other silently.
/// </summary>
public record UpdateStickerRequest(string Name, int WidthStuds, int DepthStuds, int Version = 0);
