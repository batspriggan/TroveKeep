namespace TroveKeep.Api.DTOs.Responses;

public record BaseplateResponse(Guid Id, string PartNum, string Name, int WidthStuds, int DepthStuds, int LegoColorId, string? LegoColorName, string? LegoColorRgb, DateTimeOffset CreatedAt);
