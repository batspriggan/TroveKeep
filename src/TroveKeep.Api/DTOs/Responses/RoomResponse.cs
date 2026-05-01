namespace TroveKeep.Api.DTOs.Responses;

public record PlacedTableResponse(Guid InstanceId, Guid TemplateId, int XCm, int YCm);

public record AggregateSelectionResponse(string RepresentativeId, string BpKey);

public record PlacedBaseplateResponse(Guid InstanceId, Guid BaseplateId, int XMm, int YMm, int Rotation);

public record AggregateBpLayoutResponse(string RepresentativeId, IEnumerable<PlacedBaseplateResponse> PlacedBaseplates);

public record RoomResponse(
    Guid Id,
    string Name,
    int WidthCm,
    int DepthCm,
    IEnumerable<PlacedTableResponse> Layout,
    IEnumerable<AggregateSelectionResponse> AggregateSelections,
    IEnumerable<AggregateBpLayoutResponse> AggregateBpLayouts,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    int Version);
