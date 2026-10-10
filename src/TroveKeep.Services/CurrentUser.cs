using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using TroveKeep.Core.Interfaces.Services;
using TroveKeep.Core.Models;

namespace TroveKeep.Services;

/// <summary>
/// Reads the authenticated caller from the request principal injected by the cookie handler.
/// The collection ids are not loaded here (that would need a DB round-trip on every request):
/// they live in the auth cookie as individual claims, written at login and refreshed by
/// <c>AuthController</c>'s <c>me</c> endpoint. Admins bypass the scope entirely.
/// </summary>
public class CurrentUser : ICurrentUser
{
    /// <summary>Claim type carrying a collection id the user is a member of.</summary>
    public const string CollectionClaimType = "trovekeep:collection";

    /// <summary>Claim type carrying the collection explicitly selected for this request.</summary>
    public const string ActiveCollectionClaimType = "trovekeep:active_collection";

    private readonly ClaimsPrincipal? _principal;
    private readonly Guid? _activeCollectionHeader;

    public CurrentUser(IHttpContextAccessor accessor)
    {
        _principal = accessor.HttpContext?.User;

        var header = accessor.HttpContext?.Request.Headers["X-TroveKeep-Collection"].FirstOrDefault();
        _activeCollectionHeader = Guid.TryParse(header, out var parsed) ? parsed : null;
    }

    public bool IsAuthenticated => _principal?.Identity?.IsAuthenticated ?? false;

    public Guid? UserId
    {
        get
        {
            var raw = _principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(raw, out var id) ? id : null;
        }
    }

    public string? Email => _principal?.FindFirstValue(ClaimTypes.Email);

    public UserRole Role
    {
        get
        {
            var raw = _principal?.FindFirstValue(ClaimTypes.Role);
            return Enum.TryParse<UserRole>(raw, out var role) ? role : UserRole.Guest;
        }
    }

    public bool IsAdmin => Role == UserRole.Admin;

    public IReadOnlyCollection<Guid> CollectionIds
    {
        get
        {
            var claims = _principal?.FindAll(CollectionClaimType) ?? [];
            var ids = new List<Guid>();
            foreach (var claim in claims)
                if (Guid.TryParse(claim.Value, out var id)) ids.Add(id);
            return ids;
        }
    }

    public Guid? ActiveCollectionId
    {
        get
        {
            // An explicit request header wins, but only if the user is actually a member of that
            // collection (or is an admin): otherwise a client could name any collection id.
            if (_activeCollectionHeader is { } header)
            {
                if (IsAdmin || CollectionIds.Contains(header)) return header;
                return null;
            }

            var raw = _principal?.FindFirstValue(ActiveCollectionClaimType);
            return Guid.TryParse(raw, out var id) ? id : null;
        }
    }

    public Guid RequireUserId() =>
        UserId ?? throw new UnauthorizedAccessException("No authenticated user.");
}
