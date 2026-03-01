using Microsoft.AspNetCore.Mvc;
using TroveKeep.Core.Interfaces.Services;

namespace TroveKeep.Api.Controllers;

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
        return File(data, "application/json", fileName);
    }

    [HttpPost("restore")]
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
