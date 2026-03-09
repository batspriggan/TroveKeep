using Microsoft.AspNetCore.Mvc;
using TroveKeep.Api.DTOs.Requests;
using TroveKeep.Api.DTOs.Responses;
using TroveKeep.Core.Interfaces.Repositories;
using TroveKeep.Core.Interfaces.Services;
using TroveKeep.Core.Models;

namespace TroveKeep.Api.Controllers;

[ApiController]
[Route("api/baseplates")]
public class BaseplatesController : ControllerBase
{
    private readonly IBaseplateService _service;
    private readonly IColorRepository _colorRepo;
    private readonly IImageService _imageService;
    private readonly ISetPhotoService _photoService;

    public BaseplatesController(IBaseplateService service, IColorRepository colorRepo,
        IImageService imageService, ISetPhotoService photoService)
    {
        _service = service;
        _colorRepo = colorRepo;
        _imageService = imageService;
        _photoService = photoService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<BaseplateResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        var baseplates = await _service.GetAllAsync();
        var colors = await BuildColorLookupAsync();
        return Ok(baseplates.Select(b => MapToResponse(b, colors)));
    }

    [HttpPost]
    [ProducesResponseType(typeof(BaseplateResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateBaseplateRequest request)
    {
        var model = new Baseplate
        {
            Type = Enum.Parse<BaseplateType>(request.Type, ignoreCase: true),
            PartNum = request.PartNum,
            Name = request.Name,
            WidthStuds = request.WidthStuds,
            DepthStuds = request.DepthStuds,
            LegoColorId = request.LegoColorId,
            LinkedSetId = request.LinkedSetId,
        };
        var created = await _service.CreateAsync(model);
        var colors = await BuildColorLookupAsync();
        return StatusCode(StatusCodes.Status201Created, MapToResponse(created, colors));
    }

    [HttpGet("{id:guid}/image")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetImage(Guid id)
    {
        var bp = await _service.GetByIdAsync(id);
        if (bp is null) return NotFound();

        if (bp.ImageCached)
        {
            var img = await _imageService.GetImageAsync(id.ToString(), ImageReferenceType.Baseplate);
            if (img is not null) return File(img.Data, img.ContentType);
        }

        if (bp.Type == BaseplateType.Custom && bp.LinkedSetId.HasValue)
        {
            var photos = await _photoService.GetBySetIdAsync(bp.LinkedSetId.Value);
            var first = photos.FirstOrDefault();
            if (first is not null) return File(first.Data, first.ContentType);
        }

        return NotFound();
    }

    [HttpPost("{id:guid}/image")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [RequestSizeLimit(20_000_000)]
    public async Task<IActionResult> UploadImage(Guid id, IFormFile file)
    {
        var bp = await _service.GetByIdAsync(id);
        if (bp is null) return NotFound();
        if (file is null || file.Length == 0) return BadRequest(new { error = "No file provided." });

        await _imageService.StoreUploadAsync(id.ToString(), ImageReferenceType.Baseplate,
            file.OpenReadStream(), file.ContentType);
        await _service.UpdateImageCachedAsync(id);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _service.DeleteAsync(id);
        return NoContent();
    }

    private async Task<Dictionary<int, (string Name, string Rgb)>> BuildColorLookupAsync()
    {
        var colors = await _colorRepo.GetAllAsync();
        return colors.ToDictionary(c => c.Id, c => (c.Name, c.Rgb));
    }

    private static BaseplateResponse MapToResponse(Baseplate b, Dictionary<int, (string Name, string Rgb)> colors)
    {
        colors.TryGetValue(b.LegoColorId, out var color);
        return new(b.Id, b.Type.ToString(), b.PartNum, b.Name, b.WidthStuds, b.DepthStuds,
            b.LegoColorId, color.Name, color.Rgb, b.ImageCached, b.LinkedSetId, b.CreatedAt);
    }
}
