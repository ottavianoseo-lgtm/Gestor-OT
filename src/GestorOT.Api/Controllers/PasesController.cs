using GestorOT.Application.Interfaces;
using GestorOT.Shared.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace GestorOT.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PasesController : ControllerBase
{
    private readonly IPaseBuilderService _paseBuilderService;
    private readonly IPaseXlsxExportService _exportService;
    private readonly IApplicationDbContext _context;

    public PasesController(
        IPaseBuilderService paseBuilderService,
        IPaseXlsxExportService exportService,
        IApplicationDbContext context)
    {
        _paseBuilderService = paseBuilderService;
        _exportService = exportService;
        _context = context;
    }

    [HttpGet("pending")]
    public async Task<ActionResult<IReadOnlyList<PendingImputacionItemDto>>> GetPendingItems(CancellationToken ct)
    {
        var tenantId = _context.CurrentTenantId;
        var items = await _paseBuilderService.GetPendingItemsAsync(tenantId, ct);
        return Ok(items);
    }

    [HttpGet("lotes")]
    public async Task<ActionResult<IReadOnlyList<PaseLoteDto>>> GetLotes(CancellationToken ct)
    {
        var tenantId = _context.CurrentTenantId;
        var lotes = await _paseBuilderService.GetLotesAsync(tenantId, ct);
        return Ok(lotes);
    }

    [HttpGet("lotes/{id:guid}")]
    public async Task<ActionResult<PaseLoteDto>> GetLote(Guid id, CancellationToken ct)
    {
        var tenantId = _context.CurrentTenantId;
        var lote = await _paseBuilderService.GetLoteAsync(tenantId, id, ct);
        if (lote == null) return NotFound();
        return Ok(lote);
    }

    [HttpPost("lotes")]
    public async Task<ActionResult<PaseLoteResult>> GenerarLote([FromBody] GenerarLoteRequestDto request, CancellationToken ct)
    {
        var tenantId = _context.CurrentTenantId;
        var result = await _paseBuilderService.GenerarLoteAsync(
            tenantId, request.WorkOrderIds, request.LaborIds, request.Descripcion, ct);

        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    [HttpGet("lotes/{id:guid}/export")]
    public async Task<IActionResult> ExportLote(Guid id, CancellationToken ct)
    {
        var tenantId = _context.CurrentTenantId;
        try
        {
            var (stream, fileName) = await _exportService.ExportLoteAsync(tenantId, id, ct);
            return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpGet("account-configurations")]
    public async Task<ActionResult<IReadOnlyList<AccountConfigurationDto>>> GetAccountConfigurations(CancellationToken ct)
    {
        var tenantId = _context.CurrentTenantId;
        var configs = await _paseBuilderService.GetAccountConfigurationsAsync(tenantId, ct);
        return Ok(configs);
    }

    [HttpPost("account-configurations")]
    public async Task<IActionResult> SaveAccountConfiguration([FromBody] AccountConfigurationDto dto, CancellationToken ct)
    {
        var tenantId = _context.CurrentTenantId;
        await _paseBuilderService.SaveAccountConfigurationAsync(tenantId, dto, ct);
        return Ok();
    }
}
