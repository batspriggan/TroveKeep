namespace TroveKeep.Api.DTOs.Responses;

public record LegoSetResponse(
    Guid Id,
    string SetNumber,
    string Description,
    string? PhotoUrl,
    int Quantity,
    bool ImageCached,
    IEnumerable<StorageAllocationResponse> StorageAllocations,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
