using TroveKeep.Core.Interfaces.Services;

namespace TroveKeep.Api.Auth;

/// <summary>
/// Primes <see cref="ICollectionMembershipCache"/> before authorization runs, so
/// <see cref="ICollectionScope.AccessFor"/>, <see cref="ICollectionScope.CanWrite"/> and
/// <see cref="ICollectionScope.CanManageMembers"/> are synchronous and authoritative for the rest
/// of the request.
///
/// <para>
/// Placed after <see cref="AppModeMiddleware"/> — in single-user mode the technical admin principal
/// must already be in place, and an admin needs no lookup at all — and before
/// <c>UseAuthorization</c>. Only <c>/api</c> requests are considered: the SPA and static assets have
/// no use for the access level. Anonymous requests and admins resolve to an empty map without a
/// database round-trip (see <see cref="ICollectionMembershipCache.LoadAsync"/>).
/// </para>
/// </summary>
public class CollectionMembershipMiddleware
{
    private readonly RequestDelegate _next;

    public CollectionMembershipMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ICollectionMembershipCache memberships)
    {
        if (context.Request.Path.StartsWithSegments("/api"))
            await memberships.LoadAsync(context.RequestAborted);

        await _next(context);
    }
}
