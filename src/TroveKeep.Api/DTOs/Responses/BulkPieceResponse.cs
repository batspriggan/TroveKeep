namespace TroveKeep.Api.DTOs.Responses;

public record BulkPieceResponse(
    Guid Id,
    string LegoId,
    string LegoColor,
    string Description,
    Guid? BoxId,
    Guid? DrawerId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
