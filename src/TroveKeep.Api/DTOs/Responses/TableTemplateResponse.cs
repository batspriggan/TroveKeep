namespace TroveKeep.Api.DTOs.Responses;

public record TableTemplateResponse(
    Guid Id,
    string Description,
    int WidthCm,
    int DepthCm,
    string Color,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
