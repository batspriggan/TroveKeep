namespace TroveKeep.Api.DTOs.Responses;

public record ScannerResolveResponse(
    string Kind,
    Guid Id,
    string Title,
    string? Subtitle,
    string? ColorName,
    string? ColorRgb,
    int Quantity,
    string? TargetStorageType,
    Guid? TargetStorageId,
    int? TargetStoragePosition,
    IEnumerable<ScannerAllocationResponse> Allocations);

public record ScannerAllocationResponse(
    string StorageType,
    Guid StorageId,
    string StorageName,
    Guid? DrawerContainerId,
    string? DrawerContainerName,
    int? DrawerPosition,
    int Quantity);
