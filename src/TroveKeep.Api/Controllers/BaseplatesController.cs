using Microsoft.AspNetCore.Mvc;
using TroveKeep.Api.DTOs.Requests;
using TroveKeep.Api.DTOs.Responses;
using TroveKeep.Core.Interfaces.Services;
using TroveKeep.Core.Models;

namespace TroveKeep.Api.Controllers;

[ApiController]
[Route("api/baseplates")]
public class BaseplatesController : ControllerBase
{
    private readonly IBaseplateService _service;

    public BaseplatesController(IBaseplateService service)
    {
        _service = service;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<BaseplateResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        var baseplates = await _service.GetAllAsync();
        return Ok(baseplates.Select(MapToResponse));
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
        };
        var created = await _service.CreateAsync(model);
        return StatusCode(StatusCodes.Status201Created, MapToResponse(created));
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _service.DeleteAsync(id);
        return NoContent();
    }

    private static BaseplateResponse MapToResponse(Baseplate b) =>
        new(b.Id, b.PartNum, b.Name, b.WidthStuds, b.DepthStuds, b.CreatedAt);
}
