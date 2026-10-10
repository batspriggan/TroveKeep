using TroveKeep.Core.Models;

namespace TroveKeep.Core.Interfaces.Services;

/// <summary>
/// Request-scoped view of the authenticated caller, resolved from the auth cookie claims.
/// This is the single source of truth for "who is asking" and, from step 4/5, for the
/// collection scope applied to every repository query.
/// </summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    Guid? UserId { get; }

    string? Email { get; }

    UserRole Role { get; }

    /// <summary>Admin bypasses the per-collection filter.</summary>
    bool IsAdmin { get; }

    /// <summary>Collection ids the caller is a member of. Empty for a Guest with no membership.</summary>
    IReadOnlyCollection<Guid> CollectionIds { get; }

    /// <summary>Collection explicitly selected for this request (header), if any.</summary>
    Guid? ActiveCollectionId { get; }

    /// <summary>Throws <see cref="UnauthorizedAccessException"/> when there is no authenticated user.</summary>
    Guid RequireUserId();
}
