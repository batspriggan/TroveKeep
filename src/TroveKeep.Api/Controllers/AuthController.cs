using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TroveKeep.Api.Auth;
using TroveKeep.Api.DTOs.Requests;
using TroveKeep.Api.DTOs.Responses;
using TroveKeep.Core.Interfaces.Repositories;
using TroveKeep.Core.Interfaces.Services;
using TroveKeep.Core.Models;

namespace TroveKeep.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;
    private readonly IUserRepository _users;
    private readonly ICollectionMemberRepository _members;
    private readonly ICollectionRepository _collections;
    private readonly AuthCookieFactory _cookies;
    private readonly OidcSettings _oidc;

    public AuthController(
        IAuthService auth,
        IUserRepository users,
        ICollectionMemberRepository members,
        ICollectionRepository collections,
        AuthCookieFactory cookies,
        OidcSettings oidc)
    {
        _auth = auth;
        _users = users;
        _members = members;
        _collections = collections;
        _cookies = cookies;
        _oidc = oidc;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(MeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var result = await _auth.ValidateCredentialsAsync(request.Email, request.Password);
        if (!result.Success || result.User is null)
            return Unauthorized(new { error = result.Error });

        await _cookies.SignInAsync(HttpContext, result.User);
        return Ok(await BuildMeAsync(result.User));
    }

    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return NoContent();
    }

    /// <summary>
    /// Which login methods the SPA should offer. Anonymous so the login page can render before
    /// any session exists. Local auth is always on.
    /// </summary>
    [HttpGet("providers")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ProvidersResponse), StatusCodes.Status200OK)]
    public IActionResult Providers() => Ok(new ProvidersResponse(Local: true, Oidc: _oidc.IsEnabled));

    /// <summary>
    /// Kicks off the OIDC authorization-code flow. Returns 404 (not 500) when OIDC is disabled,
    /// so a client that probes the endpoint gets an unambiguous "not available" answer.
    /// </summary>
    [HttpGet("oidc/challenge")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult OidcChallenge()
    {
        if (!_oidc.IsEnabled)
            return NotFound(new { error = "OIDC authentication is not enabled on this instance." });

        return Challenge(new AuthenticationProperties { RedirectUri = "/" }, OidcSettings.Scheme);
    }

    [HttpGet("me")]
    [ProducesResponseType(typeof(MeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Me()
    {
        var user = await CurrentUserAsync();
        if (user is null) return Unauthorized();
        return Ok(await BuildMeAsync(user));
    }

    [HttpPost("change-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 8)
            return BadRequest(new { error = "New password must be at least 8 characters long." });

        var changed = await _auth.ChangePasswordAsync(userId.Value, request.CurrentPassword, request.NewPassword);
        if (!changed) return BadRequest(new { error = "Current password is incorrect." });

        // Re-issue the cookie so the session stays valid and claims are fresh.
        var user = await _users.GetByIdAsync(userId.Value);
        if (user is not null) await _cookies.RefreshAsync(HttpContext, user);

        return NoContent();
    }

    private Guid? CurrentUserId()
    {
        var raw = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(raw, out var id) ? id : null;
    }

    private Task<User?> CurrentUserAsync()
    {
        var id = CurrentUserId();
        return id is null ? Task.FromResult<User?>(null) : _users.GetByIdAsync(id.Value);
    }

    private async Task<MeResponse> BuildMeAsync(User user)
    {
        var memberships = (await _members.GetByUserIdAsync(user.Id)).ToList();
        var byCollection = memberships.ToDictionary(m => m.CollectionId, m => m.Access);

        // An admin may act on every collection, so they see all of them; a regular user only theirs.
        var all = await _collections.GetAllAsync();
        var visible = user.Role == UserRole.Admin
            ? all
            : all.Where(c => byCollection.ContainsKey(c.Id));

        var collections = visible
            .Select(c => new CollectionSummaryResponse(
                c.Id,
                c.Name,
                // An admin may write and manage members everywhere (CollectionScope.AccessFor returns
                // Owner for any collection), so the reported level is "Admin" even when the caller
                // happens to be a Viewer member of that collection: the UI derives the controls it
                // shows from this value.
                user.Role == UserRole.Admin
                    ? "Admin"
                    : byCollection[c.Id].ToString()))
            .OrderBy(c => c.Name)
            .ToList();

        return new MeResponse(user.Id, user.Email, user.DisplayName, user.Role.ToString(), collections);
    }
}
