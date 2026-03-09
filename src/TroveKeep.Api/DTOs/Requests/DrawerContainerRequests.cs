namespace TroveKeep.Api.DTOs.Requests;

public record CreateDrawerContainerRequest(string Name, string? Description, int DrawerCount);

public record UpdateDrawerContainerRequest(string Name, string? Description, int DrawerCount, int Version = 0);
