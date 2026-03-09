namespace TroveKeep.Api.DTOs.Requests;

public record CreateRoomRequest(string Name, int WidthCm, int DepthCm);

public record UpdateRoomRequest(string Name, int WidthCm, int DepthCm, int Version = 0);

public record SaveRoomLayoutRequest(IEnumerable<PlacedTableRequest> Layout, IEnumerable<AggregateSelectionRequest> AggregateSelections, int Version = 0);

public record PlacedTableRequest(Guid InstanceId, Guid TemplateId, int XCm, int YCm);

public record AggregateSelectionRequest(string RepresentativeId, string BpKey);
