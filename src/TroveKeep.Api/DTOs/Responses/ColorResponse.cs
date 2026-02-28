namespace TroveKeep.Api.DTOs.Responses;

public record ColorResponse(Guid UniqueId, int Id, string Name, string Rgb, bool IsTrans, int? StartYear, int? EndYear);
