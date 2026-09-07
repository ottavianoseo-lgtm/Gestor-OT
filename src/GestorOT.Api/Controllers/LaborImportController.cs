using System.Text.Json;
using GestorOT.Application.Interfaces;
using GestorOT.Shared;
using GestorOT.Shared.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace GestorOT.Api.Controllers;

[ApiController]
[Route("api")]
public class LaborImportController : ControllerBase
{
    private readonly ILaborExcelImportService _importService;
    private readonly ILogger<LaborImportController> _logger;

    public LaborImportController(
        ILaborExcelImportService importService,
        ILogger<LaborImportController> logger)
    {
        _importService = importService;
        _logger = logger;
    }

    [HttpPost("campaigns/{campaignId:guid}/labors/import/preview")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<LaborImportPreviewDto>> Preview(
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
            var preview = await _importService.PreviewAsync(campaignId, stream, ct);
            return Ok(preview);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al generar vista previa de labores para la campaña {CampaignId}", campaignId);
            return StatusCode(500, "Error al procesar el archivo Excel: " + ex.Message);
        }
    }

    [HttpPost("campaigns/{campaignId:guid}/labors/import/execute")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<LaborImportResultDto>> Execute(
        Guid campaignId,
        [FromForm] IFormFile? file,
        [FromForm] string? mappingsJson,
        CancellationToken ct)
    {
        if (file == null || file.Length == 0)
            return BadRequest("Debe seleccionar un archivo Excel válido.");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext != ".xlsx" && ext != ".xls")
            return BadRequest("El archivo debe ser un libro de Excel (.xlsx).");

        List<LaborImportSupplyMappingDto> mappings = new();
        if (!string.IsNullOrWhiteSpace(mappingsJson))
        {
            try
            {
                mappings = JsonSerializer.Deserialize(mappingsJson, AppJsonSerializerContext.Default.ListLaborImportSupplyMappingDto) ?? new();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al deserializar mappingsJson");
            }
        }

        try
        {
            await using var stream = file.OpenReadStream();
            var result = await _importService.ExecuteAsync(campaignId, stream, mappings, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al ejecutar importación de labores para la campaña {CampaignId}", campaignId);
            return StatusCode(500, "Error al importar labores: " + ex.Message);
        }
    }

    [HttpGet("labors/import/template")]
    public async Task<IActionResult> DownloadTemplate(CancellationToken ct)
    {
        var (bytes, fileName) = await _importService.GenerateTemplateAsync(ct);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }
}
