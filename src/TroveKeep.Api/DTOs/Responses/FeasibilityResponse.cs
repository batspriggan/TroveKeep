namespace TroveKeep.Api.DTOs.Responses;

public record FeasibilityLine(
    Guid BaseplateId,
    string Name,
    string Type,
    int Need,
    int Quantity,
    int Reserved,
    int Available,
    int Deficit,
    string Status);

public record FeasibilityResponse(
    IEnumerable<FeasibilityLine> Lines,
    int TotalDeficit,
    bool HasShortage);
