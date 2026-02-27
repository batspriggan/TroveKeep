using Microsoft.AspNetCore.Mvc;
using TroveKeep.Api.DTOs.Requests;
using TroveKeep.Api.DTOs.Responses;
using TroveKeep.Core.Interfaces.Services;
using TroveKeep.Core.Models;

namespace TroveKeep.Api.Controllers;

[ApiController]
[Route("api/sets")]
public class SetsController : ControllerBase
{
    private readonly ILegoSetService _service;

    public SetsController(ILegoSetService service)
    {
        _service = service;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<LegoSetResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        var sets = await _service.GetAllAsync();
        return Ok(sets.Select(MapToResponse));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(LegoSetResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var set = await _service.GetByIdAsync(id);
        if (set is null) return NotFound();
        return Ok(MapToResponse(set));
    }

    [HttpPost]
    [ProducesResponseType(typeof(LegoSetResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateLegoSetRequest request)
    {
        var model = new LegoSet
        {
            SetNumber = request.SetNumber,
            Description = request.Description,
            PhotoUrl = request.PhotoUrl,
            Quantity = request.Quantity,
        };
        var created = await _service.CreateAsync(model);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, MapToResponse(created));
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(LegoSetResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateLegoSetRequest request)
    {
        var model = new LegoSet
        {
            Id = id,
            SetNumber = request.SetNumber,
            Description = request.Description,
            PhotoUrl = request.PhotoUrl,
            Quantity = request.Quantity,
        };
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

    [HttpPost("{id:guid}/storage/box/{boxId:guid}")]
    [ProducesResponseType(typeof(LegoSetResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AllocateToBox(Guid id, Guid boxId, [FromBody] AllocateStorageRequest request)
    {
        try
        {
            var updated = await _service.AllocateToBoxAsync(id, boxId, request.Quantity);
            if (updated is null) return NotFound();
            return Ok(MapToResponse(updated));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("{id:guid}/storage/{storageId:guid}")]
    [ProducesResponseType(typeof(LegoSetResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeallocateStorage(Guid id, Guid storageId)
    {
        var updated = await _service.DeallocateStorageAsync(id, storageId);
        if (updated is null) return NotFound();
        return Ok(MapToResponse(updated));
    }

    [HttpDelete("{id:guid}/storage")]
    [ProducesResponseType(typeof(LegoSetResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ClearStorage(Guid id)
    {
        var updated = await _service.ClearStorageAsync(id);
        if (updated is null) return NotFound();
        return Ok(MapToResponse(updated));
    }

    private static LegoSetResponse MapToResponse(LegoSet s) =>
        new(s.Id, s.SetNumber, s.Description, s.PhotoUrl, s.Quantity,
            s.StorageAllocations.Select(a => new StorageAllocationResponse(a.StorageId, a.Type.ToString(), a.Quantity)),
            s.CreatedAt, s.UpdatedAt);
}
