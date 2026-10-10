namespace TroveKeep.Core.Interfaces.Services;

/// <summary>
/// Request-scoped answer to "which collections may the caller read and write?".
///
/// <para>
/// It is a thin, testable layer over <see cref="ICurrentUser"/>: decoupling the repositories from
/// the raw claims makes the isolation rules explicit and gives every repository a single, shared
/// notion of the visible set. Admins bypass the filter entirely; everybody else is restricted to
/// the collections they are a member of.
/// </para>
/// </summary>
public interface ICollectionScope
{
    /// <summary>True for an admin: all collections are visible and writes skip the filter.</summary>
    bool CanAccessAll { get; }

    /// <summary>
    /// Collections the caller is a member of. Empty means "nothing visible" for a non-admin — the
    /// repositories translate that into a filter matching no document.
    /// </summary>
    IReadOnlyCollection<Guid> VisibleCollectionIds { get; }

    /// <summary>
    /// Collection a new row is assigned to when the caller does not name one: the explicitly
    /// selected collection (<c>X-TroveKeep-Collection</c>) if visible, otherwise the caller's only
    /// collection if there is exactly one, otherwise <c>null</c>.
    /// </summary>
    Guid? DefaultCollectionId { get; }

    /// <summary>True when <paramref name="collectionId"/> is visible to the caller.</summary>
    bool CanAccess(Guid collectionId);
}
