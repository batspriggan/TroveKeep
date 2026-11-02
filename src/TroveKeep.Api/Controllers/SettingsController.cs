using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TroveKeep.Core.Interfaces.Services;
using TroveKeep.Core.Models;

namespace TroveKeep.Api.Controllers;

// Guests are authenticated but confined to the Table Planner: everything else requires User or Admin.
[Authorize(Roles = "User,Admin")]
[ApiController]
[Route("api/settings")]
public class SettingsController : ControllerBase
{
    private readonly IBackupService _backupService;

    public SettingsController(IBackupService backupService)
    {
        _backupService = backupService;
    }

    [HttpGet("backup")]
    public async Task<IActionResult> Backup()
    {
        var (data, fileName) = await _backupService.ExportAsync();
        return File(data, "application/gzip", fileName);
    }

    [HttpPost("restore")]
    // A restore replaces every collection's data at once: there is no collection scope to
    // authorize it against (see CollectionWriteTargetResolver's exclusion list), so it is
    // restricted to an admin instead of any authenticated user.
    [Authorize(Roles = nameof(UserRole.Admin))]
    [RequestSizeLimit(500_000_000)]
    [RequestFormLimits(MultipartBodyLengthLimit = 500_000_000)]
    public async Task<IActionResult> Restore(IFormFile file)
    {
        try
        {
            await _backupService.ImportAsync(file.OpenReadStream());
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
