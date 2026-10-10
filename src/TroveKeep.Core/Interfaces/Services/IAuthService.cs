using TroveKeep.Core.Models;

namespace TroveKeep.Core.Interfaces.Services;

/// <summary>Outcome of a credential check. Avoids leaking whether the email exists.</summary>
public record AuthResult(bool Success, User? User, string? Error)
{
    public static AuthResult Ok(User user) => new(true, user, null);
    public static AuthResult Fail(string error = "Invalid email or password.") => new(false, null, error);
}

public interface IAuthService
{
    /// <summary>Verifies email/password. Failure is uniform for unknown email and wrong password.</summary>
    Task<AuthResult> ValidateCredentialsAsync(string email, string password, CancellationToken ct = default);

    Task<bool> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken ct = default);

    /// <summary>Resolves (and, when allowed, auto-provisions) the local account behind an external identity.</summary>
    Task<AuthResult> ResolveExternalUserAsync(string provider, string subject, string? email, string displayName, CancellationToken ct = default);
}
