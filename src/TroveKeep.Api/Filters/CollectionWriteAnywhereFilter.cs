using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using TroveKeep.Core.Interfaces.Services;

namespace TroveKeep.Api.Filters;

/// <summary>
/// Action filter for controllers whose rows belong to <em>no</em> collection — today only
/// <c>StickersController</c> (the library is instance-wide, so there is no target collection whose
/// access level could authorize the write).
///
/// <para>
/// <see cref="CollectionWriteGuardFilter"/> answers "may you write <em>this collection</em>?"; for a
/// global row that question has no answer, so the rule becomes
/// <see cref="ICollectionScope.CanWriteAnywhere"/>: an admin, or a member with
/// <c>Editor</c>/<c>Owner</c> in at least one collection. A <c>Viewer</c> (or a user with no
/// collection at all) still reads and prints the library, and gets <b>403</b> on every write.
/// </para>
///
/// <para>
/// Applied explicitly with <c>[ServiceFilter(typeof(CollectionWriteAnywhereFilter))]</c> rather than
/// globally: the rule is specific to collection-agnostic data, and every other controller keeps
/// the per-collection guard. Safe methods are skipped so reads stay open to every authenticated
/// role, and the response body matches the global guard's <c>{ error }</c> shape so the SPA renders
/// the same message.
/// </para>
/// </summary>
public sealed class CollectionWriteAnywhereFilter : IAsyncActionFilter
{
    private static readonly HashSet<string> SafeMethods =
        new(StringComparer.OrdinalIgnoreCase) { "GET", "HEAD", "OPTIONS", "TRACE" };

    private readonly ICollectionScope _scope;
    private readonly ILogger<CollectionWriteAnywhereFilter> _logger;

    public CollectionWriteAnywhereFilter(ICollectionScope scope, ILogger<CollectionWriteAnywhereFilter> logger)
    {
        _scope = scope;
        _logger = logger;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (SafeMethods.Contains(context.HttpContext.Request.Method) || _scope.CanWriteAnywhere)
        {
            await next();
            return;
        }

        var action = context.ActionDescriptor.DisplayName ?? context.HttpContext.Request.Path;
        _logger.LogInformation(
            "Blocked write on global data for {User}: {Action} (no Editor/Owner membership)",
            context.HttpContext.User.Identity?.Name ?? "anonymous", action);

        context.Result = new ObjectResult(new
        {
            error = "This library is shared by the whole instance: you need Editor access in at least one collection to change it.",
        })
        {
            StatusCode = StatusCodes.Status403Forbidden,
        };
    }
}
