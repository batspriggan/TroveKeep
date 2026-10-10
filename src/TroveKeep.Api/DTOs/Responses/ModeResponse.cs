namespace TroveKeep.Api.DTOs.Responses;

/// <summary>Application mode after a successful switch. Mirrors the fields of the setup status.</summary>
public record AppModeResponse(bool Initialized, bool MultiUserEnabled);
