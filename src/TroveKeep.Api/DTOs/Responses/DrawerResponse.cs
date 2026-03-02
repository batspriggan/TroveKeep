namespace TroveKeep.Api.DTOs.Responses;

public record DrawerResponse(
    Guid Id,
    int Position,
    string? Label,
    Guid DrawerContainerId,
    int BulkPieceCount,
    IEnumerable<string>? ContentSummary,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public record DrawerDetailResponse(
    Guid Id,
    int Position,
    string? Label,
    Guid DrawerContainerId,
    IEnumerable<BulkPieceResponse> BulkPieces,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
