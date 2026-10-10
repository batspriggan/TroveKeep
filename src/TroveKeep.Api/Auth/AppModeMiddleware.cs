using TroveKeep.Core.Interfaces.Services;

namespace TroveKeep.Api.Auth;

/// <summary>
/// Enforces the two application modes derived from <see cref="IAppStateService"/>:
///
/// <list type="bullet">
///   <item>
///     <b>Not initialized</b> — every <c>/api</c> endpoint except <c>/api/setup/*</c> answers 503,
///     with an <c>{ "error": ... }</c> body. Static files and the SPA fallback stay reachable so the
///     setup screen can be served.
///   </item>
///   <item>
///     <b>Single-user</b> (<c>initialized && !multiUserEnabled</c>) — login is disabled and every
///     request is served as the technical admin account, so <c>ICurrentUser</c> and
///     <c>ICollectionScope</c> keep working unchanged and no authentication is required.
///   </item>
/// </list>
///
/// Registered after <c>UseAuthentication</c> and before <c>UseAuthorization</c>, so the technical
/// principal is in place when the fallback policy and <c>ICurrentUser</c> run.
/// </summary>
public class AppModeMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IAppStateService _appState;
    private readonly TechnicalUserProvider _technicalUsers;

    public AppModeMiddleware(
        RequestDelegate next,
        IAppStateService appState,
        TechnicalUserProvider technicalUsers)
    {
        _next = next;
        _appState = appState;
        _technicalUsers = technicalUsers;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path;
        var isApi = path.StartsWithSegments("/api");
        var isSetup = path.StartsWithSegments("/api/setup");

        var state = await _appState.GetAsync(context.RequestAborted);

        if (!state.Initialized && isApi && !isSetup)
        {
            await WriteErrorAsync(context, StatusCodes.Status503ServiceUnavailable,
                "TroveKeep is not initialized yet. Complete the first-run setup via /api/setup.");
            return;
        }

        if (state.Initialized && !state.MultiUserEnabled)
        {
            // Single-user mode: there is nobody to log in as. Refuse the login surface explicitly
            // instead of letting it fail with "invalid credentials".
            if (path.StartsWithSegments("/api/auth/login") || path.StartsWithSegments("/api/auth/oidc"))
            {
                await WriteErrorAsync(context, StatusCodes.Status404NotFound,
                    "Login is disabled in single-user mode.");
                return;
            }

            context.User = await _technicalUsers.GetAsync(context.RequestAborted);
        }

        await _next(context);
    }

    private static async Task WriteErrorAsync(HttpContext context, int statusCode, string error)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new { error });
    }
}
