using System.Security.Claims;
using TroveKeep.Core.Interfaces.Repositories;
using TroveKeep.Core.Models;

namespace TroveKeep.Api.Auth;

/// <summary>
/// Builds (and caches) the principal single-user mode operates as: the technical admin account
/// seeded by migration 006. Constructing a principal needs scoped repositories, so the provider
/// itself is a singleton that opens a short-lived scope on first use.
///
/// <para>
/// The cache is invalidated by the setup endpoint, because enabling multi-user overwrites the
/// technical account (email, display name, active flag) and the cached identity must not survive
/// that. It is invalidated again by the post-setup mode switch (<c>PUT /api/admin/mode</c>).
/// </para>
///
/// <para>
/// When the first run enabled multi-user, the technical account was promoted in place and its
/// <c>IsSystem</c> flag cleared, so switching back to single-user later finds no system account.
/// In that case the oldest active administrator is used: single-user mode is exactly "operate as
/// an administrator", and the promoted technical account is still that administrator.
/// </para>
/// </summary>
public class TechnicalUserProvider
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private ClaimsPrincipal? _cached;

    public TechnicalUserProvider(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task<ClaimsPrincipal> GetAsync(CancellationToken ct = default)
    {
        if (_cached is { } cached) return cached;

        await _gate.WaitAsync(ct);
        try
        {
            if (_cached is { } raced) return raced;

            using var scope = _scopeFactory.CreateScope();
            var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
            var user = await users.GetSystemUserAsync();
            if (user is null)
            {
                // No system account: the first run promoted it, so fall back to an administrator
                // to let the instance be switched back to single-user from a multi-user setup.
                user = (await users.GetAllAsync())
                    .Where(u => u.Active && u.Role == UserRole.Admin)
                    .OrderBy(u => u.CreatedAt)
                    .FirstOrDefault();
            }

            if (user is null)
                throw new InvalidOperationException(
                    "No technical account found. Migration 006 should have created one; " +
                    "the database may have been altered outside of TroveKeep.");

            var cookies = scope.ServiceProvider.GetRequiredService<AuthCookieFactory>();
            _cached = await cookies.BuildPrincipalAsync(user);
            return _cached;
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Invalidate() => _cached = null;
}
