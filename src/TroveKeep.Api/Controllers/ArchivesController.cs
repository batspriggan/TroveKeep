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
    [ProducesResponseType(typeof(ArchiveStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReloadColors()
    {
        try
        {
            var (count, importedAt) = await _service.ImportColorsAsync();
            return Ok(new ArchiveStatusResponse(count, importedAt));
        }
        catch (FileNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpGet("colors/list")]
    [ProducesResponseType(typeof(IEnumerable<ColorResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetColorsList()
    {
        var colors = await _service.GetColorsAsync();
        return Ok(colors.Select(MapToResponse));
    }

    private static ColorResponse MapToResponse(RebrickableColor c) =>
        new(c.Id, c.Name, c.Rgb, c.IsTrans, c.StartYear, c.EndYear);
}
