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

    public IReadOnlyCollection<Guid> VisibleCollectionIds => _currentUser.CollectionIds;

    public Guid? DefaultCollectionId
    {
        get
        {
            // An explicit selection wins, but only if the caller may actually see it. This mirrors
            // ICurrentUser.ActiveCollectionId, which already rejects a header naming a collection the
            // user is not a member of.
            if (_currentUser.ActiveCollectionId is { } active && CanAccess(active))
                return active;

            // Without a selection, default only when the choice is unambiguous.
            var visible = VisibleCollectionIds;
            return visible.Count == 1 ? visible.First() : null;
        }
    }

    public bool CanAccess(Guid collectionId) =>
        CanAccessAll || VisibleCollectionIds.Contains(collectionId);
}
