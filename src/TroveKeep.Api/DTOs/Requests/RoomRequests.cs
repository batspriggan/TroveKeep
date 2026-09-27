namespace TroveKeep.Api.DTOs.Requests;

public record CreateRoomRequest(string Name, int WidthCm, int DepthCm);

public record UpdateRoomRequest(string Name, int WidthCm, int DepthCm, bool Obsolete = false, int Version = 0);

/// <summary>Archives or unarchives a room. Archived rooms are hidden from the room list.</summary>
public record SetRoomObsoleteRequest(bool Obsolete, int Version = 0);

/// <summary>Copies an existing room. A null name falls back to "Copy of &lt;source&gt;".</summary>
public record DuplicateRoomRequest(string? Name = null);

/// <summary>Auto-dimensioning: the room is resized to the tables' exact footprint.</summary>
public record FitRoomRequest(int Version = 0);

public record SaveRoomLayoutRequest(IEnumerable<PlacedTableRequest> Layout, IEnumerable<AggregateSelectionRequest> AggregateSelections, int Version = 0);

public record PlacedTableRequest(Guid InstanceId, Guid TemplateId, double XCm, double YCm, int Rotation = 0);

public record AggregateSelectionRequest(string RepresentativeId, string BpKey);

public record SaveAggregateBpLayoutRequest(IEnumerable<PlacedBaseplateRequest> PlacedBaseplates);

public record PlacedBaseplateRequest(Guid InstanceId, Guid BaseplateId, int XMm, int YMm, int Rotation, Guid? SourceSetId = null, Guid? PlacementId = null);
