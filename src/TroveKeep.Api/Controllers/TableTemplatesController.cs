using Microsoft.AspNetCore.Mvc;
using TroveKeep.Api.DTOs.Requests;
using TroveKeep.Api.DTOs.Responses;
using TroveKeep.Core.Exceptions;
using TroveKeep.Core.Interfaces.Services;
using TroveKeep.Core.Models;

namespace TroveKeep.Api.Controllers;

[ApiController]
[Route("api/table-templates")]
public class TableTemplatesController : ControllerBase
{
    private readonly ITableTemplateService _service;

    public TableTemplatesController(ITableTemplateService service)
    {
        _service = service;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<TableTemplateResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        var templates = await _service.GetAllAsync();
        return Ok(templates.Select(MapToResponse));
    }

    [HttpPost]
    [ProducesResponseType(typeof(TableTemplateResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateTableTemplateRequest request)
    {
        var model = new TableTemplate
        {
            Description = request.Description,
            WidthCm = request.WidthCm,
            DepthCm = request.DepthCm,
            Color = request.Color,
        };
        var created = await _service.CreateAsync(model);
        return StatusCode(StatusCodes.Status201Created, MapToResponse(created));
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(TableTemplateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTableTemplateRequest request)
    {
        try
        {
            var model = new TableTemplate
            {
                Id = id,
                Description = request.Description,
                WidthCm = request.WidthCm,
                DepthCm = request.DepthCm,
                Color = request.Color,
                Version = request.Version,
            };
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
        return NoContent();
    }

    private static TableTemplateResponse MapToResponse(TableTemplate t) =>
        new(t.Id, t.Description, t.WidthCm, t.DepthCm, t.Color, t.CreatedAt, t.UpdatedAt, t.Version);
}
