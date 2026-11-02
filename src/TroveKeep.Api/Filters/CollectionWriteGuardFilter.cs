using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using TroveKeep.Core.Interfaces.Services;
using TroveKeep.Core.Models;

namespace TroveKeep.Api.Filters;

/// <summary>
/// Global MVC filter that enforces the collection access level on every write endpoint: a request
/// whose method is not <c>GET</c>/<c>HEAD</c>/<c>OPTIONS</c>/<c>TRACE</c> is refused with
/// <b>403</b> when the caller cannot write in the collection it targets.
///
/// <para>
/// One filter instead of ~75 attributes on the actions, because the rule is identical everywhere
/// and applying it by hand is exactly the kind of change that silently misses the next endpoint.
/// <see cref="ICollectionWriteTargetResolver"/> supplies the collection (row id in the route,
/// create default, or a declared exclusion).
/// </para>
///
/// <para>
/// The rules it implements: an admin may write everywhere (the role bypass covers authorization
/// only — it does not widen the <c>X-TroveKeep-Collection</c> display filter, which keeps applying
/// to admins as before); an <see cref="CollectionAccess.Editor"/> or
/// <see cref="CollectionAccess.Owner"/> may write in their collections; a
/// <see cref="CollectionAccess.Viewer"/> never may; a non-member is left to the repositories'
/// existing scoped lookups, which answer 404 without revealing that the row exists.
/// </para>
///
/// <para>
/// This is the boundary, not the UI: the SPA hides the buttons it would get a 403 for, but nothing
/// depends on that.
/// </para>
/// </summary>
public sealed class CollectionWriteGuardFilter : IAsyncActionFilter
{
    private static readonly HashSet<string> SafeMethods =
        new(StringComparer.OrdinalIgnoreCase) { "GET", "HEAD", "OPTIONS", "TRACE" };

    private readonly ICollectionWriteTargetResolver _resolver;
    private readonly ICollectionScope _scope;
    private readonly ILogger<CollectionWriteGuardFilter> _logger;

    public CollectionWriteGuardFilter(
        ICollectionWriteTargetResolver resolver,
        ICollectionScope scope,
        ILogger<CollectionWriteGuardFilter> logger)
    {
        _resolver = resolver;
        _scope = scope;
        _logger = logger;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (SafeMethods.Contains(context.HttpContext.Request.Method))
        {
            await next();
            return;
        }

        // An admin owns every collection: nothing to resolve, nothing to refuse.
        if (_scope.CanAccessAll)
        {
            await next();
            return;
        }

        var collectionId = await _resolver.ResolveAsync(context);

        // Not bound to a collection (declared exclusion, or a row the caller cannot see: the action
        // then answers 404 exactly as an unknown id does).
        if (collectionId is not { } target)
        {
            await next();
            return;
        }

        if (_scope.CanWrite(target))
        {
            await next();
            return;
        }

        var action = context.ActionDescriptor.DisplayName ?? context.HttpContext.Request.Path;
        _logger.LogInformation(
            "Blocked write on collection {CollectionId} for {User}: {Action} (access {Access})",
            target, context.HttpContext.User.Identity?.Name ?? "anonymous", action,
            _scope.AccessFor(target)?.ToString() ?? "none");

        context.Result = new ObjectResult(new
        {
            error = _scope.CanAccess(target)
                ? "Your access level in this collection is read-only: ask an Owner for Editor access to make changes."
                : "You do not have permission to modify this collection.",
        })
        {
            StatusCode = StatusCodes.Status403Forbidden,
        };
    }
}
