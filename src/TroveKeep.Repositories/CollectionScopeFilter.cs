using System.Linq.Expressions;
using MongoDB.Driver;
using TroveKeep.Core.Interfaces.Services;

namespace TroveKeep.Repositories;

/// <summary>
/// Scope used when a repository is built outside a request (the migrations' hand-wired object
/// graph): no isolation, exactly the pre-isolation behaviour.
/// </summary>
internal sealed class UnscopedCollectionScope : ICollectionScope
{
    public static readonly UnscopedCollectionScope Instance = new();

    public bool CanAccessAll => true;

    public bool HasExplicitCollectionSelection => false;

    public IReadOnlyCollection<Guid> VisibleCollectionIds => Array.Empty<Guid>();

    public IReadOnlyCollection<Guid> AccessibleCollectionIds => Array.Empty<Guid>();

    public Guid? DefaultCollectionId => null;

    public bool CanAccess(Guid collectionId) => true;
}

/// <summary>
/// Shared helpers that turn an <see cref="ICollectionScope"/> into MongoDB filters and new-row
/// defaults. Centralised here so all isolated repositories apply exactly the same rules.
/// </summary>
internal static class CollectionScopeFilter
{
    /// <summary>
    /// Restricts <paramref name="filter"/> (or "everything", when it is <c>null</c>) to the
    /// collections visible to the caller. An explicit <c>X-TroveKeep-Collection</c> selection is a
    /// display filter and narrows listings for admins too; without one an admin gets the filter
    /// untouched, while a non-admin with no membership gets a filter that matches no document, so a
    /// query returns an empty list instead of leaking other tenants' rows.
    /// </summary>
    public static FilterDefinition<TDocument> ScopedTo<TDocument>(
        this ICollectionScope scope,
        Expression<Func<TDocument, Guid>> collectionId,
        FilterDefinition<TDocument>? filter = null)
    {
        // An explicit selection overrides the admin bypass: it is a display filter, not an
        // authorization rule. Only when there is no selection does an admin skip the filter.
        if (!scope.HasExplicitCollectionSelection && scope.CanAccessAll)
            return filter ?? Builders<TDocument>.Filter.Empty;

        var visible = scope.VisibleCollectionIds;
        if (visible.Count == 0)
        {
            // "$in: []" matches no document: a non-admin without memberships sees an empty list
            // instead of leaking other tenants' rows.
            return Builders<TDocument>.Filter.In(collectionId, Array.Empty<Guid>());
        }

        var scoped = Builders<TDocument>.Filter.In(collectionId, visible);
        return filter is null ? scoped : Builders<TDocument>.Filter.And(filter, scoped);
    }

    /// <summary>
    /// Restricts <paramref name="filter"/> to the collections the caller is a member of, ignoring
    /// the active-collection selection. Use this for per-resource lookups (fetch/update/delete by
    /// id): the active collection narrows what a listing shows, but it must not turn a legitimate
    /// id — one whose collection the caller already belongs to — into a 404.
    /// </summary>
    public static FilterDefinition<TDocument> ScopedToAccessible<TDocument>(
        this ICollectionScope scope,
        Expression<Func<TDocument, Guid>> collectionId,
        FilterDefinition<TDocument>? filter = null)
    {
        if (scope.CanAccessAll)
            return filter ?? Builders<TDocument>.Filter.Empty;

        var accessible = scope.AccessibleCollectionIds;
        if (accessible.Count == 0)
        {
            // No membership: a per-resource lookup must miss, not fall through to "everything".
            return Builders<TDocument>.Filter.In(collectionId, Array.Empty<Guid>());
        }

        var scoped = Builders<TDocument>.Filter.In(collectionId, accessible);
        return filter is null ? scoped : Builders<TDocument>.Filter.And(filter, scoped);
    }

    /// <summary>
    /// Resolves the collection a new row belongs to. A caller-supplied id must be accessible; an
    /// empty id falls back to <see cref="ICollectionScope.DefaultCollectionId"/>. Only an admin may
    /// leave the row unassigned; for everybody else a missing default is a client error.
    /// </summary>
    public static Guid ResolveCreateCollectionId(this ICollectionScope scope, Guid requested)
    {
        if (requested != Guid.Empty)
        {
            if (!scope.CanAccess(requested))
                throw new InvalidOperationException("The selected collection is not accessible.");
            return requested;
        }

        if (scope.DefaultCollectionId is { } fallback)
            return fallback;

        if (scope.CanAccessAll)
            return Guid.Empty;

        throw new InvalidOperationException("No collection selected for the new item.");
    }
}
