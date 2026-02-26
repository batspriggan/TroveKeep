namespace TroveKeep.Api.DTOs.Responses;

public record StorageLocationResponse(
    string StorageType,
    Guid StorageId,
    string StorageName,
    Guid? DrawerContainerId,
    string? DrawerContainerName,
    int? DrawerPosition);
