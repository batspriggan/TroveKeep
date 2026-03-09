namespace TroveKeep.Api.DTOs.Responses;

public record LegoSetResponse(
    Guid Id,
    string SetNumber,
    string Description,
    int Quantity,
    bool IsMoc,
    bool ImageCached,
    int PhotoCount,
    IEnumerable<StorageAllocationResponse> StorageAllocations,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    int Version);
