using GestorOT.Application.Interfaces;
using GestorOT.Shared.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace GestorOT.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TenantsController : ControllerBase
{
    private readonly ITenantService _tenantService;
    private readonly IErpSyncService _erpSyncService;

    public TenantsController(ITenantService tenantService, IErpSyncService erpSyncService)
    {
        _tenantService = tenantService;
        _erpSyncService = erpSyncService;
    }

    [HttpGet]
    public async Task<ActionResult<List<TenantDto>>> GetTenants()
    {
        var tenants = await _tenantService.GetTenantsAsync();
        return Ok(tenants.Select(t => new TenantDto(t.Id, t.Name, t.GestorMaxApiKeyEncrypted, t.GestorMaxDatabaseId, t.CreatedAt)).ToList());
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<TenantDto>> GetTenant(Guid id)
    {
        var tenant = await _tenantService.GetTenantByIdAsync(id);
        if (tenant == null) return NotFound();
        return Ok(new TenantDto(tenant.Id, tenant.Name, tenant.GestorMaxApiKeyEncrypted, tenant.GestorMaxDatabaseId, tenant.CreatedAt));
    }

    [HttpPost]
    public async Task<IActionResult> CreateTenant([FromBody] CreateTenantRequest request)
    {
        await _tenantService.CreateTenantAsync(request.Name, request.GestorMaxApiKey, request.GestorMaxDatabaseId);
        return Ok();
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateTenant(Guid id, [FromBody] UpdateTenantRequest request)
    {
        await _tenantService.UpdateTenantAsync(id, request.Name, request.GestorMaxApiKey, request.GestorMaxDatabaseId);
        return Ok();
    }

    [HttpPost("{id}/sync-employees")]
    public async Task<IActionResult> SyncEmployees(Guid id)
    {
        // En un sistema real, nos aseguraríamos de que el ITenantService.CurrentTenantId 
        // esté seteado correctamente para esta llamada (vía Header X-Tenant-Id)
        await _erpSyncService.SyncEmployeesAsync();
        return Ok(new { Message = "Sincronización de empleados completada." });
    }

    [HttpPost("{id}/sync-labors")]
    public async Task<IActionResult> SyncLabors(Guid id)
    {
        await _erpSyncService.SyncLaborTypesAsync();
        return Ok(new { Message = "Sincronización de labores completada." });
    }
}
