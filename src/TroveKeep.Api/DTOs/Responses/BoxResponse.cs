namespace TroveKeep.Api.DTOs.Responses;

public record BoxResponse(
    Guid Id,
    string Name,
    bool ImageCached,
    int SetCount,
    int BulkPieceCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    int Version);

public record BoxDetailResponse(
    Guid Id,
    string Name,
    bool ImageCached,
    IEnumerable<LegoSetResponse> Sets,
    IEnumerable<BulkPieceResponse> BulkPieces,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    int Version);
