using Microsoft.AspNetCore.Mvc;
using TroveKeep.Api.DTOs.Requests;
using TroveKeep.Api.DTOs.Responses;
using TroveKeep.Core.Interfaces.Services;
using TroveKeep.Core.Models;

namespace TroveKeep.Api.Controllers;

[ApiController]
[Route("api/drawers")]
public class DrawersController : ControllerBase
{
    private readonly IDrawerService _service;

    public DrawersController(IDrawerService service)
    {
        _service = service;
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(DrawerResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var drawer = await _service.GetByIdAsync(id);
        if (drawer is null) return NotFound();
        return Ok(MapToResponse(drawer));
    }

    [HttpGet("{id:guid}/contents")]
    [ProducesResponseType(typeof(DrawerDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetContents(Guid id)
    {
        var drawer = await _service.GetByIdWithContentsAsync(id);
        if (drawer is null) return NotFound();
        return Ok(MapToDetailResponse(drawer));
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(DrawerResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateDrawerRequest request)
    {
        var model = new Drawer { Id = id, Position = request.Position, Label = request.Label };
        var updated = await _service.UpdateAsync(model);
        if (updated is null) return NotFound();
        return Ok(MapToResponse(updated));
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var deleted = await _service.DeleteAsync(id);
        if (!deleted) return NotFound();
        return NoContent();
    }

    private static DrawerResponse MapToResponse(Drawer d) =>
        new(d.Id, d.Position, d.Label, d.DrawerContainerId, d.BulkPieces.Count, d.CreatedAt, d.UpdatedAt);

    private static DrawerDetailResponse MapToDetailResponse(Drawer d) =>
        new(d.Id, d.Position, d.Label, d.DrawerContainerId,
            d.BulkPieces.Select(p => new BulkPieceResponse(p.Id, p.LegoId, p.LegoColor, p.Description, p.Quantity, p.BoxId, p.DrawerId, p.CreatedAt, p.UpdatedAt)),
            d.CreatedAt, d.UpdatedAt);
}
