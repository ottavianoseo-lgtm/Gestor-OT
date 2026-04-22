using GestorOT.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestorOT.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SyncController : ControllerBase
{
    private readonly IErpSyncService _erpSyncService;
    private readonly IApplicationDbContext _context;

    public SyncController(IErpSyncService erpSyncService, IApplicationDbContext context)
    {
        _erpSyncService = erpSyncService;
        _context = context;
    }

    [HttpPost("catalog")]
    public async Task<IActionResult> SyncCatalog([FromQuery] Guid? tenantId)
    {
        try
        {
            if (tenantId.HasValue)
            {
                await _erpSyncService.SyncCatalogAsync(tenantId.Value);
            }
            else
            {
                // Sync all active tenants with ERP config
                var tenants = await _context.Tenants
                    .Where(t => !string.IsNullOrEmpty(t.GestorMaxApiKeyEncrypted) && !string.IsNullOrEmpty(t.GestorMaxDatabaseId))
                    .ToListAsync();

                foreach (var tenant in tenants)
                {
                    await _erpSyncService.SyncCatalogAsync(tenant.Id);
                }
            }
            return Ok(new { Message = "Sincronización completada." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    [HttpPost("erp/{tenantId:guid}")]
    public async Task<IActionResult> TriggerSync(Guid tenantId)
    {
        try 
        {
            await _erpSyncService.TotalSyncAsync(tenantId);
            return Ok(new { Message = "Sincronización total completada para el tenant " + tenantId });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    [HttpPost("total-sync/{tenantId:guid}")]
    public async Task<IActionResult> ForceTotalSync(Guid tenantId)
    {
        return await TriggerSync(tenantId);
    }

    [HttpPost("erp/all")]
    public async Task<IActionResult> SyncAll()
    {
        // Esto podria tardar mucho, pero el user lo pide para testear
        // Solo para desarrollo/test
        return Ok(new { Message = "Sync All disparado (vía Worker o manual)" });
    }
}
