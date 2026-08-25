namespace TroveKeep.Api.DTOs.Requests;

public record AllocateStorageRequest(int Quantity = 1);

public record MoveDrawerRequest(Guid DestContainerId, int DestPosition);
