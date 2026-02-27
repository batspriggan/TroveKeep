using Microsoft.AspNetCore.Mvc;
using TroveKeep.Api.DTOs.Responses;
using TroveKeep.Core.Interfaces.Repositories;
using TroveKeep.Core.Interfaces.Services;
using TroveKeep.Core.Models;

namespace TroveKeep.Api.Controllers;

[ApiController]
[Route("api/search")]
public class SearchController : ControllerBase
{
    private readonly ISearchService _service;
    private readonly IColorRepository _colorRepo;

    public SearchController(ISearchService service, IColorRepository colorRepo)
    {
        _service = service;
        _colorRepo = colorRepo;
    }

    [HttpGet]
    [ProducesResponseType(typeof(SearchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Search([FromQuery] string? q)
    {
        if (string.IsNullOrWhiteSpace(q))
            return BadRequest(new { error = "Query parameter 'q' is required." });

        var resultTask = _service.SearchAsync(q.Trim());
        var colorsTask = _colorRepo.GetAllAsync();
        await Task.WhenAll(resultTask, colorsTask);

        var result = await resultTask;
        var colors = (await colorsTask).ToDictionary(c => c.Id, c => (c.Name, c.Rgb));

        return Ok(MapToResponse(result, colors));
    }

    private static SearchResponse MapToResponse(
        SearchResult r,
        Dictionary<int, (string Name, string Rgb)> colors) => new(
        r.Sets.Select(s => new SetSearchResponse(
            s.Id, s.SetNumber, s.Description, s.Quantity,
            s.Allocations.Select(MapAllocation))),
        r.Pieces.Select(p =>
        {
            colors.TryGetValue(p.LegoColorId, out var color);
            return new PieceSearchResponse(
                p.Id, p.LegoId,
                p.LegoColorId, color.Name, color.Rgb,
                p.Description, p.Quantity,
                p.Allocations.Select(MapAllocation));
        }));

    private static ResolvedAllocationResponse MapAllocation(ResolvedAllocation a) =>
        new(a.StorageId, a.StorageType.ToString(), a.StorageName,
            a.DrawerContainerId, a.DrawerContainerName, a.DrawerPosition, a.Quantity);
}
