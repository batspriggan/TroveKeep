namespace TroveKeep.Api.DTOs.Responses;

public record BulkPieceResponse(
    Guid Id,
    string LegoId,
    string LegoColor,
    string Description,
    int Quantity,
    Guid? BoxId,
    Guid? DrawerId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
