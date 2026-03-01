namespace TroveKeep.Api.DTOs.Requests;

public record CreateRoomRequest(string Name, int WidthCm, int DepthCm);

public record UpdateRoomRequest(string Name, int WidthCm, int DepthCm);

public record SaveRoomLayoutRequest(IEnumerable<PlacedTableRequest> Layout);

public record PlacedTableRequest(Guid InstanceId, Guid TemplateId, int XCm, int YCm);
