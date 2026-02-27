namespace TroveKeep.Api.DTOs.Requests;

public record CreateLegoSetRequest(string SetNumber, string Description, string? PhotoUrl, int Quantity = 1);

public record UpdateLegoSetRequest(string SetNumber, string Description, string? PhotoUrl, int Quantity = 1);
