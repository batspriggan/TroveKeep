namespace TroveKeep.Api.DTOs.Responses;

public record DrawerResponse(
    int Position,
    string? Label,
    Guid DrawerContainerId,
    int BulkPieceCount,
    IEnumerable<string>? ContentSummary,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public record DrawerDetailResponse(
    int Position,
    string? Label,
    Guid DrawerContainerId,
    IEnumerable<BulkPieceResponse> BulkPieces,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
