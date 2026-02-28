using Microsoft.AspNetCore.Mvc;
using TroveKeep.Api.DTOs.Requests;
using TroveKeep.Api.DTOs.Responses;
using TroveKeep.Core.Interfaces.Repositories;
using TroveKeep.Core.Interfaces.Services;
using TroveKeep.Core.Models;

namespace TroveKeep.Api.Controllers;

[ApiController]
[Route("api/bulkpieces")]
public class BulkPiecesController : ControllerBase
{
    private readonly IBulkPieceService _service;
    private readonly IColorRepository _colorRepo;
    private readonly IImageService _imageService;
    private readonly IPartInventoryArchiveRepository _partInventoryRepo;

    public BulkPiecesController(IBulkPieceService service, IColorRepository colorRepo, IImageService imageService, IPartInventoryArchiveRepository partInventoryRepo)
    {
        _service = service;
        _colorRepo = colorRepo;
        _imageService = imageService;
        _partInventoryRepo = partInventoryRepo;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<BulkPieceResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        var pieces = await _service.GetAllAsync();
        var colors = await BuildColorLookupAsync();
        return Ok(pieces.Select(p => MapToResponse(p, colors)));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(BulkPieceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var piece = await _service.GetByIdAsync(id);
        if (piece is null) return NotFound();
        var colors = await BuildColorLookupAsync();
        return Ok(MapToResponse(piece, colors));
    }

    [HttpPost]
    [ProducesResponseType(typeof(BulkPieceResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateBulkPieceRequest request)
    {
        var model = new BulkPiece
        {
            LegoId = request.LegoId,
            LegoColorId = request.LegoColorId,
            Description = request.Description,
            Quantity = request.Quantity,
        };
        var created = await _service.CreateAsync(model);
        var partInventory = await _partInventoryRepo.GetByPartNumAsync(created.LegoId);
        if (partInventory is not null && !string.IsNullOrWhiteSpace(partInventory.ImgUrl))
        {
            var existing = await _imageService.GetImageAsync(created.LegoId, ImageReferenceType.Part);
            if (existing is not null)
                await _service.UpdateImageCachedAsync(created.Id);
            else
                _ = _imageService.DownloadAndStoreAsync(created.Id, created.LegoId, partInventory.ImgUrl, ImageReferenceType.Part);
        }
        var colors = await BuildColorLookupAsync();
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, MapToResponse(created, colors));
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
            LegoColorId = request.LegoColorId,
            Description = request.Description,
            Quantity = request.Quantity,
        };
        var updated = await _service.UpdateAsync(model);
        if (updated is null) return NotFound();
        var colors = await BuildColorLookupAsync();
        return Ok(MapToResponse(updated, colors));
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
            var colors = await BuildColorLookupAsync();
            return Ok(MapToResponse(updated, colors));
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
            var colors = await BuildColorLookupAsync();
            return Ok(MapToResponse(updated, colors));
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
        var colors = await BuildColorLookupAsync();
        return Ok(MapToResponse(updated, colors));
    }

    [HttpDelete("{id:guid}/storage")]
    [ProducesResponseType(typeof(BulkPieceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ClearStorage(Guid id)
    {
        var updated = await _service.ClearStorageAsync(id);
        if (updated is null) return NotFound();
        var colors = await BuildColorLookupAsync();
        return Ok(MapToResponse(updated, colors));
    }

    [HttpGet("{id:guid}/image")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetImage(Guid id)
    {
        var piece = await _service.GetByIdAsync(id);
        if (piece is null) return NotFound();
        var image = await _imageService.GetImageAsync(piece.LegoId, ImageReferenceType.Part);
        if (image is null) return NotFound();
        return File(image.Data, image.ContentType);
    }

    private async Task<Dictionary<int, (string Name, string Rgb)>> BuildColorLookupAsync()
    {
        var colors = await _colorRepo.GetAllAsync();
        return colors.ToDictionary(c => c.Id, c => (c.Name, c.Rgb));
    }

    private static BulkPieceResponse MapToResponse(
        BulkPiece p,
        Dictionary<int, (string Name, string Rgb)> colors)
    {
        colors.TryGetValue(p.LegoColorId, out var color);
        return new BulkPieceResponse(
            p.Id, p.LegoId,
            p.LegoColorId, color.Name, color.Rgb,
            p.Description, p.Quantity, p.ImageCached,
            p.StorageAllocations.Select(a => new StorageAllocationResponse(a.StorageId, a.Type.ToString(), a.Quantity)),
            p.CreatedAt, p.UpdatedAt);
    }
}
