using Microsoft.AspNetCore.Mvc;
using TroveKeep.Api.DTOs.Responses;
using TroveKeep.Core.Interfaces.Repositories;
using TroveKeep.Core.Interfaces.Services;
using TroveKeep.Core.Models;

namespace TroveKeep.Api.Controllers;

[ApiController]
[Route("api/scanner")]
public class ScannerController : ControllerBase
{
    private readonly IScannerService _scannerService;
    private readonly IColorRepository _colorRepo;

    public ScannerController(IScannerService scannerService, IColorRepository colorRepo)
    {
        _scannerService = scannerService;
        _colorRepo = colorRepo;
    }

    [HttpGet("resolve")]
    [ProducesResponseType(typeof(ScannerResolveResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Resolve([FromQuery] string? code)
    {
        var reference = LabelCodes.TryParse(code);
        if (reference is null)
            return BadRequest(new { error = "Invalid or unsupported label code." });

        var result = await _scannerService.ResolveAsync(reference);
        if (result is null) return NotFound();

        var colors = await _colorRepo.GetAllAsync();
        var color = result.ColorId is { } cid
            ? colors.FirstOrDefault(c => c.Id == cid)
            : null;

        return Ok(new ScannerResolveResponse(
            result.Kind.ToString(),
            result.Id,
            result.Title,
            result.Subtitle,
            color?.Name,
            color?.Rgb,
            result.Quantity,
            result.TargetStorageType?.ToString(),
            result.TargetStorageId,
            result.TargetStoragePosition,
            result.Allocations.Select(a => new ScannerAllocationResponse(
                a.StorageType.ToString(),
                a.StorageId,
                a.StorageName,
                a.DrawerContainerId,
                a.DrawerContainerName,
                a.DrawerPosition,
                a.Quantity))));
    }
}
