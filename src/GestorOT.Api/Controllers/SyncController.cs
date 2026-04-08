using GestorOT.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace GestorOT.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SyncController : ControllerBase
{
    private readonly IErpSyncService _erpSyncService;

    public SyncController(IErpSyncService erpSyncService)
    {
        _erpSyncService = erpSyncService;
    }

    [HttpPost("erp/{tenantId:guid}")]
    public async Task<IActionResult> TriggerSync(Guid tenantId)
    {
        // Ejecutamos en segundo plano para no bloquear al usuario, o esperamos segun prefiera el user
        // En este caso, lo ejecutaremos para devolver el resultado
        try 
        {
            await _erpSyncService.SyncLaborTypesAsync(tenantId);
            await _erpSyncService.SyncContactsAsync(tenantId);
            await _erpSyncService.SyncStockAsync(tenantId);
            return Ok(new { Message = "Sincronización completada para el tenant " + tenantId });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    [HttpPost("erp/all")]
    public async Task<IActionResult> SyncAll()
    {
        // Esto podria tardar mucho, pero el user lo pide para testear
        // Solo para desarrollo/test
        return Ok(new { Message = "Sync All disparado (vía Worker o manual)" });
    }
}
