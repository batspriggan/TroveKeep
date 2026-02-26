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
            Description = request.Description
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
            Description = request.Description
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

    [HttpGet("{id:guid}/storage")]
    [ProducesResponseType(typeof(StorageLocationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStorage(Guid id)
    {
        var piece = await _service.GetByIdAsync(id);
        if (piece is null) return NotFound();
        var storage = await _service.GetStorageAsync(id);
        if (storage is null) return NoContent();
        return Ok(MapStorageToResponse(storage));
    }

    [HttpPut("{id:guid}/storage/box/{boxId:guid}")]
    [ProducesResponseType(typeof(BulkPieceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AssignToBox(Guid id, Guid boxId)
    {
        var updated = await _service.AssignToBoxAsync(id, boxId);
        if (updated is null) return NotFound();
        return Ok(MapToResponse(updated));
    }

    [HttpPut("{id:guid}/storage/drawer/{drawerId:guid}")]
    [ProducesResponseType(typeof(BulkPieceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AssignToDrawer(Guid id, Guid drawerId)
    {
        var updated = await _service.AssignToDrawerAsync(id, drawerId);
        if (updated is null) return NotFound();
        return Ok(MapToResponse(updated));
    }

    [HttpDelete("{id:guid}/storage")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveStorage(Guid id)
    {
        var removed = await _service.RemoveStorageAsync(id);
        if (!removed) return NotFound();
        return NoContent();
    }

    private static BulkPieceResponse MapToResponse(BulkPiece p) =>
        new(p.Id, p.LegoId, p.LegoColor, p.Description, p.BoxId, p.DrawerId, p.CreatedAt, p.UpdatedAt);

    private static StorageLocationResponse MapStorageToResponse(StorageLocation s) =>
        new(s.Type.ToString(), s.StorageId, s.StorageName, s.DrawerContainerId, s.DrawerContainerName, s.DrawerPosition);
}
