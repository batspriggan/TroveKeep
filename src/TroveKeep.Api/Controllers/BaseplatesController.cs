using Microsoft.AspNetCore.Mvc;
using TroveKeep.Api.DTOs.Requests;
using TroveKeep.Api.DTOs.Responses;
using TroveKeep.Core.Exceptions;
using TroveKeep.Core.Interfaces.Repositories;
using TroveKeep.Core.Interfaces.Services;
using TroveKeep.Core.Models;

namespace TroveKeep.Api.Controllers;

[ApiController]
[Route("api/baseplates")]
public class BaseplatesController : ControllerBase
{
    private readonly IBaseplateService _service;
    private readonly IPlanningService _planningService;
    private readonly IMocReconciliationService _reconciliationService;
    private readonly IColorRepository _colorRepo;
    private readonly IRoomRepository _roomRepo;
    private readonly ILegoSetRepository _setRepo;
    private readonly IImageService _imageService;
    private readonly ISetPhotoService _photoService;

    public BaseplatesController(IBaseplateService service, IPlanningService planningService,
        IMocReconciliationService reconciliationService,
        IColorRepository colorRepo, IRoomRepository roomRepo, ILegoSetRepository setRepo,
        IImageService imageService, ISetPhotoService photoService)
    {
        _service = service;
        _planningService = planningService;
        _reconciliationService = reconciliationService;
        _colorRepo = colorRepo;
        _roomRepo = roomRepo;
        _setRepo = setRepo;
        _imageService = imageService;
        _photoService = photoService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<BaseplateResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        var baseplates = (await _service.GetAllAsync()).ToList();
        var ctx = await BuildContextAsync(baseplates);
        return Ok(baseplates.Select(b => MapToResponse(b, ctx)));
    }

