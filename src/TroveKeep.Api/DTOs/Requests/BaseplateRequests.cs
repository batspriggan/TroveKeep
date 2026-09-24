namespace TroveKeep.Api.DTOs.Requests;

public record CreateBaseplateRequest(
    string Type,
    string PartNum,
    string Name,
    int WidthStuds,
    int DepthStuds,
    int LegoColorId,
    Guid? LinkedSetId,
    string? RoadShape,
    int Quantity = 1,
    string? Notes = null);

public record UpdateBaseplateRequest(
    string Type,
    string PartNum,
    string Name,
    int WidthStuds,
    int DepthStuds,
    int LegoColorId,
    Guid? LinkedSetId,
    string? RoadShape,
    int? Quantity,
    string? Notes);

public record CreateReservationRequest(Guid SetId, int Quantity);

/// <summary>Reconciles a legacy MOC row: decompose it into <paramref name="TargetBaseplateId"/> plates.</summary>
public record ReconcileRequest(Guid TargetBaseplateId, int? Cols, int? Rows);

/// <summary>Clears a quarantine flag; <c>ConfirmAsPhysicalPlate</c> also drops the linked set.</summary>
public record UnquarantineRequest(bool ConfirmAsPhysicalPlate);
