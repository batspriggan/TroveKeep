using TroveKeep.Core.Interfaces.Services;
using TroveKeep.Core.Models;

namespace TroveKeep.Services;

/// <summary>
/// Collection isolation derived from <see cref="ICurrentUser"/> (visible set, from the cookie
/// claims) and <see cref="ICollectionMembershipCache"/> (access level, from <c>collection_members</c>).
/// Registered scoped, so it is computed once per request from the auth cookie claims, the
/// active-collection header and the membership lookup primed by <c>CollectionMembershipMiddleware</c>.
/// </summary>
public class CollectionScope : ICollectionScope
{
    private readonly ICurrentUser _currentUser;
    private readonly ICollectionMembershipCache _memberships;

    public CollectionScope(ICurrentUser currentUser, ICollectionMembershipCache memberships)
    {
        _currentUser = currentUser;
        _memberships = memberships;
    }

    public bool CanAccessAll => _currentUser.IsAdmin;

    /// <summary>
    /// True when this request carries a usable explicit selection: an active collection that the
    /// caller may access. <see cref="CanAccess"/> is always true for an admin, so for an admin a
    /// syntactically valid header counts even if the collection no longer exists. We deliberately
    /// accept that: the selection is a display filter, and a stale id simply yields an empty list
    /// until the user clears it — exactly the behaviour a non-admin already gets for a stale
    /// membership. Verifying existence here would add a DB round-trip to every request and would
    /// still not cover a collection deleted between the check and the query.
    /// </summary>
    public bool HasExplicitCollectionSelection =>
        _currentUser.ActiveCollectionId is { } active && CanAccess(active);

    public IReadOnlyCollection<Guid> VisibleCollectionIds
    {
        get
        {
            // An explicit collection selection (X-TroveKeep-Collection) narrows the visible set to
            // that single collection, so reads stop spanning every membership of the caller. It is
            // a display filter, so unlike CanAccessAll it applies to an admin too.
            //
            // This can only ever shrink access, never widen it: ActiveCollectionId already returns
            // the header solely when the caller is an admin or a member of that collection, and the
            // membership is re-checked here explicitly so a claim-derived active id (which is not
            // validated above) cannot leak a collection the user does not belong to.
            if (HasExplicitCollectionSelection && _currentUser.ActiveCollectionId is { } active)
                return new[] { active };

            // No usable selection: keep the pre-existing behaviour (every membership of the user).
            return _currentUser.CollectionIds;
        }
    }

    public Guid? DefaultCollectionId
    {
        get
        {
            // An explicit selection wins, but only if the caller may actually access it.
            // ActiveCollectionId already rejects a header naming a collection the user is not a
            // member of, and CanAccess re-checks membership, so this stays in sync with the
            // narrowing applied to listings (a single visible collection resolves to itself).
            if (_currentUser.ActiveCollectionId is { } active && CanAccess(active))
                return active;

            // Without a selection, default only when the choice is unambiguous.
            var visible = VisibleCollectionIds;
            return visible.Count == 1 ? visible.First() : null;
        }
    }

    /// <summary>
    /// Full membership, not narrowed by the active-collection header. This keeps per-resource
    /// access and write-target validation exactly as they were before the header started
    /// narrowing listings.
    /// </summary>
    public IReadOnlyCollection<Guid> AccessibleCollectionIds => _currentUser.CollectionIds;

    public bool CanAccess(Guid collectionId) =>
        CanAccessAll || AccessibleCollectionIds.Contains(collectionId);

    /// <summary>
    /// Highest level the caller holds. An admin is modelled as <see cref="CollectionAccess.Owner"/>
    /// of every collection (they may write and manage members anywhere), a member gets the level
    /// stored on their membership, and a non-member gets <c>null</c>.
    /// </summary>
    public CollectionAccess? AccessFor(Guid collectionId)
    {
        if (CanAccessAll) return CollectionAccess.Owner;

        // Not loaded yet (a code path that runs outside the request pipeline, e.g. the migrations'
        // hand-wired object graph): no level is known, so the caller is treated as a non-member and
        // nothing is writable. The middleware primes the cache on every real request.
        if (!_memberships.IsLoaded) return null;

        return _memberships.AccessByCollection.TryGetValue(collectionId, out var access)
            ? access
            : null;
    }

    public bool CanWrite(Guid collectionId) =>
        CanAccessAll || (AccessFor(collectionId) is CollectionAccess.Editor or CollectionAccess.Owner);

    public bool CanManageMembers(Guid collectionId) =>
        CanAccessAll || (AccessFor(collectionId) == CollectionAccess.Owner);
}