    [HttpPost]
    [ProducesResponseType(typeof(BaseplateResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateBaseplateRequest request,
        [FromQuery] bool imported = false)
    {
        try
        {
            var model = new Baseplate
            {
                Type = ParseType(request.Type),
                PartNum = request.PartNum,
                Name = request.Name,
                WidthStuds = request.WidthStuds,
                DepthStuds = request.DepthStuds,
                LegoColorId = request.LegoColorId,
                LinkedSetId = request.LinkedSetId,
                RoadShape = ParseRoadShape(request.RoadShape),
                Quantity = request.Quantity,
                Notes = request.Notes,
            };
            var created = await _service.CreateAsync(model, imported);
            var ctx = await BuildContextAsync([created]);
            return StatusCode(StatusCodes.Status201Created, MapToResponse(created, ctx));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(BaseplateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateBaseplateRequest request)
    {
        try
        {
            var model = new Baseplate
            {
                Type = ParseType(request.Type),
                PartNum = request.PartNum,
                Name = request.Name,
                WidthStuds = request.WidthStuds,
                DepthStuds = request.DepthStuds,
                LegoColorId = request.LegoColorId,
                LinkedSetId = request.LinkedSetId,
                RoadShape = ParseRoadShape(request.RoadShape),
                Quantity = request.Quantity ?? 1,
                Notes = request.Notes,
                Version = request.Version,
            };
            var updated = await _service.UpdateAsync(id, model);
            var ctx = await BuildContextAsync([updated]);
            return Ok(MapToResponse(updated, ctx));
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (ConcurrencyException ex) { return Conflict(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("{id:guid}/confirm")]
    [ProducesResponseType(typeof(BaseplateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Confirm(Guid id)
    {
        try
        {
            var bp = await _service.ConfirmAsync(id);
            var ctx = await BuildContextAsync([bp]);
            return Ok(MapToResponse(bp, ctx));
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (ConcurrencyException ex) { return Conflict(new { error = ex.Message }); }
    }

    [HttpPost("{id:guid}/reservations")]
    [ProducesResponseType(typeof(BaseplateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AddReservation(Guid id, [FromBody] CreateReservationRequest request)
    {
        try
        {
            var bp = await _service.AddReservationAsync(id, request.SetId, request.Quantity);
            var ctx = await BuildContextAsync([bp]);
            return Ok(MapToResponse(bp, ctx));
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpDelete("{id:guid}/reservations/{setId:guid}")]
    [ProducesResponseType(typeof(BaseplateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveReservation(Guid id, Guid setId)
    {
        try
        {
            var bp = await _service.RemoveReservationAsync(id, setId);
            var ctx = await BuildContextAsync([bp]);
            return Ok(MapToResponse(bp, ctx));
        }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPost("feasibility")]
    [ProducesResponseType(typeof(FeasibilityResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Feasibility([FromBody] FeasibilityRequest request)
    {
        try
        {
            var refs = (request.Aggregates ?? []).Select(a => new FeasibilityAggregateRef
            {
                RoomId = a.RoomId,
                RepresentativeId = a.RepresentativeId,
            });
            var result = await _planningService.CalculateFeasibilityAsync(refs);
            var response = new FeasibilityResponse(
                result.Lines.Select(l => new FeasibilityLine(l.BaseplateId, l.Name, l.Type,
                    l.Need, l.Quantity, l.Reserved, l.Available, l.Deficit, l.Status)),
                result.TotalDeficit,
                result.HasShortage);
            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Dissolves a legacy MOC row into real plates of <see cref="ReconcileRequest.TargetBaseplateId"/>.
    /// When cols/rows are omitted they are derived from the two footprints (either orientation).
    /// </summary>
    [HttpPost("{id:guid}/reconcile")]
    [ProducesResponseType(typeof(DissolveResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Reconcile(Guid id, [FromBody] ReconcileRequest request)
    {
        try
        {
            var cols = request.Cols ?? 0;
            var rows = request.Rows ?? 0;

            if (cols < 1 || rows < 1)
            {
                var moc = await _service.GetByIdAsync(id);
                if (moc is null) return NotFound();

                var target = await _service.GetByIdAsync(request.TargetBaseplateId);
                if (target is null) return NotFound();

                if (!TryResolveArrangement(moc, target, out cols, out rows))
                    return BadRequest(new { error = "The selected target does not tile the MOC footprint." });
            }

            var result = await _reconciliationService.DissolveAsync(id, request.TargetBaseplateId, cols, rows);
            return Ok(new DissolveResponse(result.Dissolved, result.PlatesCreated,
                result.PlacementsRewritten, result.Reason));
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPost("{id:guid}/unquarantine")]
    [ProducesResponseType(typeof(BaseplateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Unquarantine(Guid id, [FromBody] UnquarantineRequest request)
    {
        try
        {
            var bp = await _service.UnquarantineAsync(id, request.ConfirmAsPhysicalPlate);
            var ctx = await BuildContextAsync([bp]);
            return Ok(MapToResponse(bp, ctx));
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (ConcurrencyException ex) { return Conflict(new { error = ex.Message }); }
    }

    /// <summary>All baseplate reservations held by a set/MOC, denormalised with the plate data.</summary>
    [HttpGet("by-set/{setId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<SetReservationResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBySet(Guid setId)
    {
        var baseplates = (await _service.GetAllAsync()).ToList();
        var colors = await BuildColorLookupAsync();

        var response = baseplates
            .SelectMany(bp => bp.Reservations
                .Where(r => r.SetId == setId)
                .Select(r =>
                {
                    colors.TryGetValue(bp.LegoColorId, out var color);
                    return new SetReservationResponse(
                        bp.Id, bp.Name, bp.Type.ToString(), bp.WidthStuds, bp.DepthStuds,
                        bp.LegoColorId, color.Name, color.Rgb, r.Quantity, null, r.CreatedAt);
                }))
            .OrderBy(r => r.PlateName)
            .ThenBy(r => r.BaseplateId)
            .ToList();

        return Ok(response);
    }

    /// <summary>
    /// Sets/MOCs that own at least one baseplate reservation, with the compact
    /// <c>cols×rows</c> arrangement derived from the reserved plate count.
    /// </summary>
    [HttpGet("planner-entities")]
    [ProducesResponseType(typeof(IEnumerable<PlannerEntityResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPlannerEntities()
    {
        var baseplates = (await _service.GetAllAsync()).ToList();

        var reservations = baseplates
            .SelectMany(bp => bp.Reservations.Select(r => (Baseplate: bp, Reservation: r)))
            .ToList();

        var groups = reservations.GroupBy(x => x.Reservation.SetId).ToList();
        if (groups.Count == 0) return Ok(Array.Empty<PlannerEntityResponse>());

        var sets = (await _setRepo.GetByIdsAsync(groups.Select(g => g.Key)))
            .ToDictionary(s => s.Id);

        var response = new List<PlannerEntityResponse>();

        foreach (var group in groups)
        {
            var totalPlates = group.Sum(x => x.Reservation.Quantity);
            if (totalPlates <= 0) continue;

            // "Most reserved" module type; deterministic tie-break on the baseplate id.
            var module = group
                .GroupBy(x => x.Baseplate.Id)
                .Select(g => new { Baseplate = g.First().Baseplate, Quantity = g.Sum(x => x.Reservation.Quantity) })
                .OrderByDescending(x => x.Quantity)
                .ThenBy(x => x.Baseplate.Id)
                .First()
                .Baseplate;

            var cols = (int)Math.Ceiling(Math.Sqrt(totalPlates));
            var rows = (int)Math.Ceiling((double)totalPlates / cols);

            sets.TryGetValue(group.Key, out var set);
            var name = set is null
                ? module.Name
                : (string.IsNullOrWhiteSpace(set.Description) ? set.SetNumber : set.Description);

            response.Add(new PlannerEntityResponse(
                group.Key,
                name,
                set?.IsMoc ?? false,
                module.Id,
                module.WidthStuds,
                module.DepthStuds,
                totalPlates,
                cols,
                rows,
                cols * module.WidthStuds,
                rows * module.DepthStuds,
                false)); // phase 1: no per-set quarantine propagation
        }

        return Ok(response.OrderBy(r => r.Name).ThenBy(r => r.SetId).ToList());
    }

    [HttpGet("{id:guid}/image")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetImage(Guid id)
    {
        var bp = await _service.GetByIdAsync(id);
        if (bp is null) return NotFound();

        if (bp.ImageCached)
        {
            var img = await _imageService.GetImageAsync(id.ToString(), ImageReferenceType.Baseplate);
            if (img is not null) return File(img.Data, img.ContentType);
        }

        if (bp.Type == BaseplateType.Custom && bp.LinkedSetId.HasValue)
        {
            var photos = await _photoService.GetBySetIdAsync(bp.LinkedSetId.Value);
            var first = photos.FirstOrDefault();
            if (first is not null) return File(first.Data, first.ContentType);
        }

        return NotFound();
    }

    [HttpPost("{id:guid}/image")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [RequestSizeLimit(20_000_000)]
    public async Task<IActionResult> UploadImage(Guid id, IFormFile file)
    {
        var bp = await _service.GetByIdAsync(id);
        if (bp is null) return NotFound();
        if (file is null || file.Length == 0) return BadRequest(new { error = "No file provided." });

        await _imageService.StoreUploadAsync(id.ToString(), ImageReferenceType.Baseplate,
            file.OpenReadStream(), file.ContentType);
        await _service.UpdateImageCachedAsync(id, true);
        return NoContent();
    }

    [HttpDelete("{id:guid}/image")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteImage(Guid id)
    {
        var bp = await _service.GetByIdAsync(id);
        if (bp is null) return NotFound();

        await _imageService.DeleteAsync(id.ToString(), ImageReferenceType.Baseplate);
        await _service.UpdateImageCachedAsync(id, false);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _service.DeleteAsync(id);
        return NoContent();
    }

    private static bool TryResolveArrangement(Baseplate moc, Baseplate target, out int cols, out int rows)
    {
        cols = 0;
        rows = 0;

        if (target.WidthStuds <= 0 || target.DepthStuds <= 0) return false;

        if (moc.WidthStuds % target.WidthStuds == 0 && moc.DepthStuds % target.DepthStuds == 0)
        {
            cols = moc.WidthStuds / target.WidthStuds;
            rows = moc.DepthStuds / target.DepthStuds;
            return cols > 0 && rows > 0;
        }

        if (moc.WidthStuds % target.DepthStuds == 0 && moc.DepthStuds % target.WidthStuds == 0)
        {
            cols = moc.WidthStuds / target.DepthStuds;
            rows = moc.DepthStuds / target.WidthStuds;
            return cols > 0 && rows > 0;
        }

        return false;
    }

    private static BaseplateType ParseType(string type)
    {
        if (!Enum.TryParse<BaseplateType>(type, ignoreCase: true, out var parsed))
            throw new InvalidOperationException($"Unknown baseplate type '{type}'.");
        return parsed;
    }

    private static RoadShape? ParseRoadShape(string? roadShape)
    {
        if (string.IsNullOrWhiteSpace(roadShape)) return null;
        if (!Enum.TryParse<RoadShape>(roadShape, ignoreCase: true, out var parsed))
            throw new InvalidOperationException($"Unknown road shape '{roadShape}'.");
        return parsed;
    }

    private sealed record ResponseContext(
        Dictionary<int, (string Name, string Rgb)> Colors,
        Dictionary<Guid, string> SetDescriptions,
        Dictionary<Guid, int> InLayoutCounts);

    private async Task<ResponseContext> BuildContextAsync(IEnumerable<Baseplate> baseplates)
    {
        var colors = await BuildColorLookupAsync();

        var setIds = baseplates
            .SelectMany(b => b.Reservations.Select(r => r.SetId))
            .Distinct()
            .ToList();
        var setDescriptions = new Dictionary<Guid, string>();
        if (setIds.Count > 0)
        {
            var sets = await _setRepo.GetByIdsAsync(setIds);
            foreach (var set in sets)
                setDescriptions[set.Id] = string.IsNullOrWhiteSpace(set.Description)
                    ? set.SetNumber
                    : set.Description;
        }

        var rooms = await _roomRepo.GetAllAsync();
        var inLayoutCounts = new Dictionary<Guid, int>();
        foreach (var room in rooms)
            foreach (var layout in room.AggregateBpLayouts)
                foreach (var placed in layout.PlacedBaseplates)
                    inLayoutCounts[placed.BaseplateId] = inLayoutCounts.GetValueOrDefault(placed.BaseplateId) + 1;

        return new ResponseContext(colors, setDescriptions, inLayoutCounts);
    }

    private static BaseplateResponse MapToResponse(Baseplate b, ResponseContext ctx)
    {
        ctx.Colors.TryGetValue(b.LegoColorId, out var color);

        var reserved = b.Reservations.Sum(r => r.Quantity);
        var available = Math.Max(0, b.Quantity - reserved);
        var overReserved = reserved > b.Quantity;

        var reservations = b.Reservations
            .OrderBy(r => r.CreatedAt)
            .Select(r => new ReservationResponse(r.SetId, ctx.SetDescriptions.GetValueOrDefault(r.SetId), r.Quantity))
            .ToList();

        return new BaseplateResponse(
            b.Id, b.Type.ToString(), b.PartNum, b.Name, b.WidthStuds, b.DepthStuds,
            b.LegoColorId, color.Name, color.Rgb, b.ImageCached, b.LinkedSetId,
            b.RoadShape?.ToString(),
            b.Quantity, reserved, available, ctx.InLayoutCounts.GetValueOrDefault(b.Id),
            b.NeedsReview, b.Notes, reservations,
            b.CreatedAt, b.UpdatedAt, b.Version,
            b.Quarantined, b.QuarantineReason, overReserved);
    }

    private async Task<Dictionary<int, (string Name, string Rgb)>> BuildColorLookupAsync()
    {
        var colors = await _colorRepo.GetAllAsync();
        return colors.ToDictionary(c => c.Id, c => (c.Name, c.Rgb));
    }
}
