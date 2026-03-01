namespace TroveKeep.Api.DTOs.Requests;

public record CreateBaseplateRequest(string PartNum, string Name, int WidthStuds, int DepthStuds, int LegoColorId);
