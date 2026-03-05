using Microsoft.AspNetCore.Mvc;
using TroveKeep.Api.DTOs.Requests;
using TroveKeep.Api.DTOs.Responses;
using TroveKeep.Core.Interfaces.Services;
using TroveKeep.Core.Models;

namespace TroveKeep.Api.Controllers;

[ApiController]
[Route("api/drawercontainers")]
public class DrawerContainersController : ControllerBase
{
    private readonly IDrawerContainerService _service;
    private readonly IImageService _imageService;

    public DrawerContainersController(IDrawerContainerService service, IImageService imageService)
    {
        _service = service;
        _imageService = imageService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<DrawerContainerResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        var containers = await _service.GetAllAsync();
        return Ok(containers.Select(MapToResponse));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(DrawerContainerResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var container = await _service.GetByIdAsync(id);
        if (container is null) return NotFound();
        return Ok(MapToResponse(container));
    }

    [HttpGet("{id:guid}/drawers")]
    [ProducesResponseType(typeof(DrawerContainerDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDrawers(Guid id)
    {
        var container = await _service.GetByIdWithDrawersAsync(id);
        if (container is null) return NotFound();
        return Ok(MapToDetailResponse(container));
    }

    [HttpPost]
    [ProducesResponseType(typeof(DrawerContainerResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateDrawerContainerRequest request)
    {
        var created = await _service.CreateAsync(request.Name, request.Description, request.DrawerCount);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, MapToResponse(created));
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(DrawerContainerResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateDrawerContainerRequest request)
    {
        var model = new DrawerContainer { Id = id, Name = request.Name, Description = request.Description };
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
        await _imageService.DeleteAsync(id.ToString(), ImageReferenceType.DrawerContainer);
        return NoContent();
    }

    [HttpPost("{id:guid}/drawers")]
    [ProducesResponseType(typeof(DrawerResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AddDrawer(Guid id, [FromBody] CreateDrawerRequest request)
    {
        var drawer = new Drawer
        {
            Position = request.Position,
            Label = request.Label,
            DrawerContainerId = id
        };
        var created = await _service.AddDrawerAsync(id, drawer);
        if (created is null) return NotFound();
        return CreatedAtAction("GetByPosition", "Drawers",
            new { containerId = created.DrawerContainerId, position = created.Position },
            MapDrawerToResponse(created));
    }

    [HttpGet("{id:guid}/image")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetImage(Guid id)
    {
        var image = await _imageService.GetImageAsync(id.ToString(), ImageReferenceType.DrawerContainer);
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
        var container = await _service.GetByIdAsync(id);
        if (container is null) return NotFound();
        await _imageService.StoreUploadAsync(id.ToString(), ImageReferenceType.DrawerContainer, file.OpenReadStream(), file.ContentType);
        await _service.UpdateImageCachedAsync(id, true);
        return NoContent();
    }

    [HttpDelete("{id:guid}/image")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteImage(Guid id)
    {
        var container = await _service.GetByIdAsync(id);
        if (container is null) return NotFound();
        await _imageService.DeleteAsync(id.ToString(), ImageReferenceType.DrawerContainer);
        await _service.UpdateImageCachedAsync(id, false);
        return NoContent();
    }

    private static DrawerContainerResponse MapToResponse(DrawerContainer c) =>
        new(c.Id, c.Name, c.Description, c.ImageCached, c.Drawers.Count, c.CreatedAt, c.UpdatedAt);

    private static DrawerContainerDetailResponse MapToDetailResponse(DrawerContainer c) =>
        new(c.Id, c.Name, c.Description, c.ImageCached,
            c.Drawers.Select(MapDrawerToResponse),
            c.CreatedAt, c.UpdatedAt);

    private static DrawerResponse MapDrawerToResponse(Drawer d) =>
        new(d.Position, d.Label, d.DrawerContainerId, d.BulkPieces.Count,
            d.BulkPieces.Count > 0 ? d.BulkPieces.Select(p => p.LegoId) : null,
            d.CreatedAt, d.UpdatedAt);
}
