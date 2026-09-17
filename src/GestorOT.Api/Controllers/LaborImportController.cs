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
        [FromForm] string? laborTypeMappingsJson,
        [FromForm] string? supplierMappingsJson,
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

        List<LaborImportTypeMappingDto> laborTypeMappings = new();
        if (!string.IsNullOrWhiteSpace(laborTypeMappingsJson))
        {
            try
            {
                laborTypeMappings = JsonSerializer.Deserialize(laborTypeMappingsJson, AppJsonSerializerContext.Default.ListLaborImportTypeMappingDto) ?? new();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al deserializar laborTypeMappingsJson");
            }
        }

        List<LaborImportSupplierMappingDto> supplierMappings = new();
        if (!string.IsNullOrWhiteSpace(supplierMappingsJson))
        {
            try
            {
                supplierMappings = JsonSerializer.Deserialize(supplierMappingsJson, AppJsonSerializerContext.Default.ListLaborImportSupplierMappingDto) ?? new();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al deserializar supplierMappingsJson");
            }
        }

        try
        {
            await using var stream = file.OpenReadStream();
            var result = await _importService.ExecuteAsync(campaignId, stream, mappings, laborTypeMappings, supplierMappings, ct);
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

    /// <summary>
    /// Sube un Excel sin mostrar preview: importa de una las filas en verde
    /// (full match) y deja el resto en un lote pendiente para matcheo manual.
    /// </summary>
    [HttpPost("campaigns/{campaignId:guid}/labors/import/upload")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<LaborImportUploadResultDto>> Upload(
        Guid campaignId,
        [FromForm] IFormFile? file,
        [FromForm] bool force = false,
        CancellationToken ct = default)
    {
        if (file == null || file.Length == 0)
            return BadRequest("Debe seleccionar un archivo Excel válido.");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext != ".xlsx" && ext != ".xls")
            return BadRequest("El archivo debe ser un libro de Excel (.xlsx).");

        try
        {
            await using var stream = file.OpenReadStream();
            var result = await _importService.UploadAsync(campaignId, stream, file.FileName, User?.Identity?.Name, force, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en la subida directa de labores para la campaña {CampaignId}", campaignId);
            return StatusCode(500, "Error al importar labores: " + ex.Message);
        }
    }

    [HttpGet("campaigns/{campaignId:guid}/labors/import/batches")]
    public async Task<ActionResult<List<LaborImportBatchDto>>> GetBatches(Guid campaignId, CancellationToken ct)
    {
        return Ok(await _importService.GetBatchesAsync(campaignId, ct));
    }

    [HttpGet("campaigns/{campaignId:guid}/labors/import/pending-count")]
    public async Task<ActionResult<int>> GetPendingCount(Guid campaignId, CancellationToken ct)
    {
        return Ok(await _importService.GetPendingCountAsync(campaignId, ct));
    }

    [HttpGet("labors/import/batches/{batchId:guid}")]
    public async Task<ActionResult<LaborImportBatchDetailDto>> GetBatchDetail(Guid batchId, CancellationToken ct)
    {
        var detail = await _importService.GetBatchDetailAsync(batchId, ct);
        if (detail == null) return NotFound("Lote de importación no encontrado.");
        return Ok(detail);
    }

    [HttpPut("labors/import/batches/{batchId:guid}/mappings")]
    public async Task<IActionResult> SaveBatchMappings(Guid batchId, [FromBody] LaborImportBatchMappingsDto mappings, CancellationToken ct)
    {
        try
        {
            await _importService.SaveBatchMappingsAsync(batchId, mappings, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("labors/import/batches/{batchId:guid}/import")]
    public async Task<ActionResult<LaborImportBatchResolveResultDto>> ImportBatchRows(
        Guid batchId, [FromBody] LaborImportRowIndexesRequest? request, CancellationToken ct)
    {
        try
        {
            var result = await _importService.ImportBatchRowsAsync(batchId, request?.RowIndexes, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al importar filas del lote {BatchId}", batchId);
            return StatusCode(500, "Error al importar filas: " + ex.Message);
        }
    }

    [HttpPost("labors/import/batches/{batchId:guid}/discard")]
    public async Task<ActionResult<LaborImportBatchResolveResultDto>> DiscardBatchRows(
        Guid batchId, [FromBody] LaborImportRowIndexesRequest? request, CancellationToken ct)
    {
        try
        {
            var result = await _importService.DiscardBatchRowsAsync(batchId, request?.RowIndexes, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("labors/import/batches/{batchId:guid}/reevaluate")]
    public async Task<ActionResult<LaborImportBatchDetailDto>> ReevaluateBatch(Guid batchId, CancellationToken ct)
    {
        var detail = await _importService.ReevaluateBatchAsync(batchId, ct);
        if (detail == null) return NotFound("Lote de importación no encontrado.");
        return Ok(detail);
    }
}

public class LaborImportRowIndexesRequest
{
    public List<int>? RowIndexes { get; set; }
}
