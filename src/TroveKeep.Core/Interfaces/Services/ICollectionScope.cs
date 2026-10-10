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
///
/// <para>
/// When the caller has selected an active collection (the <c>X-TroveKeep-Collection</c> header),
/// the visible set is narrowed to that single collection. The header can only restrict access,
/// never broaden it: it is honoured only when the caller is already a member of the named
/// collection, otherwise the full membership set is used.
/// </para>
/// </summary>
public interface ICollectionScope
{
    /// <summary>True for an admin: all collections are visible and writes skip the filter.</summary>
    bool CanAccessAll { get; }

    /// <summary>
    /// Collections a listing may span, after applying the active-collection filter. Empty means
    /// "nothing visible" for a non-admin — the repositories translate that into a filter matching
    /// no document.
    ///
    /// <para>
    /// For a non-admin this is the single collection selected via <c>X-TroveKeep-Collection</c> when
    /// the selection is valid (the caller is a member of it); otherwise it is every collection the
    /// caller is a member of. An admin is unaffected: the repositories skip the filter entirely
    /// because <see cref="CanAccessAll"/> is true.
    /// </para>
    ///
    /// <para>
    /// This is a <em>display</em> filter: it narrows listings. Access to an individual resource is
    /// governed by <see cref="AccessibleCollectionIds"/>, so requesting a legitimate id by id still
    /// succeeds even when its collection is not the active one.
    /// </para>
    /// </summary>
    IReadOnlyCollection<Guid> VisibleCollectionIds { get; }

    /// <summary>
    /// Every collection the caller is a member of, regardless of the active-collection selection.
    /// This is the authority for per-resource access (fetch/update/delete by id) and for the
    /// membership checks used when assigning a row to a collection: the header may narrow a
    /// listing, but it must never turn a legitimate id into a 404 or reject a collection the
    /// caller already belongs to.
    /// </summary>
    IReadOnlyCollection<Guid> AccessibleCollectionIds { get; }

    /// <summary>
    /// Collection a new row is assigned to when the caller does not name one: the explicitly
    /// selected collection (<c>X-TroveKeep-Collection</c>) if accessible, otherwise the caller's
    /// only collection if there is exactly one, otherwise <c>null</c>.
    /// </summary>
    Guid? DefaultCollectionId { get; }

    /// <summary>
    /// True when the caller may access <paramref name="collectionId"/> as a resource. Unlike
    /// <see cref="VisibleCollectionIds"/> this is not narrowed by the active-collection header.
    /// </summary>
    bool CanAccess(Guid collectionId);
}
