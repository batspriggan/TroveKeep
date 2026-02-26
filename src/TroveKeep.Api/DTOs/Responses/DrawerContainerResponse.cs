namespace TroveKeep.Api.DTOs.Responses;

public record DrawerContainerResponse(
    Guid Id,
    string Name,
    string? Description,
    int DrawerCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public record DrawerContainerDetailResponse(
    Guid Id,
    string Name,
    string? Description,
    IEnumerable<DrawerResponse> Drawers,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
