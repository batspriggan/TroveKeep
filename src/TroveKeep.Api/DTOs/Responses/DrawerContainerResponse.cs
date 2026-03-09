namespace TroveKeep.Api.DTOs.Responses;

public record DrawerContainerResponse(
    Guid Id,
    string Name,
    string? Description,
    bool ImageCached,
    int DrawerCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    int Version);

public record DrawerContainerDetailResponse(
    Guid Id,
    string Name,
    string? Description,
    bool ImageCached,
    IEnumerable<DrawerResponse> Drawers,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    int Version);
