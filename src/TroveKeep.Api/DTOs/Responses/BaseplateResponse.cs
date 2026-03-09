namespace TroveKeep.Api.DTOs.Responses;

public record BaseplateResponse(
    Guid Id,
    string Type,
    string PartNum,
    string Name,
    int WidthStuds,
    int DepthStuds,
    int LegoColorId,
    string? LegoColorName,
    string? LegoColorRgb,
    bool ImageCached,
    Guid? LinkedSetId,
    DateTimeOffset CreatedAt);
