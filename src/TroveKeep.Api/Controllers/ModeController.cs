using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TroveKeep.Api.Auth;
using TroveKeep.Api.DTOs.Requests;
using TroveKeep.Api.DTOs.Responses;
using TroveKeep.Core.Interfaces.Services;
using TroveKeep.Core.Models;

namespace TroveKeep.Api.Controllers;

/// <summary>
/// Post-setup switch between single-user and multi-user mode, exposed in the user-management
/// screen. Kept in its own controller instead of <c>SetupController</c>, which is anonymous and
/// strictly first-run, and of <c>UsersController</c>, which is user CRUD: this endpoint mutates
/// process-wide application state, not an account.
///
/// <para>
/// Admin-only. In single-user mode every caller already runs as the technical administrator
/// (<see cref="AppModeMiddleware"/>), so anyone able to reach the instance can move it back to
/// multi-user — unavoidable in a mode whose premise is "no credentials, everyone is admin".
/// </para>
/// </summary>
[Authorize(Roles = nameof(UserRole.Admin))]
[ApiController]
[Route("api/admin/mode")]
public class ModeController : ControllerBase
{
    private readonly IAppStateService _appState;
    private readonly TechnicalUserProvider _technicalUsers;
    private readonly ILogger<ModeController> _logger;

    public ModeController(
        IAppStateService appState,
        TechnicalUserProvider technicalUsers,
        ILogger<ModeController> logger)
    {
        _appState = appState;
        _technicalUsers = technicalUsers;
        _logger = logger;
    }

    /// <summary>
    /// Switches the instance between single-user and multi-user mode. The instance is by
    /// definition already initialized (the toggle only exists after the first run), so
    /// <c>initialized</c> is written as <c>true</c>.
    /// </summary>
    [HttpPut]
    [ProducesResponseType(typeof(AppModeResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> SetMode([FromBody] SetModeRequest request)
    {
        // SetAsync refreshes the singleton cache, so the middleware picks up the new mode on the
        // very next request without a process restart.
        await _appState.SetAsync(initialized: true, multiUserEnabled: request.MultiUserEnabled);

        // The technical principal may no longer match the database (the administrator was
        // promoted, or roles/memberships changed while multi-user was active).
        _technicalUsers.Invalidate();

        _logger.LogInformation(
            "Application mode switched to {Mode}.",
            request.MultiUserEnabled ? "multi-user" : "single-user");

        return Ok(new AppModeResponse(true, request.MultiUserEnabled));
    }
}
