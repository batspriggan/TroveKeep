using Microsoft.AspNetCore.Mvc;
using TroveKeep.Api.DTOs.Requests;
using TroveKeep.Api.DTOs.Responses;
using TroveKeep.Core.Interfaces.Services;
using TroveKeep.Core.Models;

namespace TroveKeep.Api.Controllers;

[ApiController]
[Route("api/bulkpieces")]
public class BulkPiecesController : ControllerBase
{
    private readonly IBulkPieceService _service;

    public BulkPiecesController(IBulkPieceService service)
    {
        _service = service;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<BulkPieceResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        var pieces = await _service.GetAllAsync();
        return Ok(pieces.Select(MapToResponse));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(BulkPieceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var piece = await _service.GetByIdAsync(id);
        if (piece is null) return NotFound();
        return Ok(MapToResponse(piece));
    }

    [HttpPost]
    [ProducesResponseType(typeof(BulkPieceResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateBulkPieceRequest request)
    {
        var model = new BulkPiece
        {
            LegoId = request.LegoId,
            LegoColor = request.LegoColor,
            Description = request.Description,
            Quantity = request.Quantity,
        };
        var created = await _service.CreateAsync(model);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, MapToResponse(created));
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(BulkPieceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateBulkPieceRequest request)
    {
        var model = new BulkPiece
        {
            Id = id,
            LegoId = request.LegoId,
            LegoColor = request.LegoColor,
            Description = request.Description,
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
    [ProducesResponseType(typeof(BulkPieceResponse), StatusCodes.Status200OK)]
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

    [HttpPost("{id:guid}/storage/drawer/{drawerId:guid}")]
    [ProducesResponseType(typeof(BulkPieceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AllocateToDrawer(Guid id, Guid drawerId, [FromBody] AllocateStorageRequest request)
    {
        try
        {
            var updated = await _service.AllocateToDrawerAsync(id, drawerId, request.Quantity);
            if (updated is null) return NotFound();
            return Ok(MapToResponse(updated));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("{id:guid}/storage/{storageId:guid}")]
    [ProducesResponseType(typeof(BulkPieceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeallocateStorage(Guid id, Guid storageId)
    {
        var updated = await _service.DeallocateStorageAsync(id, storageId);
        if (updated is null) return NotFound();
        return Ok(MapToResponse(updated));
    }

    [HttpDelete("{id:guid}/storage")]
    [ProducesResponseType(typeof(BulkPieceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ClearStorage(Guid id)
    {
        var updated = await _service.ClearStorageAsync(id);
        if (updated is null) return NotFound();
        return Ok(MapToResponse(updated));
    }

    private static BulkPieceResponse MapToResponse(BulkPiece p) =>
        new(p.Id, p.LegoId, p.LegoColor, p.Description, p.Quantity,
            p.StorageAllocations.Select(a => new StorageAllocationResponse(a.StorageId, a.Type.ToString(), a.Quantity)),
            p.CreatedAt, p.UpdatedAt);
}
