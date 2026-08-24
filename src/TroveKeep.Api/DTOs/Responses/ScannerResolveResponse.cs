namespace TroveKeep.Api.DTOs.Responses;

public record ScannerResolveResponse(
    Guid Id,
    string LegoId,
    int LegoColorId,
    string? LegoColorName,
    string? LegoColorRgb,
    string Description,
    int Quantity,
    IEnumerable<ScannerAllocationResponse> Allocations);

public record ScannerAllocationResponse(
    string StorageType,
    Guid StorageId,
    string StorageName,
    Guid? DrawerContainerId,
    string? DrawerContainerName,
    int? DrawerPosition,
    int Quantity);
