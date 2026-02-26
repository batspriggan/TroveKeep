namespace TroveKeep.Api.DTOs.Requests;

public record CreateLegoSetRequest(string SetNumber, string Description, string? PhotoUrl);

public record UpdateLegoSetRequest(string SetNumber, string Description, string? PhotoUrl);
