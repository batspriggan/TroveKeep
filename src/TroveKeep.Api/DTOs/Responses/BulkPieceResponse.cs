namespace TroveKeep.Api.DTOs.Responses;

public record BulkPieceResponse(
    Guid Id,
    string LegoId,
    int LegoColorId,
    string? LegoColorName,
    string? LegoColorRgb,
    string Description,
    int Quantity,
    IEnumerable<StorageAllocationResponse> StorageAllocations,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
