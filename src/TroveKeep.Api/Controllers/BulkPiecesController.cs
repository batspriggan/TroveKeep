using System.IO.Compression;
using Microsoft.AspNetCore.Mvc;
using TroveKeep.Api.DTOs.Requests;
using TroveKeep.Api.DTOs.Responses;
using TroveKeep.Core.Exceptions;
using TroveKeep.Core.Interfaces.Repositories;
using TroveKeep.Core.Interfaces.Services;
using TroveKeep.Core.Models;

namespace TroveKeep.Api.Controllers;

[ApiController]
[Route("api/bulkpieces")]
public class BulkPiecesController : ControllerBase
{
    private readonly IBulkPieceService _service;
    private readonly IColorRepository _colorRepo;
    private readonly IImageService _imageService;
    private readonly ILabelPrintService _labelPrintService;
    private readonly IBoxRepository _boxRepo;
    private readonly IDrawerContainerRepository _drawerContainerRepo;

    public BulkPiecesController(IBulkPieceService service, IColorRepository colorRepo, IImageService imageService, ILabelPrintService labelPrintService, IBoxRepository boxRepo, IDrawerContainerRepository drawerContainerRepo)
    {
        _service = service;
        _colorRepo = colorRepo;
        _imageService = imageService;
        _labelPrintService = labelPrintService;
        _boxRepo = boxRepo;
        _drawerContainerRepo = drawerContainerRepo;
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<BulkPieceResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int size = 50,
        [FromQuery] string? q = null)
    {
        var (pieces, total) = await _service.GetPageAsync(page, size, q);
        var colors = await BuildColorLookupAsync();
        var items = pieces.Select(p => MapToResponse(p, colors));
        var totalPages = (int)Math.Ceiling((double)total / size);
        return Ok(new PagedResponse<BulkPieceResponse>(items, total, page, size, totalPages));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(BulkPieceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var piece = await _service.GetByIdAsync(id);
        if (piece is null) return NotFound();
        var colors = await BuildColorLookupAsync();
        return Ok(MapToResponse(piece, colors));
    }

    [HttpPost]
    [ProducesResponseType(typeof(BulkPieceResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateBulkPieceRequest request)
    {
        var model = new BulkPiece
        {
            LegoId = request.LegoId,
            LegoColorId = request.LegoColorId,
            Description = request.Description,
            Quantity = request.Quantity,
        };
        var created = await _service.CreateAsync(model);
        _ = QueuePartImageAsync(created);
        var colors = await BuildColorLookupAsync();
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, MapToResponse(created, colors));
    }

    private const string RebrickableLdrawBase = "https://cdn.rebrickable.com/media/parts/ldraw";

    /// <summary>
    /// Downloads a part image addressed by (partNum, colorId) via the Rebrickable LDRAW render,
    /// falling back to the color-0 render when the exact color has no image. Returns whether an
    /// image was stored (and ImageCached set to true). Fire-and-forget from Create, awaited from
    /// the lazy GET /image re-index.
    /// </summary>
    private async Task<bool> QueuePartImageAsync(BulkPiece piece)
    {
        var url = $"{RebrickableLdrawBase}/{piece.LegoColorId}/{piece.LegoId}.png";
        var ok = await _imageService.DownloadAndStoreAsync(piece.Id, piece.LegoId, url, ImageReferenceType.Part, piece.LegoColorId);
        if (!ok && piece.LegoColorId != 0)
        {
            var url0 = $"{RebrickableLdrawBase}/0/{piece.LegoId}.png";
            ok = await _imageService.DownloadAndStoreAsync(piece.Id, piece.LegoId, url0, ImageReferenceType.Part, piece.LegoColorId);
        }
        return ok;
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(BulkPieceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateBulkPieceRequest request)
    {
        try
        {
            var model = new BulkPiece
            {
                Id = id,
                LegoId = request.LegoId,
                LegoColorId = request.LegoColorId,
                Description = request.Description,
                Quantity = request.Quantity,
                Version = request.Version,
            };
            var updated = await _service.UpdateAsync(model);
            if (updated is null) return NotFound();
            var colors = await BuildColorLookupAsync();
            return Ok(MapToResponse(updated, colors));
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

    [HttpPost("{id:guid}/storage/box/{boxId:guid}")]
    [ProducesResponseType(typeof(BulkPieceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AllocateToBox(Guid id, Guid boxId, [FromBody] AllocateStorageRequest request)
    {
        try
        {
            var updated = await _service.AllocateToBoxAsync(id, boxId, request.Quantity);
            if (updated is null) return NotFound();
            var colors = await BuildColorLookupAsync();
            return Ok(MapToResponse(updated, colors));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id:guid}/storage/drawer/{containerId:guid}/{position:int}")]
    [ProducesResponseType(typeof(BulkPieceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AllocateToDrawer(Guid id, Guid containerId, int position, [FromBody] AllocateStorageRequest request)
    {
        try
        {
            var updated = await _service.AllocateToDrawerAsync(id, containerId, position, request.Quantity);
            if (updated is null) return NotFound();
            var colors = await BuildColorLookupAsync();
            return Ok(MapToResponse(updated, colors));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{id:guid}/storage/drawer/{containerId:guid}/{position:int}")]
    [ProducesResponseType(typeof(BulkPieceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetDrawerQuantity(Guid id, Guid containerId, int position, [FromBody] AllocateStorageRequest request)
    {
        try
        {
            var updated = await _service.SetDrawerQuantityAsync(id, containerId, position, request.Quantity);
            if (updated is null) return NotFound();
            var colors = await BuildColorLookupAsync();
            return Ok(MapToResponse(updated, colors));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("{id:guid}/storage/box/{boxId:guid}")]
    [ProducesResponseType(typeof(BulkPieceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeallocateFromBox(Guid id, Guid boxId)
    {
        var updated = await _service.DeallocateFromBoxAsync(id, boxId);
        if (updated is null) return NotFound();
        var colors = await BuildColorLookupAsync();
        return Ok(MapToResponse(updated, colors));
    }

    [HttpDelete("{id:guid}/storage/drawer/{containerId:guid}/{position:int}")]
    [ProducesResponseType(typeof(BulkPieceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeallocateFromDrawer(Guid id, Guid containerId, int position)
    {
        var updated = await _service.DeallocateFromDrawerAsync(id, containerId, position);
        if (updated is null) return NotFound();
        var colors = await BuildColorLookupAsync();
        return Ok(MapToResponse(updated, colors));
    }

    [HttpDelete("{id:guid}/storage")]
    [ProducesResponseType(typeof(BulkPieceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ClearStorage(Guid id)
    {
        var updated = await _service.ClearStorageAsync(id);
        if (updated is null) return NotFound();
        var colors = await BuildColorLookupAsync();
        return Ok(MapToResponse(updated, colors));
    }

    [HttpGet("{id:guid}/image")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetImage(Guid id)
    {
        var piece = await _service.GetByIdAsync(id);
        if (piece is null) return NotFound();
        var image = await _imageService.GetImageAsync(piece.LegoId, ImageReferenceType.Part, piece.LegoColorId);
        if (image is null)
        {
            // Lazy re-index: the piece has no per-color image yet (e.g. reset by migration 003,
            // or the initial fire-and-forget failed). Fetch it now and store it.
            var stored = await QueuePartImageAsync(piece);
            image = stored
                ? await _imageService.GetImageAsync(piece.LegoId, ImageReferenceType.Part, piece.LegoColorId)
                : null;
        }
        if (image is null) return NotFound();
        return File(image.Data, image.ContentType);
    }

    [HttpGet("{id:guid}/label-file")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLabelFile(Guid id, [FromQuery] int? copies = null)
    {
        var piece = await _service.GetByIdAsync(id);
        if (piece is null) return NotFound();

        var allocations = piece.StorageAllocations ?? [];
        var color = (await _colorRepo.GetAllAsync()).FirstOrDefault(c => c.Id == piece.LegoColorId);
        var colorName = color?.Name;

        var boxIds = allocations.Where(a => a.StorageType == StorageType.Box).Select(a => a.StorageId).Distinct().ToList();
        var containerIds = allocations.Where(a => a.StorageType == StorageType.Drawer).Select(a => a.StorageId).Distinct().ToList();
        var boxes = boxIds.Count > 0
            ? (await _boxRepo.GetByIdsAsync(boxIds)).ToDictionary(b => b.Id)
            : new Dictionary<Guid, Box>();
        var containers = containerIds.Count > 0
            ? (await _drawerContainerRepo.GetByIdsAsync(containerIds)).ToDictionary(c => c.Id)
            : new Dictionary<Guid, DrawerContainer>();

        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            var index = 0;
            foreach (var a in allocations)
            {
                index++;
                string locationLine;
                if (a.StorageType == StorageType.Box)
                {
                    locationLine = boxes.GetValueOrDefault(a.StorageId)?.Name ?? "(unknown box)";
                }
                else
                {
                    var container = containers.GetValueOrDefault(a.StorageId);
                    locationLine = $"{container?.Name ?? "(unknown container)"} - {a.StoragePosition}";
                }

                var entry = zip.CreateEntry(_labelPrintService.GetBulkPieceLocationFileName(piece, index), CompressionLevel.Optimal);
                await using var writer = new StreamWriter(entry.Open());
                await writer.WriteAsync(_labelPrintService.BuildBulkPieceLocationLabel(piece, colorName, locationLine, copies));
            }
        }

        return File(ms.ToArray(), "application/zip", $"piece-{piece.LegoId}-{piece.LegoColorId}-labels.zip");
    }

    private async Task<Dictionary<int, (string Name, string Rgb)>> BuildColorLookupAsync()
    {
        var colors = await _colorRepo.GetAllAsync();
        return colors.ToDictionary(c => c.Id, c => (c.Name, c.Rgb));
    }

    private static BulkPieceResponse MapToResponse(
        BulkPiece p,
        Dictionary<int, (string Name, string Rgb)> colors)
    {
        colors.TryGetValue(p.LegoColorId, out var color);
        return new BulkPieceResponse(
            p.Id, p.LegoId,
            p.LegoColorId, color.Name, color.Rgb,
            p.Description, p.Quantity, p.ImageCached,
            p.StorageAllocations.Select(a => new StorageAllocationResponse(a.StorageId, a.StoragePosition, a.StorageType.ToString(), a.Quantity)),
            p.CreatedAt, p.UpdatedAt, p.Version);
    }
}
