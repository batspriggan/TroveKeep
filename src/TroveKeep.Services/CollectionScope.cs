using TroveKeep.Core.Interfaces.Services;

namespace TroveKeep.Services;

/// <summary>
/// Collection isolation derived entirely from <see cref="ICurrentUser"/>. Registered scoped, so it
/// is computed once per request from the auth cookie claims and the active-collection header.
/// </summary>
public class CollectionScope : ICollectionScope
{
    private readonly ICurrentUser _currentUser;

    public CollectionScope(ICurrentUser currentUser)
    {
        _currentUser = currentUser;
    }

    public bool CanAccessAll => _currentUser.IsAdmin;

    public IReadOnlyCollection<Guid> VisibleCollectionIds
    {
        get
        {
            // An explicit collection selection (X-TroveKeep-Collection) narrows the visible set to
            // that single collection, so reads stop spanning every membership of the caller.
            //
            // This can only ever shrink access, never widen it: ActiveCollectionId already returns
            // the header solely when the caller is an admin or a member of that collection, and the
            // membership is re-checked here explicitly so a claim-derived active id (which is not
            // validated above) cannot leak a collection the user does not belong to.
            if (!CanAccessAll
                && _currentUser.ActiveCollectionId is { } active
                && _currentUser.CollectionIds.Contains(active))
            {
                return new[] { active };
            }

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
}
