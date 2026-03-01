using Microsoft.AspNetCore.Mvc;
using TroveKeep.Api.DTOs.Responses;
using TroveKeep.Core.Interfaces.Services;
using TroveKeep.Core.Models;

namespace TroveKeep.Api.Controllers;

[ApiController]
[Route("api/archives")]
public class ArchivesController : ControllerBase
{
    private readonly IArchiveService _service;

    public ArchivesController(IArchiveService service)
    {
        _service = service;
    }

    [HttpGet("colors")]
    [ProducesResponseType(typeof(ArchiveStatusResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetColorsStatus()
    {
        var (count, importedAt) = await _service.GetColorsStatusAsync();
        return Ok(new ArchiveStatusResponse(count, importedAt));
    }

    [HttpPost("colors/reload")]
    [RequestSizeLimit(50_000_000)]
    [RequestFormLimits(MultipartBodyLengthLimit = 50_000_000)]
    [ProducesResponseType(typeof(ArchiveStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ReloadColors(IFormFile file)
    {
        try
        {
            var (count, importedAt) = await _service.ImportColorsAsync(file.OpenReadStream());
            return Ok(new ArchiveStatusResponse(count, importedAt));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("colors/list")]
    [ProducesResponseType(typeof(IEnumerable<ColorResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetColorsList()
    {
        var colors = await _service.GetColorsAsync();
        return Ok(colors.Select(MapToResponse));
    }

    [HttpGet("sets")]
    [ProducesResponseType(typeof(ArchiveStatusResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSetsStatus()
    {
        var (count, importedAt) = await _service.GetSetsStatusAsync();
        return Ok(new ArchiveStatusResponse(count, importedAt));
    }

    [HttpGet("sets/search")]
    [ProducesResponseType(typeof(IEnumerable<SetArchiveSearchResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SearchSets([FromQuery] string? q, [FromQuery] int limit = 10)
    {
        if (string.IsNullOrWhiteSpace(q))
            return Ok(Array.Empty<SetArchiveSearchResponse>());

        var cap = Math.Min(limit, 20);
        var results = await _service.SearchSetsAsync(q, cap);
        return Ok(results.Select(s => new SetArchiveSearchResponse(s.SetNum, s.Name, s.Year, s.NumParts, s.ImgUrl)));
    }

    [HttpPost("sets/reload")]
    [RequestSizeLimit(50_000_000)]
    [RequestFormLimits(MultipartBodyLengthLimit = 50_000_000)]
    [ProducesResponseType(typeof(ArchiveStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ReloadSets(IFormFile file)
    {
        try
        {
            var (count, importedAt) = await _service.ImportSetsAsync(file.OpenReadStream());
            return Ok(new ArchiveStatusResponse(count, importedAt));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("parts")]
    [ProducesResponseType(typeof(ArchiveStatusResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPartsStatus()
    {
        var (count, importedAt) = await _service.GetPartsStatusAsync();
        return Ok(new ArchiveStatusResponse(count, importedAt));
    }

    [HttpGet("parts/search")]
    [ProducesResponseType(typeof(IEnumerable<PartArchiveSearchResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SearchParts(
        [FromQuery] string? q,
        [FromQuery] int limit = 10,
        [FromQuery] int? categoryId = null)
    {
        if (string.IsNullOrWhiteSpace(q))
            return Ok(Array.Empty<PartArchiveSearchResponse>());

        var cap = Math.Max(0, limit); // 0 = no limit (MongoDB semantics)
        var results = await _service.SearchPartsAsync(q, cap, categoryId);
        return Ok(results.Select(p => new PartArchiveSearchResponse(p.PartNum, p.Name)));
    }

    [HttpPost("parts/reload")]
    [RequestSizeLimit(50_000_000)]
    [RequestFormLimits(MultipartBodyLengthLimit = 50_000_000)]
    [ProducesResponseType(typeof(ArchiveStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ReloadParts(IFormFile file)
    {
        try
        {
            var (count, importedAt) = await _service.ImportPartsAsync(file.OpenReadStream());
            return Ok(new ArchiveStatusResponse(count, importedAt));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("partsinventory")]
    [ProducesResponseType(typeof(ArchiveStatusResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPartsInventoryStatus()
    {
        var (count, importedAt) = await _service.GetPartsInventoryStatusAsync();
        return Ok(new ArchiveStatusResponse(count, importedAt));
    }

    [HttpGet("partsinventory/search")]
    [ProducesResponseType(typeof(IEnumerable<PartArchiveSearchResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SearchPartsInventory([FromQuery] string? q, [FromQuery] int limit = 10)
    {
        if (string.IsNullOrWhiteSpace(q))
            return Ok(Array.Empty<PartArchiveSearchResponse>());

        var cap = Math.Min(limit, 20);
        var results = await _service.SearchPartsAsync(q, cap);
        return Ok(results.Select(p => new PartArchiveSearchResponse(p.PartNum, p.Name)));
    }

    [HttpPost("partsinventory/reload")]
    [RequestSizeLimit(50_000_000)]
    [RequestFormLimits(MultipartBodyLengthLimit = 50_000_000)]
    [ProducesResponseType(typeof(ArchiveStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ReloadPartsInventory(IFormFile file)
    {
        try
        {
            var (count, importedAt) = await _service.ImportPartsInventoryAsync(file.OpenReadStream());
            return Ok(new ArchiveStatusResponse(count, importedAt));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("partcategories")]
    [ProducesResponseType(typeof(ArchiveStatusResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPartCategoriesStatus()
    {
        var (count, importedAt) = await _service.GetPartCategoriesStatusAsync();
        return Ok(new ArchiveStatusResponse(count, importedAt));
    }

    [HttpPost("partcategories/reload")]
    [RequestSizeLimit(50_000_000)]
    [RequestFormLimits(MultipartBodyLengthLimit = 50_000_000)]
    [ProducesResponseType(typeof(ArchiveStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ReloadPartCategories(IFormFile file)
    {
        try
        {
            var (count, importedAt) = await _service.ImportPartCategoriesAsync(file.OpenReadStream());
            return Ok(new ArchiveStatusResponse(count, importedAt));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("partcategories/list")]
    [ProducesResponseType(typeof(IEnumerable<PartCategoryResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPartCategoriesList()
    {
        var categories = await _service.GetPartCategoriesAsync();
        return Ok(categories.Select(c => new PartCategoryResponse(c.Id, c.Name)));
    }

    private static ColorResponse MapToResponse(RebrickableColor c) =>
        new(c.UniqueId, c.Id, c.Name, c.Rgb, c.IsTrans, c.StartYear, c.EndYear);
}
