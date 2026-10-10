namespace TroveKeep.Api.Auth;

/// <summary>
/// Configuration for the optional OpenID Connect login (Pangolin), bound from the
/// <c>Auth:Oidc</c> section. When it is not fully configured, the OIDC scheme is not
/// registered at all and the application runs exactly as it did before this feature existed
/// (local cookie auth only).
/// </summary>
public sealed class OidcSettings
{
    public const string SectionName = "Auth:Oidc";

    /// <summary>Authentication scheme name used by the challenge endpoint and the callback.</summary>
    public const string Scheme = "oidc";

    /// <summary>Path the middleware handles for the authorization-code round-trip.</summary>
    public const string CallbackPath = "/api/auth/oidc/callback";

    /// <summary>Master switch. Kept explicit so credentials alone do not silently enable login.</summary>
    public bool Enabled { get; set; }

    /// <summary>Issuer URL of the Pangolin OIDC provider (discovery document root).</summary>
    public string Authority { get; set; } = string.Empty;

    public string ClientId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>Space-separated scopes requested from the provider.</summary>
    public string Scopes { get; set; } = "openid profile email";

    /// <summary>
    /// Whether an unknown external identity may create a local <c>Guest</c> account. Handled by
    /// <c>AuthService.ResolveExternalUserAsync</c>; kept in config to document the behaviour.
    /// </summary>
    public bool AutoProvision { get; set; } = true;

    /// <summary>Must stay <c>true</c> in production; relaxed only for local development.</summary>
    public bool RequireHttpsMetadata { get; set; } = true;

    private bool HasCredentials =>
        !string.IsNullOrWhiteSpace(Authority) && !string.IsNullOrWhiteSpace(ClientId);

    /// <summary>
    /// OIDC is active only when it is explicitly enabled <b>and</b> the provider coordinates are
    /// present. Enabling without an authority/client id would make the handler throw at startup,
    /// so it is treated as disabled with a warning instead.
    /// </summary>
    public bool IsEnabled => Enabled && HasCredentials;

    /// <summary>Parses <see cref="Scopes"/> into the list the handler expects.</summary>
    public IEnumerable<string> ParsedScopes => Scopes
        .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Distinct(StringComparer.Ordinal);
}
