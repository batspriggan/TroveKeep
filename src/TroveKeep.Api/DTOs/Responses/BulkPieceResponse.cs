namespace TroveKeep.Api.DTOs.Responses;

public record BulkPieceResponse(
    Guid Id,
    string LegoId,
    string LegoColor,
    string Description,
    int Quantity,
    IEnumerable<StorageAllocationResponse> StorageAllocations,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
