using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TroveKeep.Api.DTOs.Requests;
using TroveKeep.Api.DTOs.Responses;
using TroveKeep.Api.Filters;
using TroveKeep.Core.Exceptions;
using TroveKeep.Core.Interfaces.Repositories;
using TroveKeep.Core.Interfaces.Services;
using TroveKeep.Core.Models;
using TroveKeep.Services;

namespace TroveKeep.Api.Controllers;

/// <summary>
/// Sticker projects: named print jobs whose embedded items pick artwork from the global
/// <c>/api/sticker-images</c> library and give it a tile footprint, a copy count and a fit.
///
/// <para>
/// <b>Reads</b> are open to every regular account. <b>Writes</b> are authorized by
/// <see cref="CollectionWriteAnywhereFilter"/> (admin, or Editor/Owner of at least one collection):
/// a project carries no <c>CollectionId</c>, so there is no target collection whose level could
/// authorize the write.
/// </para>
/// </summary>
[Authorize(Roles = "User,Admin")]
[ApiController]
[Route("api/sticker-projects")]
public class StickerProjectsController : ControllerBase
{
    private readonly IStickerProjectService _service;
    private readonly IStickerImageRepository _images;

    public StickerProjectsController(IStickerProjectService service, IStickerImageRepository images)
    {
        _service = service;
        _images = images;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<StickerProjectResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        var projects = (await _service.GetAllAsync()).ToList();
        var lookup = await LoadImagesAsync();
        return Ok(projects.Select(p => MapToResponse(p, lookup)));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(StickerProjectResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var project = await _service.GetByIdAsync(id);
        if (project is null) return NotFound();
        return Ok(MapToResponse(project, await LoadImagesAsync()));
    }

    [HttpPost]
    [ServiceFilter(typeof(CollectionWriteAnywhereFilter))]
    [ProducesResponseType(typeof(StickerProjectResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create([FromBody] CreateStickerProjectRequest request)
    {
        try
        {
            var created = await _service.CreateAsync(new StickerProject
            {
                Name = request.Name,
                Items = ToModels(request.Items),
            });
            return StatusCode(StatusCodes.Status201Created,
                MapToResponse(created, await LoadImagesAsync()));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    [ServiceFilter(typeof(CollectionWriteAnywhereFilter))]
    [ProducesResponseType(typeof(StickerProjectResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateStickerProjectRequest request)
    {
        try
        {
            var updated = await _service.UpdateAsync(id, new StickerProject
            {
                Name = request.Name,
                Items = ToModels(request.Items),
                Version = request.Version,
            });
            return Ok(MapToResponse(updated, await LoadImagesAsync()));
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (ConcurrencyException ex) { return Conflict(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpDelete("{id:guid}")]
    [ServiceFilter(typeof(CollectionWriteAnywhereFilter))]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var deleted = await _service.DeleteAsync(id);
        if (!deleted) return NotFound();
        return NoContent();
    }

    /// <summary>The artwork currently in the library, keyed by id, for denormalising item responses.</summary>
    private async Task<IReadOnlyDictionary<Guid, StickerImage>> LoadImagesAsync() =>
        (await _images.GetAllAsync()).ToDictionary(i => i.Id);

    /// <summary>Maps the request items onto domain items, parsing the fit name (400 on garbage).</summary>
    private static List<StickerItem> ToModels(List<StickerItemRequest>? items) =>
        (items ?? []).Select(i => new StickerItem
        {
            Id = i.Id ?? Guid.Empty,
            ImageId = i.ImageId,
            WidthStuds = i.WidthStuds,
            DepthStuds = i.DepthStuds,
            Copies = i.Copies,
            Fit = StickerRules.ParseFit(i.Fit),
            Notes = i.Notes,
        }).ToList();

    private static StickerProjectResponse MapToResponse(
        StickerProject project, IReadOnlyDictionary<Guid, StickerImage> images) =>
        new(project.Id,
            project.Name,
            project.Items.Select(i => MapItem(i, images)).ToList(),
            project.CreatedAt,
            project.UpdatedAt,
            project.Version);

    private static StickerItemResponse MapItem(
        StickerItem item, IReadOnlyDictionary<Guid, StickerImage> images)
    {
        images.TryGetValue(item.ImageId, out var image);
        return new StickerItemResponse(
            item.Id,
            item.ImageId,
            image?.Name,
            image?.ImageCached ?? false,
            item.WidthStuds,
            item.DepthStuds,
            item.WidthStuds * StickerItem.StudMm,
            item.DepthStuds * StickerItem.StudMm,
            item.Copies,
            item.Fit.ToString(),
            item.Notes);
    }
}
