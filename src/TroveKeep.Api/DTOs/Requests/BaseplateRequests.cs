namespace TroveKeep.Api.DTOs.Requests;

public record CreateBaseplateRequest(
    string Type,
    string PartNum,
    string Name,
    int WidthStuds,
    int DepthStuds,
    int LegoColorId,
    Guid? LinkedSetId);
