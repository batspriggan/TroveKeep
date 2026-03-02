namespace TroveKeep.Api.DTOs.Responses;

public record StorageAllocationResponse(Guid StorageId, int? StoragePosition, string StorageType, int Quantity);
