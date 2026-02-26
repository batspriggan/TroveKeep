namespace TroveKeep.Api.DTOs.Requests;

public record CreateDrawerRequest(int Position, string? Label);

public record UpdateDrawerRequest(int Position, string? Label);
