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

    public CollectionsController(ICollectionService service, IUserRepository users, ICurrentUser current)
    {
        _service = service;
        _users = users;
        _current = current;
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
    [Authorize(Roles = nameof(UserRole.Admin))]
    [ProducesResponseType(typeof(IEnumerable<CollectionMemberResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMembers(Guid id)
    {
        var members = (await _service.GetMembersAsync(id)).ToList();
        var users = (await _users.GetAllAsync()).ToDictionary(u => u.Id);
        var response = members
            .Select(m => users.TryGetValue(m.UserId, out var u)
                ? new CollectionMemberResponse(m.UserId, u.Email, u.DisplayName, m.Access.ToString(), m.AddedAt)
                : new CollectionMemberResponse(m.UserId, "(unknown)", "(unknown)", m.Access.ToString(), m.AddedAt));
        return Ok(response);
    }

    [HttpPost("{id:guid}/members")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetMember(Guid id, [FromBody] SetCollectionMemberRequest request)
    {
        if (!Enum.TryParse<CollectionAccess>(request.Access, ignoreCase: true, out var access))
            return BadRequest(new { error = $"Unknown access level '{request.Access}'." });

        if (await _service.GetByIdAsync(id) is null) return NotFound();
        if (await _users.GetByIdAsync(request.UserId) is null) return NotFound();

        await _service.SetMemberAsync(id, request.UserId, access);
        return NoContent();
    }

    [HttpDelete("{id:guid}/members/{userId:guid}")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveMember(Guid id, Guid userId)
    {
        var removed = await _service.RemoveMemberAsync(id, userId);
        return removed ? NoContent() : NotFound();
    }

    private static CollectionResponse MapToResponse(Collection c) =>
        new(c.Id, c.Name, c.Description, c.CreatedAt, c.UpdatedAt);
}
