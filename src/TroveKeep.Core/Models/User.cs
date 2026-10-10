namespace TroveKeep.Core.Models;

/// <summary>
/// An application account. Accounts are local by default (email + password hash); an account
/// may additionally be linked to an external identity provider (e.g. Pangolin via OIDC) through
/// <see cref="ExternalProvider"/>/<see cref="ExternalSubject"/>.
///
/// <para>
/// <see cref="PasswordHash"/> is <c>null</c> for accounts created purely by auto-provisioning
/// from an external provider. Such an account can still set a local fallback password.
/// </para>
/// </summary>
public class User
{
    /// <summary>Emails are stored lower-cased and trimmed so lookups are case-insensitive.</summary>
    public static string NormalizeEmail(string? email) => (email ?? string.Empty).Trim().ToLowerInvariant();

    public Guid Id { get; set; }

    /// <summary>Login identifier. Stored lower-cased: lookups are case-insensitive.</summary>
    public string Email { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    /// <summary>ASP.NET Core <c>PasswordHasher</c> output. Null for external-only accounts.</summary>
    public string? PasswordHash { get; set; }

    public UserRole Role { get; set; } = UserRole.Guest;

    public bool Active { get; set; } = true;

    /// <summary>
    /// Technical account created by migration 006 to own the default collection before the first
    /// run is configured. It is never loggable (<see cref="PasswordHash"/> is null and
    /// <see cref="Active"/> is false) and exists so that single-user mode has an owner to operate
    /// as. The first-run setup overwrites it (and clears this flag) when multi-user is enabled.
    /// </summary>
    public bool IsSystem { get; set; }

    /// <summary>External identity provider name (e.g. <c>"oidc"</c>), null for local-only accounts.</summary>
    public string? ExternalProvider { get; set; }

    /// <summary>Subject claim (<c>sub</c>) at the external provider, null for local-only accounts.</summary>
    public string? ExternalSubject { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
