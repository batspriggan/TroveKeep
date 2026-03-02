using Microsoft.AspNetCore.Mvc;
using TroveKeep.Api.DTOs.Requests;
using TroveKeep.Api.DTOs.Responses;
using TroveKeep.Core.Interfaces.Repositories;
using TroveKeep.Core.Interfaces.Services;
using TroveKeep.Core.Models;

namespace TroveKeep.Api.Controllers;

[ApiController]
[Route("api/drawers")]
public class DrawersController : ControllerBase
{
    private readonly IDrawerService _service;
    private readonly IColorRepository _colorRepo;

    public DrawersController(IDrawerService service, IColorRepository colorRepo)
    {
        _service = service;
        _colorRepo = colorRepo;
    }

    [HttpGet("{containerId:guid}/{position:int}")]
    [ProducesResponseType(typeof(DrawerResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByPosition(Guid containerId, int position)
    {
        var drawer = await _service.GetByPositionAsync(containerId, position);
        if (drawer is null) return NotFound();
        return Ok(MapToResponse(drawer));
    }

    [HttpGet("{containerId:guid}/{position:int}/contents")]
    [ProducesResponseType(typeof(DrawerDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetContents(Guid containerId, int position)
    {
        var drawer = await _service.GetByPositionWithContentsAsync(containerId, position);
        if (drawer is null) return NotFound();
        var colors = await BuildColorLookupAsync();
        return Ok(MapToDetailResponse(drawer, colors));
    }

    [HttpPut("{containerId:guid}/{position:int}")]
    [ProducesResponseType(typeof(DrawerResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update(Guid containerId, int position, [FromBody] UpdateDrawerRequest request)
    {
        var model = new Drawer { DrawerContainerId = containerId, Position = position, Label = request.Label };
        var updated = await _service.UpdateAsync(model);
        if (updated is null) return NotFound();
        return Ok(MapToResponse(updated));
    }

    [HttpDelete("{containerId:guid}/{position:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid containerId, int position)
    {
        var deleted = await _service.DeleteAsync(containerId, position);
        if (!deleted) return NotFound();
        return NoContent();
    }

    private async Task<Dictionary<int, (string Name, string Rgb)>> BuildColorLookupAsync()
    {
        var colors = await _colorRepo.GetAllAsync();
        return colors.ToDictionary(c => c.Id, c => (c.Name, c.Rgb));
    }

    private static DrawerResponse MapToResponse(Drawer d) =>
        new(d.Position, d.Label, d.DrawerContainerId, d.BulkPieces.Count, null, d.CreatedAt, d.UpdatedAt);

    private static DrawerDetailResponse MapToDetailResponse(Drawer d, Dictionary<int, (string Name, string Rgb)> colors) =>
        new(d.Position, d.Label, d.DrawerContainerId,
            d.BulkPieces.Select(p =>
            {
                colors.TryGetValue(p.LegoColorId, out var color);
                return new BulkPieceResponse(p.Id, p.LegoId,
                    p.LegoColorId, color.Name, color.Rgb,
                    p.Description, p.Quantity, p.ImageCached,
                    p.StorageAllocations.Select(a => new StorageAllocationResponse(a.StorageId, a.StoragePosition, a.StorageType.ToString(), a.Quantity)),
                    p.CreatedAt, p.UpdatedAt);
            }),
            d.CreatedAt, d.UpdatedAt);
}
