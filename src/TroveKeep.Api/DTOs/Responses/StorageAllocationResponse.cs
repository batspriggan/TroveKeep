namespace TroveKeep.Api.DTOs.Responses;

public record StorageAllocationResponse(Guid StorageId, string StorageType, int Quantity);
