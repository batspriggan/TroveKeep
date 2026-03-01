namespace TroveKeep.Api.DTOs.Responses;

public record PlacedTableResponse(Guid InstanceId, Guid TemplateId, int XCm, int YCm);

public record AggregateSelectionResponse(string RepresentativeId, string BpKey);

public record RoomResponse(
    Guid Id,
    string Name,
    int WidthCm,
    int DepthCm,
    IEnumerable<PlacedTableResponse> Layout,
    IEnumerable<AggregateSelectionResponse> AggregateSelections,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
