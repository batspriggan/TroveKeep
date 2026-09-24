namespace TroveKeep.Api.DTOs.Requests;

public record FeasibilityRequest(IEnumerable<FeasibilityAggregate> Aggregates);

public record FeasibilityAggregate(Guid RoomId, string RepresentativeId);
