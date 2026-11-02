using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TroveKeep.Api.DTOs.Requests;
using TroveKeep.Api.DTOs.Responses;
using TroveKeep.Core.Interfaces.Repositories;
using TroveKeep.Core.Interfaces.Services;
using TroveKeep.Core.Models;

namespace TroveKeep.Api.Controllers;

[ApiController]
[Route("api/collections")]
public class CollectionsController : ControllerBase
{
    private readonly ICollectionService _service;
    private readonly IUserRepository _users;
    private readonly ICurrentUser _current;
    private readonly ICollectionScope _scope;

    public CollectionsController(ICollectionService service, IUserRepository users, ICurrentUser current,
        ICollectionScope scope)
    {
        _service = service;
        _users = users;
        _current = current;
        _scope = scope;
    }

    /// <summary>Admins see every collection; everyone else only the ones they are a member of.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<CollectionResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        var collections = _current.IsAdmin
            ? await _service.GetAllAsync()
            : await _service.GetForUserAsync(_current.RequireUserId(), isAdmin: false);
        return Ok(collections.Select(MapToResponse));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(CollectionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        if (!_current.IsAdmin && !_current.CollectionIds.Contains(id)) return NotFound();
        var collection = await _service.GetByIdAsync(id);
        return collection is null ? NotFound() : Ok(MapToResponse(collection));
    }

    [HttpPost]
    [Authorize(Roles = nameof(UserRole.Admin))]
    [ProducesResponseType(typeof(CollectionResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateCollectionRequest request)
    {
        try
        {
            var created = await _service.CreateAsync(request.Name, request.Description);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, MapToResponse(created));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    [ProducesResponseType(typeof(CollectionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCollectionRequest request)
    {
        try
        {
            var updated = await _service.UpdateAsync(id, request.Name, request.Description);
            return updated is null ? NotFound() : Ok(MapToResponse(updated));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var deleted = await _service.DeleteAsync(id);
        return deleted ? NoContent() : NotFound();
    }

    [HttpGet("{id:guid}/members")]
    [ProducesResponseType(typeof(IEnumerable<CollectionMemberResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMembers(Guid id)
    {
        if (DenyMemberManagement(id) is { } denied) return denied;

        var members = (await _service.GetMembersAsync(id)).ToList();
        var users = (await _users.GetAllAsync()).ToDictionary(u => u.Id);
        var response = members
            .Select(m => users.TryGetValue(m.UserId, out var u)
                ? new CollectionMemberResponse(m.UserId, u.Email, u.DisplayName, m.Access.ToString(), m.AddedAt)
                : new CollectionMemberResponse(m.UserId, "(unknown)", "(unknown)", m.Access.ToString(), m.AddedAt));
        return Ok(response);
    }

    [HttpPost("{id:guid}/members")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetMember(Guid id, [FromBody] SetCollectionMemberRequest request)
    {
        if (!Enum.TryParse<CollectionAccess>(request.Access, ignoreCase: true, out var access))
            return BadRequest(new { error = $"Unknown access level '{request.Access}'." });

        if (DenyMemberManagement(id) is { } denied) return denied;

        if (await _service.GetByIdAsync(id) is null) return NotFound();
        if (await _users.GetByIdAsync(request.UserId) is null) return NotFound();

        // Demoting the last Owner would leave the collection with nobody able to manage it: the
        // same lock-out guard UsersController applies to the last admin.
        if (access != CollectionAccess.Owner && await WouldOrphanAsync(id, request.UserId)) return OrphanResponse();

        await _service.SetMemberAsync(id, request.UserId, access);
        return NoContent();
    }

    [HttpDelete("{id:guid}/members/{userId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveMember(Guid id, Guid userId)
    {
        if (DenyMemberManagement(id) is { } denied) return denied;

        if (await WouldOrphanAsync(id, userId)) return OrphanResponse();

        var removed = await _service.RemoveMemberAsync(id, userId);
        return removed ? NoContent() : NotFound();
    }

    /// <summary>
    /// Active accounts that are not members yet, for the "add member" picker. Available to an
    /// admin or the collection's Owner — the same rule as the member list — instead of widening
    /// <c>GET /api/users</c>, which stays admin-only.
    /// </summary>
    [HttpGet("{id:guid}/member-candidates")]
    [ProducesResponseType(typeof(IEnumerable<CollectionMemberCandidateResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMemberCandidates(Guid id)
    {
        if (DenyMemberManagement(id) is { } denied) return denied;

        var members = (await _service.GetMembersAsync(id)).Select(m => m.UserId).ToHashSet();
        var candidates = (await _users.GetAllAsync())
            .Where(u => u.Active && !members.Contains(u.Id))
            .OrderBy(u => u.DisplayName)
            .Select(u => new CollectionMemberCandidateResponse(u.Id, u.Email, u.DisplayName));

        return Ok(candidates);
    }

    /// <summary>
    /// Member management is reserved to an admin or the <see cref="CollectionAccess.Owner"/> of the
    /// collection: an <see cref="CollectionAccess.Editor"/> may write the collection's data but not
    /// its membership, and a non-member must not learn that the collection exists (404, not 403).
    /// </summary>
    private IActionResult? DenyMemberManagement(Guid collectionId)
    {
        if (!_scope.CanAccess(collectionId)) return NotFound();

        if (!_scope.CanManageMembers(collectionId))
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                error = "Only an Owner of this collection (or an admin) can manage its members.",
            });

        return null;
    }

    private async Task<bool> WouldOrphanAsync(Guid collectionId, Guid userId)
    {
        var members = (await _service.GetMembersAsync(collectionId)).ToList();
        var target = members.FirstOrDefault(m => m.UserId == userId);
        if (target is null || target.Access != CollectionAccess.Owner) return false;

        return members.Count(m => m.Access == CollectionAccess.Owner) <= 1;
    }

    private IActionResult OrphanResponse() => BadRequest(new
    {
        error = "A collection must keep at least one Owner: grant Owner to another member first.",
    });

    private static CollectionResponse MapToResponse(Collection c) =>
        new(c.Id, c.Name, c.Description, c.CreatedAt, c.UpdatedAt);
}
