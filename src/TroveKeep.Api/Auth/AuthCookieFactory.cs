using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using TroveKeep.Core.Interfaces.Repositories;
using TroveKeep.Core.Models;
using TroveKeep.Services;

namespace TroveKeep.Api.Auth;

/// <summary>
/// Builds and issues the authentication cookie. Kept in one place so the local login, the OIDC
/// callback and the "refresh my claims" endpoint all emit an identical principal — in particular
/// the collection claims that <see cref="CurrentUser"/> reads without a database round-trip.
/// </summary>
public class AuthCookieFactory
{
    private readonly ICollectionMemberRepository _members;

    public AuthCookieFactory(ICollectionMemberRepository members)
    {
        _members = members;
    }

    public async Task<ClaimsPrincipal> BuildPrincipalAsync(User user)
    {
        var collectionIds = await _members.GetCollectionIdsForUserAsync(user.Id);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Name, user.DisplayName),
            new(ClaimTypes.Role, user.Role.ToString()),
        };
        claims.AddRange(collectionIds.Select(id => new Claim(CurrentUser.CollectionClaimType, id.ToString())));

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        return new ClaimsPrincipal(identity);
    }

    /// <summary>Signs the user in, replacing any previous session cookie.</summary>
    public async Task SignInAsync(HttpContext http, User user, bool persistent = true)
    {
        var principal = await BuildPrincipalAsync(user);
        await http.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties { IsPersistent = persistent });
    }

    /// <summary>Re-issues the cookie from the current database state (role/membership changes).</summary>
    public Task RefreshAsync(HttpContext http, User user) => SignInAsync(http, user);
}
