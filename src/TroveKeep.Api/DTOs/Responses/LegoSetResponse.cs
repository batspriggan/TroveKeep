namespace TroveKeep.Api.DTOs.Responses;

public record LegoSetResponse(
    Guid Id,
    string SetNumber,
    string Description,
    string? PhotoUrl,
    int Quantity,
    Guid? BoxId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
