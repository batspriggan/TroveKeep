using TroveKeep.Core.Models;

namespace TroveKeep.Core.Interfaces.Services;

/// <summary>
/// Request-scoped cache of the caller's <em>access level</em> per collection, loaded from the
/// <c>collection_members</c> document rather than from the auth cookie.
///
/// <para>
/// The cookie only carries the collection <em>ids</em> the user belongs to (<see cref="CurrentUser"/>),
/// which is enough to scope listings but not to authorize writes. The level is deliberately read
/// from the database for every request instead of being added as a claim: it is an authorization
/// input, and a claim would keep a demoted member (Editor → Viewer, or removed) writing until the
/// cookie is refreshed, which with sliding expiration can be days. The cost is one indexed lookup
/// per request and only for an authenticated non-admin — an admin bypasses the scope entirely and
/// the single-user technical account is an admin, so the common paths pay nothing.
/// </para>
///
/// <para>
/// Loaded by <c>CollectionMembershipMiddleware</c>, which runs after the app-mode middleware (so the
/// technical principal of single-user mode is already in place) and before authorization. Every
/// consumer below can therefore rely on <see cref="IsLoaded"/> being true for an authenticated
/// non-admin request; the <see cref="ICollectionScope"/> accessors stay synchronous.
/// </para>
/// </summary>
public interface ICollectionMembershipCache
{
    /// <summary>True once the memberships of this request have been fetched (or skipped).</summary>
    bool IsLoaded { get; }

    /// <summary>Access level per collection the caller is a member of. Empty when not loaded.</summary>
    IReadOnlyDictionary<Guid, CollectionAccess> AccessByCollection { get; }

    /// <summary>
    /// Loads the memberships of the current caller once; subsequent calls are a no-op. Anonymous
    /// callers and admins resolve to an empty map without touching the database.
    /// </summary>
    Task LoadAsync(CancellationToken ct = default);
}
