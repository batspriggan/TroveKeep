using Microsoft.AspNetCore.Mvc;

namespace TroveKeep.Api.Filters;

/// <summary>
/// Works out which collection an HTTP write targets, so <see cref="CollectionWriteGuardFilter"/>
/// can authorize it without a per-action attribute on ~75 endpoints.
///
/// <para>
/// A <c>null</c> result means "this request is not bound to a single collection": either the action
/// is excluded (see the tables below), or the row named in the route is not visible to the caller —
/// in which case the repository already narrows the write and the action answers 404, so allowing
/// it through leaks nothing and keeps the "unknown id" behaviour identical for every access level.
/// </para>
/// </summary>
public interface ICollectionWriteTargetResolver
{
    /// <summary>Collection the request would modify, or <c>null</c> when it is collection-agnostic.</summary>
    Task<Guid?> ResolveAsync(ActionContext context);
}
