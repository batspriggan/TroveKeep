using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TroveKeep.Api.DTOs.Requests;
using TroveKeep.Api.DTOs.Responses;
using TroveKeep.Core.Interfaces.Services;
using TroveKeep.Core.Models;

namespace TroveKeep.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Roles = nameof(UserRole.Admin))]
public class UsersController : ControllerBase
{
    private readonly IUserService _service;

    public UsersController(IUserService service)
    {
        _service = service;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<UserResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        var users = await _service.GetAllAsync();
        return Ok(users.Select(MapToResponse));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var user = await _service.GetByIdAsync(id);
        return user is null ? NotFound() : Ok(MapToResponse(user));
    }

    [HttpPost]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest request)
    {
        if (!Enum.TryParse<UserRole>(request.Role, ignoreCase: true, out var role))
            return BadRequest(new { error = $"Unknown role '{request.Role}'." });

        try
        {
            var created = await _service.CreateAsync(request.Email, request.DisplayName, request.Password, role);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, MapToResponse(created));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUserRequest request)
    {
        if (!Enum.TryParse<UserRole>(request.Role, ignoreCase: true, out var role))
            return BadRequest(new { error = $"Unknown role '{request.Role}'." });

        var updated = await _service.UpdateAsync(id, request.DisplayName, role, request.Active);
        return updated is null ? NotFound() : Ok(MapToResponse(updated));
    }

    [HttpPost("{id:guid}/password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetPassword(Guid id, [FromBody] SetUserPasswordRequest request)
    {
        try
        {
            var ok = await _service.SetPasswordAsync(id, request.NewPassword);
            return ok ? NoContent() : NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id)
    {
        // Guard: an admin must not delete the only remaining admin (or themselves) and lock the
        // instance out. Checked here rather than in the service because it needs the caller's id.
        var all = (await _service.GetAllAsync()).ToList();
        var target = all.FirstOrDefault(u => u.Id == id);
        if (target is null) return NotFound();

        var admins = all.Count(u => u.Role == UserRole.Admin && u.Active);
        if (target.Role == UserRole.Admin && admins <= 1)
            return BadRequest(new { error = "Cannot delete the last remaining admin." });

        await _service.DeleteAsync(id);
        return NoContent();
    }

    private static UserResponse MapToResponse(User u) =>
        new(u.Id, u.Email, u.DisplayName, u.Role.ToString(), u.Active,
            u.PasswordHash is not null, u.ExternalProvider, u.CreatedAt, u.UpdatedAt);
}
