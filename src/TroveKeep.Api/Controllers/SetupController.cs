using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using TroveKeep.Api.Auth;
using TroveKeep.Api.DTOs.Requests;
using TroveKeep.Api.DTOs.Responses;
using TroveKeep.Core.Interfaces.Repositories;
using TroveKeep.Core.Interfaces.Services;
using TroveKeep.Core.Models;

namespace TroveKeep.Api.Controllers;

/// <summary>
/// First-run configuration. Both endpoints are anonymous because they must be reachable before any
/// account is usable; the mutating one is gated by the transient setup token printed at startup.
/// </summary>
[ApiController]
[Route("api/setup")]
public class SetupController : ControllerBase
{
    private readonly IAppStateService _appState;
    private readonly IUserRepository _users;
    private readonly TechnicalUserProvider _technicalUsers;
    private readonly ILogger<SetupController> _logger;
    private readonly PasswordHasher<User> _hasher = new();

    public SetupController(
        IAppStateService appState,
        IUserRepository users,
        TechnicalUserProvider technicalUsers,
        ILogger<SetupController> logger)
    {
        _appState = appState;
        _users = users;
        _technicalUsers = technicalUsers;
        _logger = logger;
    }

    [HttpGet("status")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(SetupStatusResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Status()
    {
        var state = await _appState.GetAsync();
        return Ok(new SetupStatusResponse(state.Initialized, state.MultiUserEnabled, _appState.RequiresSetupToken));
    }

    /// <summary>
    /// One-shot first-run setup. Returns 409 once <c>app_config.initialized</c> is true — this also
    /// covers an instance that is already multi-user, so a configured instance can never be
    /// reconfigured through this endpoint (to switch modes the operator resets the database state).
    /// </summary>
    [HttpPost]
    [AllowAnonymous]
    [ProducesResponseType(typeof(SetupStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Complete([FromBody] SetupRequest request)
    {
        var state = await _appState.GetAsync();
        if (state.Initialized)
        {
            return Conflict(new
            {
                error = "This instance is already set up. Setup cannot be run again; " +
                        "reset the application state to change the mode.",
            });
        }

        if (!TokenMatches(_appState.SetupToken, request.Token))
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "Invalid setup token." });

        if (request.MultiUser)
        {
            var email = TroveKeep.Core.Models.User.NormalizeEmail(request.Email);
            if (string.IsNullOrWhiteSpace(email))
                return BadRequest(new { error = "Email is required when enabling multi-user." });

            if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
                return BadRequest(new { error = "Password must be at least 8 characters long when enabling multi-user." });

            var systemUser = await _users.GetSystemUserAsync();
            if (systemUser is null)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    error = "No technical account to promote. The database is not in the expected state.",
                });
            }

            // Overwrite the technical account in place so the existing collection membership and
            // any data stamped with its ownership stay valid.
            systemUser.Email = email;
            systemUser.DisplayName = email;
            systemUser.PasswordHash = _hasher.HashPassword(systemUser, request.Password);
            systemUser.Active = true;
            systemUser.IsSystem = false;
            systemUser.Role = UserRole.Admin;

            if (await _users.UpdateAsync(systemUser) is null)
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { error = "The technical account disappeared during setup." });
        }

        await _appState.SetAsync(initialized: true, multiUserEnabled: request.MultiUser);

        // The cached technical principal no longer matches the database (it was rewritten when
        // multi-user was enabled), and single-user mode may have just ended.
        _technicalUsers.Invalidate();

        _logger.LogInformation(
            "First-run setup completed: multi-user {State}.",
            request.MultiUser ? "enabled" : "disabled (single-user mode)");

        return Ok(new SetupStatusResponse(true, request.MultiUser, false));
    }

    /// <summary>Constant-time token comparison to avoid leaking the token through timing.</summary>
    private static bool TokenMatches(string? expected, string? provided)
    {
        if (string.IsNullOrEmpty(expected) || string.IsNullOrEmpty(provided)) return false;

        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var providedBytes = Encoding.UTF8.GetBytes(provided);
        return CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes);
    }
}
