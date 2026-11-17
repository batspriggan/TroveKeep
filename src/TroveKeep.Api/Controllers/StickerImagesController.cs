using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TroveKeep.Api.DTOs.Requests;
using TroveKeep.Api.DTOs.Responses;
using TroveKeep.Api.Filters;
using TroveKeep.Core.Exceptions;
using TroveKeep.Core.Interfaces.Services;
using TroveKeep.Core.Models;

namespace TroveKeep.Api.Controllers;

/// <summary>
/// The global sticker image library: the artwork that sticker projects print, with no size of its
/// own (the footprint lives on the project item).
///
/// <para>
/// <b>Reads</b> are open to every regular account (a Viewer may browse and print the library — its
/// artwork is not collection data). <b>Writes</b> are authorized by
/// <see cref="CollectionWriteAnywhereFilter"/>: an admin, or a member with Editor/Owner access in
/// at least one collection. A Guest has no use for the page at all, hence the role gate.
/// </para>
/// </summary>
[Authorize(Roles = "User,Admin")]
[ApiController]
[Route("api/sticker-images")]
public class StickerImagesController : ControllerBase
{
    /// <summary>Upload limit for artwork (the SPA downsamples before uploading).</summary>
    private const long MaxImageBytes = 10_000_000;

    private readonly IStickerImageService _service;
    private readonly IImageService _imageService;

    public StickerImagesController(IStickerImageService service, IImageService imageService)
    {
        _service = service;
        _imageService = imageService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<StickerImageResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        var images = await _service.GetAllAsync();
        return Ok(images.Select(MapToResponse));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(StickerImageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var image = await _service.GetByIdAsync(id);
        if (image is null) return NotFound();
        return Ok(MapToResponse(image));
    }

    [HttpPost]
    [ServiceFilter(typeof(CollectionWriteAnywhereFilter))]
    [ProducesResponseType(typeof(StickerImageResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create([FromBody] CreateStickerImageRequest request)
    {
        try
        {
            var created = await _service.CreateAsync(new StickerImage { Name = request.Name });
            return StatusCode(StatusCodes.Status201Created, MapToResponse(created));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    [ServiceFilter(typeof(CollectionWriteAnywhereFilter))]
    [ProducesResponseType(typeof(StickerImageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateStickerImageRequest request)
    {
        try
        {
            var updated = await _service.UpdateAsync(id, new StickerImage
            {
                Name = request.Name,
                Version = request.Version,
            });
            return Ok(MapToResponse(updated));
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (ConcurrencyException ex) { return Conflict(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    /// <summary>
    /// Deletes an image. Refused with a 400 when a project item still prints it (the message names
    /// how many items use it), so the sheet can never point at vanished artwork.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ServiceFilter(typeof(CollectionWriteAnywhereFilter))]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            var deleted = await _service.DeleteAsync(id);
            if (!deleted) return NotFound();
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// The artwork of an image. Answers 404 when the row carries no artwork
    /// (<c>imageCached = false</c>), so clients can use <c>ImageCached</c> to avoid the request
    /// entirely and still get a clean miss.
    /// </summary>
    [HttpGet("{id:guid}/image")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetImage(Guid id)
    {
        var image = await _service.GetByIdAsync(id);
        if (image is null || !image.ImageCached) return NotFound();

        var img = await _imageService.GetImageAsync(id.ToString(), ImageReferenceType.StickerImage);
        if (img is null) return NotFound();

        return File(img.Data, img.ContentType);
    }

    /// <summary>
    /// Stores (or replaces) the artwork. PNG and JPEG only, checked on the <em>bytes</em> as well as
    /// the declared content type: the sheet engine relies on a decodable raster, and a mislabelled
    /// upload fails on the client-side canvas rather than here, where the error is actionable.
    /// Answers 200 with the updated row so the client gets the new <c>version</c> without a refetch.
    /// </summary>
    [HttpPost("{id:guid}/image")]
    [ServiceFilter(typeof(CollectionWriteAnywhereFilter))]
    [RequestSizeLimit(MaxImageBytes)]
    [ProducesResponseType(typeof(StickerImageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UploadImage(Guid id, IFormFile file)
    {
        var image = await _service.GetByIdAsync(id);
        if (image is null) return NotFound();
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "No file provided." });
        if (file.Length > MaxImageBytes)
            return BadRequest(new { error = "The image is too large (10 MB maximum)." });

        byte[] data;
        using (var ms = new MemoryStream())
        {
            await file.OpenReadStream().CopyToAsync(ms);
            data = ms.ToArray();
        }

        var contentType = ResolveImageContentType(file.ContentType, data);
        if (contentType is null)
            return BadRequest(new { error = "Unsupported image format: upload a PNG or a JPEG file." });

        using var content = new MemoryStream(data);
        await _imageService.StoreUploadAsync(id.ToString(), ImageReferenceType.StickerImage, content, contentType);
        var updated = await _service.UpdateImageCachedAsync(id, true);
        return Ok(MapToResponse(updated));
    }

    /// <summary>Removes the artwork. Answers 200 with the updated row (see <see cref="UploadImage"/>).</summary>
    [HttpDelete("{id:guid}/image")]
    [ServiceFilter(typeof(CollectionWriteAnywhereFilter))]
    [ProducesResponseType(typeof(StickerImageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteImage(Guid id)
    {
        var image = await _service.GetByIdAsync(id);
        if (image is null) return NotFound();

        await _imageService.DeleteAsync(id.ToString(), ImageReferenceType.StickerImage);
        var updated = await _service.UpdateImageCachedAsync(id, false);
        return Ok(MapToResponse(updated));
    }

    private static StickerImageResponse MapToResponse(StickerImage image) =>
        new(image.Id, image.Name, image.ImageCached, image.CreatedAt, image.UpdatedAt, image.Version);

    /// <summary>
    /// Accepts a PNG or JPEG payload. The declared content type must be one of the two and the
    /// magic bytes must agree with it, so a <c>.txt</c> renamed to <c>.png</c> is a 400 and not a
    /// broken sticker.
    /// </summary>
    private static string? ResolveImageContentType(string? declared, byte[] data)
    {
        var isPng = data.Length > 7
            && data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47
            && data[4] == 0x0D && data[5] == 0x0A && data[6] == 0x1A && data[7] == 0x0A;
        var isJpeg = data.Length > 2
            && data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF;

        return (declared ?? string.Empty).ToLowerInvariant() switch
        {
            "image/png" when isPng => "image/png",
            "image/jpeg" when isJpeg => "image/jpeg",
            "image/jpg" when isJpeg => "image/jpeg",
            _ => null,
        };
    }
}
