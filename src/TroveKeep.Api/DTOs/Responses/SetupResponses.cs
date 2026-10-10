namespace TroveKeep.Api.DTOs.Responses;

/// <summary>First-run state of the instance. <see cref="RequiresToken"/> is true while the setup can still run.</summary>
public record SetupStatusResponse(bool Initialized, bool MultiUserEnabled, bool RequiresToken);
