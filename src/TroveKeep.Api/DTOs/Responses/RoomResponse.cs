namespace TroveKeep.Api.DTOs.Responses;

public record PlacedTableResponse(Guid InstanceId, Guid TemplateId, int XCm, int YCm);

public record RoomResponse(
    Guid Id,
    string Name,
    int WidthCm,
    int DepthCm,
    IEnumerable<PlacedTableResponse> Layout,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
