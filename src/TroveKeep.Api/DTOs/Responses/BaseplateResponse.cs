namespace TroveKeep.Api.DTOs.Responses;

public record ReservationResponse(Guid SetId, string? SetDescription, int Quantity);

/// <summary>Result of reconciling (dissolving) a legacy MOC row.</summary>
public record DissolveResponse(bool Dissolved, int PlatesCreated, int PlacementsRewritten, string? Reason);

/// <summary>A reservation of one baseplate type by a set/MOC (denormalised for the set detail page).</summary>
public record SetReservationResponse(
    Guid BaseplateId,
    string PlateName,
    string Type,
    int WidthStuds,
    int DepthStuds,
    int LegoColorId,
    string? LegoColorName,
    string? LegoColorRgb,
    int Quantity,
    string? SourceSetId,
    DateTimeOffset CreatedAt);

/// <summary>A set/MOC that owns baseplate reservations, as a placeable planner entity.</summary>
public record PlannerEntityResponse(
    Guid SetId,
    string Name,
    bool IsMoc,
    Guid ModuleBaseplateId,
    int ModuleWidthStuds,
    int ModuleDepthStuds,
    int TotalPlates,
    int Cols,
    int Rows,
    int FootprintWidthStuds,
    int FootprintDepthStuds,
    bool Quarantined);

public record BaseplateResponse(
    Guid Id,
    string Type,
    string PartNum,
    string Name,
    int WidthStuds,
    int DepthStuds,
    int LegoColorId,
    string? LegoColorName,
    string? LegoColorRgb,
    bool ImageCached,
    Guid? LinkedSetId,
    string? RoadShape,
    int Quantity,
    int ReservedQuantity,
    int AvailableQuantity,
    int InLayoutQuantity,
    bool NeedsReview,
    string? Notes,
    IEnumerable<ReservationResponse> Reservations,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    int Version,
    bool Quarantined,
    string? QuarantineReason,
    bool OverReserved);
