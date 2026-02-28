using Microsoft.AspNetCore.Mvc;
using TroveKeep.Api.DTOs.Requests;
using TroveKeep.Api.DTOs.Responses;
using TroveKeep.Core.Interfaces.Services;
using TroveKeep.Core.Models;

namespace TroveKeep.Api.Controllers;

[ApiController]
[Route("api/rooms")]
public class RoomsController : ControllerBase
{
    private readonly IRoomService _service;

    public RoomsController(IRoomService service)
    {
        _service = service;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<RoomResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        var rooms = await _service.GetAllAsync();
        return Ok(rooms.Select(MapToResponse));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(RoomResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var room = await _service.GetByIdAsync(id);
        if (room is null) return NotFound();
        return Ok(MapToResponse(room));
    }

    [HttpPost]
    [ProducesResponseType(typeof(RoomResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateRoomRequest request)
    {
        var model = new Room
        {
            Name = request.Name,
            WidthCm = request.WidthCm,
            DepthCm = request.DepthCm,
        };
        var created = await _service.CreateAsync(model);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, MapToResponse(created));
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(RoomResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateRoomRequest request)
    {
        var model = new Room
        {
            Id = id,
            Name = request.Name,
            WidthCm = request.WidthCm,
            DepthCm = request.DepthCm,
        };
        var updated = await _service.UpdateAsync(model);
        if (updated is null) return NotFound();
        return Ok(MapToResponse(updated));
    }

    [HttpPut("{id:guid}/layout")]
    [ProducesResponseType(typeof(RoomResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SaveLayout(Guid id, [FromBody] SaveRoomLayoutRequest request)
    {
        var layout = request.Layout.Select(p => new PlacedTable
        {
            InstanceId = p.InstanceId,
            TemplateId = p.TemplateId,
            XCm = p.XCm,
            YCm = p.YCm,
        });
        var updated = await _service.SaveLayoutAsync(id, layout);
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
        return NoContent();
    }

    private static RoomResponse MapToResponse(Room r) =>
        new(r.Id, r.Name, r.WidthCm, r.DepthCm,
            r.Layout.Select(p => new PlacedTableResponse(p.InstanceId, p.TemplateId, p.XCm, p.YCm)),
            r.CreatedAt, r.UpdatedAt);
}
