using Microsoft.AspNetCore.Mvc;
using TroveKeep.Api.DTOs.Responses;
using TroveKeep.Core.Interfaces.Services;
using TroveKeep.Core.Models;

namespace TroveKeep.Api.Controllers;

[ApiController]
[Route("api/search")]
public class SearchController : ControllerBase
{
    private readonly ISearchService _service;

    public SearchController(ISearchService service)
    {
        _service = service;
    }

    [HttpGet]
    [ProducesResponseType(typeof(SearchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Search([FromQuery] string? q)
    {
        if (string.IsNullOrWhiteSpace(q))
            return BadRequest(new { error = "Query parameter 'q' is required." });

        var result = await _service.SearchAsync(q.Trim());
        return Ok(MapToResponse(result));
    }

    private static SearchResponse MapToResponse(SearchResult r) => new(
        r.Sets.Select(s => new SetSearchResponse(
            s.Id, s.SetNumber, s.Description, s.Quantity,
            s.Allocations.Select(MapAllocation))),
        r.Pieces.Select(p => new PieceSearchResponse(
            p.Id, p.LegoId, p.LegoColor, p.Description, p.Quantity,
            p.Allocations.Select(MapAllocation))));

    private static ResolvedAllocationResponse MapAllocation(ResolvedAllocation a) =>
        new(a.StorageId, a.StorageType.ToString(), a.StorageName,
            a.DrawerContainerId, a.DrawerContainerName, a.DrawerPosition, a.Quantity);
}
