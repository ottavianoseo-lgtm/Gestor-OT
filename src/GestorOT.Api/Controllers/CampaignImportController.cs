using GestorOT.Application.Interfaces;
using GestorOT.Shared.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace GestorOT.Api.Controllers;

[ApiController]
[Route("api/campaigns")]
public class CampaignImportController : ControllerBase
{
    private readonly ILotExcelImportService _importService;
    private readonly ILogger<CampaignImportController> _logger;

    public CampaignImportController(
        ILotExcelImportService importService,
        ILogger<CampaignImportController> logger)
    {
        _importService = importService;
        _logger = logger;
    }

    [HttpPost("{campaignId:guid}/import/preview")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<LotImportSummaryDto>> Preview(
        Guid campaignId,
        [FromForm] IFormFile? file,
        CancellationToken ct)
    {
        if (file == null || file.Length == 0)
            return BadRequest("Debe seleccionar un archivo Excel válido.");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext != ".xlsx" && ext != ".xls")
            return BadRequest("El archivo debe ser un libro de Excel (.xlsx).");

        try
        {
            await using var stream = file.OpenReadStream();
            var summary = await _importService.PreviewAsync(campaignId, stream, ct);
            return Ok(summary);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al procesar vista previa de importación para la campaña {CampaignId}", campaignId);
            return StatusCode(500, "Ocurrió un error inesperado al leer el archivo Excel: " + ex.Message);
        }
    }

    [HttpPost("{campaignId:guid}/import/execute")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<LotImportResultDto>> Execute(
        Guid campaignId,
        [FromForm] IFormFile? file,
        CancellationToken ct)
    {
        if (file == null || file.Length == 0)
            return BadRequest("Debe seleccionar un archivo Excel válido.");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext != ".xlsx" && ext != ".xls")
            return BadRequest("El archivo debe ser un libro de Excel (.xlsx).");

        try
        {
            await using var stream = file.OpenReadStream();
            var result = await _importService.ExecuteAsync(campaignId, stream, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al ejecutar importación para la campaña {CampaignId}", campaignId);
            return StatusCode(500, "Ocurrió un error al guardar los datos importados: " + ex.Message);
        }
    }

    [HttpGet("import/template")]
    public async Task<IActionResult> DownloadTemplate(CancellationToken ct)
    {
        var (bytes, fileName) = await _importService.GenerateTemplateAsync(ct);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }
}
