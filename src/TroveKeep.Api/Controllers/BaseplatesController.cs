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

    public BaseplatesController(IBaseplateService service, IColorRepository colorRepo)
    {
        _service = service;
        _colorRepo = colorRepo;
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
            PartNum = request.PartNum,
            Name = request.Name,
            WidthStuds = request.WidthStuds,
            DepthStuds = request.DepthStuds,
            LegoColorId = request.LegoColorId,
        };
        var created = await _service.CreateAsync(model);
        var colors = await BuildColorLookupAsync();
        return StatusCode(StatusCodes.Status201Created, MapToResponse(created, colors));
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
        return new(b.Id, b.PartNum, b.Name, b.WidthStuds, b.DepthStuds,
            b.LegoColorId, color.Name, color.Rgb, b.CreatedAt);
    }
}
