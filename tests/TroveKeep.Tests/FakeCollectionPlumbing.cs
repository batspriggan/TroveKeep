using TroveKeep.Core.Interfaces.Services;
using TroveKeep.Core.Models;

namespace TroveKeep.Tests;

/// <summary>Minimal <see cref="ICurrentUser"/> for the authorization tests.</summary>
internal sealed class FakeCurrentUser : ICurrentUser
{
    public bool IsAuthenticated { get; init; } = true;
    public Guid? UserId { get; init; } = Guid.NewGuid();
    public string? Email { get; init; } = "user@example.test";
    public UserRole Role { get; init; } = UserRole.User;
    public bool IsAdmin => Role == UserRole.Admin;
    public IReadOnlyCollection<Guid> CollectionIds { get; init; } = Array.Empty<Guid>();
    public Guid? ActiveCollectionId { get; init; }
    public Guid RequireUserId() => UserId ?? throw new UnauthorizedAccessException();
}

/// <summary>Membership cache with a hand-set level map (no database involved).</summary>
internal sealed class FakeMembershipCache : ICollectionMembershipCache
{
    public bool IsLoaded { get; init; } = true;
    public IReadOnlyDictionary<Guid, CollectionAccess> AccessByCollection { get; init; } =
        new Dictionary<Guid, CollectionAccess>();
    public Task LoadAsync(CancellationToken ct = default) => Task.CompletedTask;

    public static FakeMembershipCache With(params (Guid Id, CollectionAccess Level)[] levels)
    {
        var map = new Dictionary<Guid, CollectionAccess>();
        foreach (var (id, level) in levels) map[id] = level;
        return new FakeMembershipCache { AccessByCollection = map };
    }
}
