using TroveKeep.Core.Interfaces.Repositories;
using TroveKeep.Core.Interfaces.Services;
using TroveKeep.Core.Models;

namespace TroveKeep.Services;

/// <summary>
/// Scoped implementation of <see cref="ICollectionMembershipCache"/>: one <c>collection_members</c>
/// query per request at most, no matter how many times the scope is asked about the caller's level.
/// </summary>
public sealed class CollectionMembershipCache : ICollectionMembershipCache
{
    private static readonly IReadOnlyDictionary<Guid, CollectionAccess> Empty =
        new Dictionary<Guid, CollectionAccess>();

    private readonly ICurrentUser _currentUser;
    private readonly ICollectionMemberRepository _members;

    private IReadOnlyDictionary<Guid, CollectionAccess> _accessByCollection = Empty;

    public CollectionMembershipCache(ICurrentUser currentUser, ICollectionMemberRepository members)
    {
        _currentUser = currentUser;
        _members = members;
    }

    public bool IsLoaded { get; private set; }

    public IReadOnlyDictionary<Guid, CollectionAccess> AccessByCollection => _accessByCollection;

    public async Task LoadAsync(CancellationToken ct = default)
    {
        if (IsLoaded) return;

        // Admins authorize everything through CanAccessAll and anonymous requests never reach a
        // guarded action: neither needs the lookup.
        if (!_currentUser.IsAuthenticated || _currentUser.IsAdmin || _currentUser.UserId is null)
        {
            IsLoaded = true;
            return;
        }

        var memberships = await _members.GetByUserIdAsync(_currentUser.UserId.Value);

        // One membership per (collection, user) is the invariant, but a duplicated document must
        // never widen access: merging with Max keeps the strongest stored level deterministic.
        _accessByCollection = memberships
            .GroupBy(m => m.CollectionId)
            .ToDictionary(g => g.Key, g => g.Max(m => m.Access));

        IsLoaded = true;
    }
}
