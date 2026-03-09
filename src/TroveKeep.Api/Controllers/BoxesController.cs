using Microsoft.AspNetCore.Mvc;
using TroveKeep.Api.DTOs.Requests;
using TroveKeep.Api.DTOs.Responses;
using TroveKeep.Core.Exceptions;
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
    private readonly IImageService _imageService;

    public BoxesController(IBoxService service, IColorRepository colorRepo, IImageService imageService)
    {
        _service = service;
        _colorRepo = colorRepo;
        _imageService = imageService;
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
        var model = new Box { Name = request.Name };
        var created = await _service.CreateAsync(model);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, MapToResponse(created));
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(BoxResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateBoxRequest request)
    {
        try
        {
            var model = new Box { Id = id, Name = request.Name, Version = request.Version };
            var updated = await _service.UpdateAsync(model);
            if (updated is null) return NotFound();
            return Ok(MapToResponse(updated));
        }
        catch (ConcurrencyException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var deleted = await _service.DeleteAsync(id);
        if (!deleted) return NotFound();
        await _imageService.DeleteAsync(id.ToString(), ImageReferenceType.Box);
        return NoContent();
    }

    [HttpGet("{id:guid}/image")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetImage(Guid id)
    {
        var image = await _imageService.GetImageAsync(id.ToString(), ImageReferenceType.Box);
        if (image is null) return NotFound();
        return File(image.Data, image.ContentType);
    }

    [HttpPost("{id:guid}/image")]
    [RequestSizeLimit(10_000_000)]
    [RequestFormLimits(MultipartBodyLengthLimit = 10_000_000)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UploadImage(Guid id, IFormFile file)
    {
        var box = await _service.GetByIdAsync(id);
        if (box is null) return NotFound();
        await _imageService.StoreUploadAsync(id.ToString(), ImageReferenceType.Box, file.OpenReadStream(), file.ContentType);
        await _service.UpdateImageCachedAsync(id, true);
        return NoContent();
    }

    [HttpDelete("{id:guid}/image")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteImage(Guid id)
    {
        var box = await _service.GetByIdAsync(id);
        if (box is null) return NotFound();
        await _imageService.DeleteAsync(id.ToString(), ImageReferenceType.Box);
        await _service.UpdateImageCachedAsync(id, false);
        return NoContent();
    }

    private async Task<Dictionary<int, (string Name, string Rgb)>> BuildColorLookupAsync()
    {
        var colors = await _colorRepo.GetAllAsync();
        return colors.ToDictionary(c => c.Id, c => (c.Name, c.Rgb));
    }

    private static BoxResponse MapToResponse(Box b) =>
        new(b.Id, b.Name, b.ImageCached, b.Sets.Count, b.BulkPieces.Count, b.CreatedAt, b.UpdatedAt, b.Version);

    private static BoxDetailResponse MapToDetailResponse(Box b, Dictionary<int, (string Name, string Rgb)> colors) =>
        new(b.Id, b.Name, b.ImageCached,
            b.Sets.Select(s => new LegoSetResponse(s.Id, s.SetNumber, s.Description, s.Quantity, s.IsMoc, s.ImageCached, 0,
                s.StorageAllocations.Select(a => new StorageAllocationResponse(a.StorageId, a.StoragePosition, a.StorageType.ToString(), a.Quantity)),
                s.CreatedAt, s.UpdatedAt, s.Version)),
            b.BulkPieces.Select(p =>
            {
                colors.TryGetValue(p.LegoColorId, out var color);
                return new BulkPieceResponse(p.Id, p.LegoId,
                    p.LegoColorId, color.Name, color.Rgb,
                    p.Description, p.Quantity, p.ImageCached,
                    p.StorageAllocations.Select(a => new StorageAllocationResponse(a.StorageId, a.StoragePosition, a.StorageType.ToString(), a.Quantity)),
                    p.CreatedAt, p.UpdatedAt, p.Version);
            }),
            b.CreatedAt, b.UpdatedAt, b.Version);
}
