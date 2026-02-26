namespace TroveKeep.Api.DTOs.Responses;

public record BoxResponse(
    Guid Id,
    string Name,
    string? PhotoUrl,
    int SetCount,
    int BulkPieceCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public record BoxDetailResponse(
    Guid Id,
    string Name,
    string? PhotoUrl,
    IEnumerable<LegoSetResponse> Sets,
    IEnumerable<BulkPieceResponse> BulkPieces,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
