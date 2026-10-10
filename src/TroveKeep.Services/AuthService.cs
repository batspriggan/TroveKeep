using Microsoft.AspNetCore.Identity;
using TroveKeep.Core.Interfaces.Repositories;
using TroveKeep.Core.Interfaces.Services;
using TroveKeep.Core.Models;

namespace TroveKeep.Services;

/// <summary>
/// Local credential handling plus external-identity resolution. Passwords are hashed with
/// <see cref="PasswordHasher{TUser}"/> (PBKDF2, versioned) — no full ASP.NET Core Identity stack
/// is pulled in, only the hasher that ships with the framework.
/// </summary>
public class AuthService : IAuthService
{
    private readonly IUserRepository _users;
    private readonly PasswordHasher<User> _hasher = new();

    /// <summary>Default provider name for externally-provisioned accounts.</summary>
    public const string OidcProvider = "oidc";

    public AuthService(IUserRepository users)
    {
        _users = users;
    }

    public async Task<AuthResult> ValidateCredentialsAsync(string email, string password, CancellationToken ct = default)
    {
        var user = await _users.GetByEmailAsync(email);
        if (user is null || user.PasswordHash is null)
            return AuthResult.Fail();

        if (!user.Active)
            return AuthResult.Fail("Account is disabled.");

        var result = _hasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (result == PasswordVerificationResult.Failed)
            return AuthResult.Fail();

        // Transparently rehash when the hashing parameters have moved on.
        if (result == PasswordVerificationResult.SuccessRehashNeeded)
            await SetPasswordAsync(user.Id, password);

        return AuthResult.Ok(user);
    }

    public async Task<bool> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken ct = default)
    {
        var user = await _users.GetByIdAsync(userId);
        if (user is null) return false;

        // An external-only account has no current password to verify: setting the first one is allowed.
        if (user.PasswordHash is not null)
        {
            var verify = _hasher.VerifyHashedPassword(user, user.PasswordHash, currentPassword);
            if (verify == PasswordVerificationResult.Failed) return false;
        }

        await SetPasswordAsync(userId, newPassword);
        return true;
    }

    public async Task<AuthResult> ResolveExternalUserAsync(
        string provider, string subject, string? email, string displayName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(subject))
            return AuthResult.Fail("External identity is missing the subject claim.");

        var user = await _users.GetByEmailAsync(email ?? string.Empty);

        // No email we can key on: we cannot safely match or create an account.
        if (user is null && string.IsNullOrWhiteSpace(email))
            return AuthResult.Fail("External identity did not provide an email address.");

        if (user is null)
        {
            user = await _users.CreateAsync(new User
            {
                Email = email!,
                DisplayName = string.IsNullOrWhiteSpace(displayName) ? email! : displayName,
                PasswordHash = null,
                // Auto-provisioned accounts start with the least privilege: an admin must
                // promote them and add them to a collection.
                Role = UserRole.Guest,
                Active = true,
                ExternalProvider = provider,
                ExternalSubject = subject,
            });
            return AuthResult.Ok(user);
        }

        if (!user.Active)
            return AuthResult.Fail("Account is disabled.");

        // Link the external identity on first successful external login.
        if (user.ExternalSubject is null)
        {
            user.ExternalProvider = provider;
            user.ExternalSubject = subject;
            user = await _users.UpdateAsync(user) ?? user;
        }
        else if (!string.Equals(user.ExternalSubject, subject, StringComparison.Ordinal))
        {
            return AuthResult.Fail("This email is already linked to a different external identity.");
        }

        return AuthResult.Ok(user);
    }

    private async Task SetPasswordAsync(Guid userId, string password)
    {
        var user = await _users.GetByIdAsync(userId);
        if (user is null) return;
        user.PasswordHash = _hasher.HashPassword(user, password);
        await _users.UpdateAsync(user);
    }
}
