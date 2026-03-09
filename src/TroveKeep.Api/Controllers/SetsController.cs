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
    private readonly IImageService _imageService;
    private readonly ISetPhotoService _photoService;

    public SetsController(ILegoSetService service, IImageService imageService, ISetPhotoService photoService)
    {
        _service = service;
        _imageService = imageService;
        _photoService = photoService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<LegoSetResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        var sets = (await _service.GetAllAsync()).ToList();
        var photoCounts = await _photoService.GetCountsBySetIdsAsync(sets.Select(s => s.Id));
        return Ok(sets.Select(s => MapToResponse(s, photoCounts.GetValueOrDefault(s.Id))));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(LegoSetResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var set = await _service.GetByIdAsync(id);
        if (set is null) return NotFound();
        var photos = await _photoService.GetBySetIdAsync(id);
        return Ok(MapToResponse(set, photos.Count()));
    }

    [HttpPost]
    [ProducesResponseType(typeof(LegoSetResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateLegoSetRequest request)
    {
        var model = new LegoSet
        {
            SetNumber = request.SetNumber ?? string.Empty,
            Description = request.Description,
            Quantity = request.Quantity,
            IsMoc = request.IsMoc,
        };
        var created = await _service.CreateAsync(model);
        if (!string.IsNullOrWhiteSpace(request.PhotoUrl))
            _ = _imageService.DownloadAndStoreAsync(created.Id, created.SetNumber, request.PhotoUrl, ImageReferenceType.Set);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, MapToResponse(created, 0));
    }

    [HttpGet("{id:guid}/image")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetImage(Guid id)
    {
        var set = await _service.GetByIdAsync(id);
        if (set is null) return NotFound();
        var image = await _imageService.GetImageAsync(set.SetNumber, ImageReferenceType.Set);
        if (image is null) return NotFound();
        return File(image.Data, image.ContentType);
    }

    [HttpGet("{id:guid}/photos")]
    [ProducesResponseType(typeof(IEnumerable<SetPhotoResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPhotos(Guid id)
    {
        var set = await _service.GetByIdAsync(id);
        if (set is null) return NotFound();
        var photos = await _photoService.GetBySetIdAsync(id);
        return Ok(photos.Select(p => new SetPhotoResponse(p.Id, p.UploadedAt)));
    }

    [HttpGet("{id:guid}/photos/{photoId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPhoto(Guid id, Guid photoId)
    {
        var photo = await _photoService.GetByIdAsync(photoId);
        if (photo is null || photo.SetId != id) return NotFound();
        return File(photo.Data, photo.ContentType);
    }

    [HttpPost("{id:guid}/photos")]
    [ProducesResponseType(typeof(SetPhotoResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [RequestSizeLimit(20_000_000)]
    public async Task<IActionResult> UploadPhoto(Guid id, IFormFile file)
    {
        var set = await _service.GetByIdAsync(id);
        if (set is null) return NotFound();
        if (file is null || file.Length == 0) return BadRequest(new { error = "No file provided." });

        var photoId = await _photoService.UploadAsync(id, file.OpenReadStream(), file.ContentType);
        var photo = await _photoService.GetByIdAsync(photoId);
        return CreatedAtAction(nameof(GetPhoto), new { id, photoId }, new SetPhotoResponse(photo!.Id, photo.UploadedAt));
    }

    [HttpDelete("{id:guid}/photos/{photoId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeletePhoto(Guid id, Guid photoId)
    {
        var photo = await _photoService.GetByIdAsync(photoId);
        if (photo is null || photo.SetId != id) return NotFound();
        await _photoService.DeleteAsync(photoId);
        return NoContent();
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
            SetNumber = request.SetNumber ?? string.Empty,
            Description = request.Description,
            Quantity = request.Quantity,
            IsMoc = request.IsMoc,
        };
        var updated = await _service.UpdateAsync(model);
        if (updated is null) return NotFound();
        var photos = await _photoService.GetBySetIdAsync(id);
        return Ok(MapToResponse(updated, photos.Count()));
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var deleted = await _service.DeleteAsync(id);
        if (!deleted) return NotFound();
        await _photoService.DeleteBySetIdAsync(id);
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
            var photos = await _photoService.GetBySetIdAsync(id);
            return Ok(MapToResponse(updated, photos.Count()));
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
        var photos = await _photoService.GetBySetIdAsync(id);
        return Ok(MapToResponse(updated, photos.Count()));
    }

    [HttpDelete("{id:guid}/storage")]
    [ProducesResponseType(typeof(LegoSetResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ClearStorage(Guid id)
    {
        var updated = await _service.ClearStorageAsync(id);
        if (updated is null) return NotFound();
        var photos = await _photoService.GetBySetIdAsync(id);
        return Ok(MapToResponse(updated, photos.Count()));
    }

    private static LegoSetResponse MapToResponse(LegoSet s, int photoCount) =>
        new(s.Id, s.SetNumber, s.Description, s.Quantity, s.IsMoc, s.ImageCached, photoCount,
            s.StorageAllocations.Select(a => new StorageAllocationResponse(a.StorageId, a.StoragePosition, a.StorageType.ToString(), a.Quantity)),
            s.CreatedAt, s.UpdatedAt);
}
