namespace TroveKeep.Api.DTOs.Requests;

public record CreateBoxRequest(string Name, string? PhotoUrl);

public record UpdateBoxRequest(string Name, string? PhotoUrl);
