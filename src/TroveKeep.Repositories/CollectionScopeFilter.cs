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

    public IReadOnlyCollection<Guid> VisibleCollectionIds => Array.Empty<Guid>();

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
    /// collections visible to the caller. An admin gets the filter untouched; a non-admin with no
    /// membership gets a filter that matches no document, so a query returns an empty list instead
    /// of leaking other tenants' rows.
    /// </summary>
    public static FilterDefinition<TDocument> ScopedTo<TDocument>(
        this ICollectionScope scope,
        Expression<Func<TDocument, Guid>> collectionId,
        FilterDefinition<TDocument>? filter = null)
    {
        if (scope.CanAccessAll)
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
