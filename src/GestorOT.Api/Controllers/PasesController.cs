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
    private readonly IErpSyncService _erpSyncService;
    private readonly IApplicationDbContext _context;

    public PasesController(
        IPaseBuilderService paseBuilderService,
        IPaseXlsxExportService exportService,
        IErpSyncService erpSyncService,
        IApplicationDbContext context)
    {
        _paseBuilderService = paseBuilderService;
        _exportService = exportService;
        _erpSyncService = erpSyncService;
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

    [HttpGet("erp/empresas")]
    public async Task<ActionResult<List<ErpCompanyDto>>> GetErpEmpresas(CancellationToken ct)
    {
        var tenantId = _context.CurrentTenantId;
        var list = await _erpSyncService.GetEmpresasAsync(tenantId, ct);
        return Ok(list);
    }

    [HttpGet("erp/comprobantes")]
    public async Task<ActionResult<List<ErpVoucherTypeDto>>> GetErpComprobantes(CancellationToken ct)
    {
        var tenantId = _context.CurrentTenantId;
        var list = await _erpSyncService.GetComprobantesAsync(tenantId, ct);
        return Ok(list);
    }

    [HttpGet("erp/monedas")]
    public async Task<ActionResult<List<ErpCurrencyDto>>> GetErpMonedas(CancellationToken ct)
    {
        var tenantId = _context.CurrentTenantId;
        var list = await _erpSyncService.GetMonedasAsync(tenantId, ct);
        return Ok(list);
    }

    [HttpGet("erp/perfiles")]
    public async Task<ActionResult<List<ErpProfileDto>>> GetErpPerfiles(CancellationToken ct)
    {
        var tenantId = _context.CurrentTenantId;
        var list = await _erpSyncService.GetPerfilesAsync(tenantId, ct);
        return Ok(list);
    }

    [HttpGet("erp/cuentas")]
    public async Task<ActionResult<List<ErpAccountDto>>> GetErpCuentas(CancellationToken ct)
    {
        var tenantId = _context.CurrentTenantId;
        var list = await _erpSyncService.GetCuentasAsync(tenantId, ct);
        return Ok(list);
    }

    [HttpGet("erp/cuentas/gestion")]
    public async Task<ActionResult<List<ErpAccountDto>>> GetErpCuentasGestion(CancellationToken ct)
    {
        var tenantId = _context.CurrentTenantId;
        var list = await _erpSyncService.GetCuentasGestionAsync(tenantId, ct);
        return Ok(list);
    }

    [HttpGet("erp/cuentas/centro")]
    public async Task<ActionResult<List<ErpAccountDto>>> GetErpCuentasCentro(CancellationToken ct)
    {
        var tenantId = _context.CurrentTenantId;
        var list = await _erpSyncService.GetCuentasCentroAsync(tenantId, ct);
        return Ok(list);
    }

    [HttpGet("erp/cuentas/contabilidad")]
    public async Task<ActionResult<List<ErpAccountDto>>> GetErpCuentasContabilidad(CancellationToken ct)
    {
        var tenantId = _context.CurrentTenantId;
        var list = await _erpSyncService.GetCuentasContabilidadAsync(tenantId, ct);
        return Ok(list);
    }

    [HttpGet("erp/personas")]
    public async Task<ActionResult<List<ErpPersonItemDto>>> GetErpPersonas(CancellationToken ct)
    {
        var tenantId = _context.CurrentTenantId;
        var list = await _erpSyncService.GetPersonasAsync(tenantId, ct);
        return Ok(list);
    }
}

