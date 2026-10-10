namespace TroveKeep.Api.DTOs.Requests;

/// <summary>
/// First-run setup. When <see cref="MultiUser"/> is true the technical account seeded by migration
/// 006 is overwritten with the supplied credentials; when false the instance stays single-user.
/// <see cref="Token"/> is the transient setup token printed in the startup logs.
/// </summary>
public record SetupRequest(bool MultiUser, string? Email, string? Password, string? Token);
