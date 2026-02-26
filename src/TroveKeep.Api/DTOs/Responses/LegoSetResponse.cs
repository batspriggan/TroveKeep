namespace TroveKeep.Api.DTOs.Responses;

public record LegoSetResponse(
    Guid Id,
    string SetNumber,
    string Description,
    string? PhotoUrl,
    Guid? BoxId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
