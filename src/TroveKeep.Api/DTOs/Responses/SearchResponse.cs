namespace TroveKeep.Api.DTOs.Responses;

public record SearchResponse(
    IEnumerable<SetSearchResponse> Sets,
    IEnumerable<PieceSearchResponse> Pieces);

public record SetSearchResponse(
    Guid Id,
    string SetNumber,
    string Description,
    int Quantity,
    IEnumerable<ResolvedAllocationResponse> Allocations);

public record PieceSearchResponse(
    Guid Id,
    string LegoId,
    string LegoColor,
    string Description,
    int Quantity,
    IEnumerable<ResolvedAllocationResponse> Allocations);

public record ResolvedAllocationResponse(
    Guid StorageId,
    string StorageType,
    string StorageName,
    Guid? DrawerContainerId,
    string? DrawerContainerName,
    int? DrawerPosition,
    int Quantity);
