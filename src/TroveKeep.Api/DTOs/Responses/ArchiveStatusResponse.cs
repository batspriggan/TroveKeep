namespace TroveKeep.Api.DTOs.Responses;

public record ArchiveStatusResponse(int Count, DateTimeOffset? LastImportedAt);
