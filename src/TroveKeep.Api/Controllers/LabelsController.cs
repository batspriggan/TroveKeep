using Microsoft.AspNetCore.Mvc;
using TroveKeep.Api.DTOs.Requests;
using TroveKeep.Api.DTOs.Responses;
using TroveKeep.Core.Interfaces.Services;
using TroveKeep.Core.Models;

namespace TroveKeep.Api.Controllers;

/// <summary>
/// Unified entry point for label printing. The sink (client download vs remote server)
/// is a persisted setting; in <b>client</b> mode the existing <c>/label-*</c> download
/// endpoints on the entity controllers are still the way to fetch the files, while
/// <c>POST /api/labels/print</c> reports back that the client sink is active.
/// </summary>
[ApiController]
[Route("api/labels")]
public class LabelsController : ControllerBase
{
    private readonly ILabelDispatchService _dispatchService;

    public LabelsController(ILabelDispatchService dispatchService)
    {
        _dispatchService = dispatchService;
    }

    // ---- Config ----

    [HttpGet("config")]
    [ProducesResponseType(typeof(LabelConfigResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetConfig()
    {
        var config = await _dispatchService.GetConfigAsync();
        return Ok(ToResponse(config));
    }

    [HttpPut("config")]
    [ProducesResponseType(typeof(LabelConfigResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateConfig([FromBody] LabelConfigRequest request)
    {
        if (!request.Mode.Equals("client", StringComparison.OrdinalIgnoreCase)
            && !request.Mode.Equals("server", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { error = "mode deve essere 'client' o 'server'" });
        }

        if (request.Mode.Equals("server", StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(request.ServerUrl))
        {
            return BadRequest(new { error = "serverUrl e' obbligatorio in modalita' server" });
        }

        var saved = await _dispatchService.SaveConfigAsync(new LabelPrintConfig
        {
            Mode = request.Mode,
            ServerUrl = request.ServerUrl,
            ServerToken = request.ServerToken,
        });
        return Ok(ToResponse(saved));
    }

    // ---- Print ----

    [HttpPost("print")]
    [ProducesResponseType(typeof(PrintOutcomeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Print([FromBody] PrintLabelRequest request)
    {
        var outcome = request.Kind.ToLowerInvariant() switch
        {
            "set" => await _dispatchService.PrintSetAsync(request.Id, request.Copies, request.Size),
            "bulkpiece" => await _dispatchService.PrintBulkPieceAsync(request.Id, request.Copies),
            "box-summary" => await _dispatchService.PrintBoxSummaryAsync(request.Id, request.Copies),
            "box-qr" => await _dispatchService.PrintBoxQrAsync(request.Id, request.Copies),
            "box-pieces" => await _dispatchService.PrintBoxPieceLabelsAsync(request.Id),
            "container-pieces" => await _dispatchService.PrintContainerPieceLabelsAsync(request.Id),
            _ => null,
        };

        if (outcome is null)
            return BadRequest(new { error = $"kind non valido: '{request.Kind}'" });

        if (outcome.Sink == "none")
            return NotFound(new { error = outcome.Message });

        return Ok(ToResponse(outcome));
    }

    // ---- Remote server introspection ----

    [HttpGet("server/health")]
    [ProducesResponseType(typeof(ServerHealthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetServerHealth()
    {
        var health = await _dispatchService.GetServerHealthAsync();
        if (health is null)
            return NotFound(new { error = "nessun server configurato o server non raggiungibile" });
        return Ok(new ServerHealthResponse(health.Ok, health.Printer, health.Pending, health.Printing, health.Failed));
    }

    [HttpGet("server/jobs")]
    [ProducesResponseType(typeof(IEnumerable<ServerJobResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetServerJobs([FromQuery] string? state = null, [FromQuery] int limit = 100)
    {
        var jobs = await _dispatchService.GetJobsAsync(state, limit);
        if (jobs is null)
            return NotFound(new { error = "nessun server configurato o server non raggiungibile" });
        return Ok(jobs.Select(j => new ServerJobResponse(j.JobId, j.Status, j.Attempts, j.Error, j.Uncertain)));
    }

    [HttpPost("server/jobs/{jobId}/retry")]
    [ProducesResponseType(typeof(ServerJobResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RetryServerJob(string jobId)
    {
        var job = await _dispatchService.RetryJobAsync(jobId);
        if (job is null)
            return NotFound(new { error = "job non trovato, già stampato, o server non raggiungibile" });
        return Ok(new ServerJobResponse(job.JobId, job.Status, job.Attempts, job.Error, job.Uncertain));
    }

    [HttpGet("server/formats")]
    [ProducesResponseType(typeof(IEnumerable<ServerFormatResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetServerFormats()
    {
        var formats = await _dispatchService.GetFormatsAsync();
        if (formats is null)
            return NotFound(new { error = "nessun server configurato o server non raggiungibile" });
        return Ok(formats.Select(f => new ServerFormatResponse(f.Name, f.Label)));
    }

    // ---- Mapping ----

    private static LabelConfigResponse ToResponse(LabelPrintConfig config) =>
        new(config.Mode, config.ServerUrl, !string.IsNullOrEmpty(config.ServerToken));

    private static PrintOutcomeResponse ToResponse(PrintOutcome outcome) =>
        new(outcome.Sink, outcome.Sent, outcome.Rejected, outcome.Uncertain,
            outcome.Items.Select(i => new PrintItemResponse(i.FileName, i.Status, i.JobId, i.Error)).ToList(),
            outcome.ServerUrl, outcome.PrinterAvailable, outcome.Message);
}
