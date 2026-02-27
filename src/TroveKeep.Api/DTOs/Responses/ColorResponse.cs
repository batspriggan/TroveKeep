namespace TroveKeep.Api.DTOs.Responses;

public record ColorResponse(int Id, string Name, string Rgb, bool IsTrans, int? StartYear, int? EndYear);
