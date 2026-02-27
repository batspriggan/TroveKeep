using Microsoft.AspNetCore.Mvc;
using TroveKeep.Api.DTOs.Requests;
using TroveKeep.Api.DTOs.Responses;
using TroveKeep.Core.Interfaces.Repositories;
using TroveKeep.Core.Interfaces.Services;
using TroveKeep.Core.Models;

namespace TroveKeep.Api.Controllers;

[ApiController]
[Route("api/boxes")]
public class BoxesController : ControllerBase
{
    private readonly IBoxService _service;
    private readonly IColorRepository _colorRepo;

    public BoxesController(IBoxService service, IColorRepository colorRepo)
    {
        _service = service;
        _colorRepo = colorRepo;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<BoxResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        var boxes = await _service.GetAllAsync();
        return Ok(boxes.Select(MapToResponse));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(BoxResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var box = await _service.GetByIdAsync(id);
        if (box is null) return NotFound();
        return Ok(MapToResponse(box));
    }

    [HttpGet("{id:guid}/contents")]
    [ProducesResponseType(typeof(BoxDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetContents(Guid id)
    {
        var box = await _service.GetByIdWithContentsAsync(id);
        if (box is null) return NotFound();
        var colors = await BuildColorLookupAsync();
        return Ok(MapToDetailResponse(box, colors));
    }

    [HttpPost]
    [ProducesResponseType(typeof(BoxResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateBoxRequest request)
    {
        var model = new Box { Name = request.Name, PhotoUrl = request.PhotoUrl };
        var created = await _service.CreateAsync(model);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, MapToResponse(created));
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(BoxResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateBoxRequest request)
    {
        var model = new Box { Id = id, Name = request.Name, PhotoUrl = request.PhotoUrl };
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

    private async Task<Dictionary<int, (string Name, string Rgb)>> BuildColorLookupAsync()
    {
        var colors = await _colorRepo.GetAllAsync();
        return colors.ToDictionary(c => c.Id, c => (c.Name, c.Rgb));
    }

    private static BoxResponse MapToResponse(Box b) =>
        new(b.Id, b.Name, b.PhotoUrl, b.Sets.Count, b.BulkPieces.Count, b.CreatedAt, b.UpdatedAt);

    private static BoxDetailResponse MapToDetailResponse(Box b, Dictionary<int, (string Name, string Rgb)> colors) =>
        new(b.Id, b.Name, b.PhotoUrl,
            b.Sets.Select(s => new LegoSetResponse(s.Id, s.SetNumber, s.Description, s.PhotoUrl, s.Quantity, s.ImageCached,
                s.StorageAllocations.Select(a => new StorageAllocationResponse(a.StorageId, a.Type.ToString(), a.Quantity)),
                s.CreatedAt, s.UpdatedAt)),
            b.BulkPieces.Select(p =>
            {
                colors.TryGetValue(p.LegoColorId, out var color);
                return new BulkPieceResponse(p.Id, p.LegoId,
                    p.LegoColorId, color.Name, color.Rgb,
                    p.Description, p.Quantity,
                    p.StorageAllocations.Select(a => new StorageAllocationResponse(a.StorageId, a.Type.ToString(), a.Quantity)),
                    p.CreatedAt, p.UpdatedAt);
            }),
            b.CreatedAt, b.UpdatedAt);
}
