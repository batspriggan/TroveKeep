namespace TroveKeep.Core.Interfaces.Services;

/// <summary>
/// Application-wide mode, persisted in <c>meta/_id="app_config"</c>.
/// <para>
/// <see cref="Initialized"/> is false until the first-run setup completes: while false every
/// <c>/api</c> endpoint except <c>/api/setup/*</c> is refused with 503.
/// </para>
/// <para>
/// <see cref="MultiUserEnabled"/> is false for a single-user instance: login is disabled and every
/// request is served as the technical admin account.
/// </para>
/// </summary>
public readonly record struct AppState(bool Initialized, bool MultiUserEnabled);

/// <summary>
/// Cached, singleton view of <see cref="AppState"/> plus the transient setup token. The cache is
/// explicitly invalidated by the setup endpoint; the token is regenerated on every startup while
/// the instance still needs configuring.
/// </summary>
public interface IAppStateService
{
    Task<AppState> GetAsync(CancellationToken ct = default);

    /// <summary>Persists the new mode and refreshes the cache. Clears the setup token.</summary>
    Task SetAsync(bool initialized, bool multiUserEnabled, CancellationToken ct = default);

    /// <summary>Drops the cached state so the next read hits the database.</summary>
    void Invalidate();

    /// <summary>
    /// Token required by <c>POST /api/setup</c>. Null once the instance has been configured.
    /// Never exposed by <c>GET /api/setup/status</c> itself, only via <see cref="RequiresSetupToken"/>.
    /// </summary>
    string? SetupToken { get; }

    /// <summary>True while a setup token is available, i.e. the instance can still be configured.</summary>
    bool RequiresSetupToken { get; }

    /// <summary>Returns the current token, generating one on first call.</summary>
    string EnsureSetupToken();
}
