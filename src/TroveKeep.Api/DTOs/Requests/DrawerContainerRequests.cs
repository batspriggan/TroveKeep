namespace TroveKeep.Api.DTOs.Requests;

public record CreateDrawerContainerRequest(string Name, string? Description);

public record UpdateDrawerContainerRequest(string Name, string? Description);
