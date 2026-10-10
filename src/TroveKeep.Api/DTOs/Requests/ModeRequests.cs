namespace TroveKeep.Api.DTOs.Requests;

/// <summary>
/// Post-setup switch between single-user and multi-user mode. Unlike the first-run setup this is
/// not gated by the setup token: the instance is already initialized and the caller is an
/// authenticated administrator.
/// </summary>
public record SetModeRequest(bool MultiUserEnabled);
