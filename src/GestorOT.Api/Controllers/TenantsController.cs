using System.Security.Claims;
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

    private bool IsSuperAdmin => User.IsInRole("SuperAdmin") 
        || User.FindFirst(ClaimTypes.Role)?.Value == "SuperAdmin";

    private Guid UserTenantId => Guid.TryParse(User.FindFirst("tenant_id")?.Value, out var id) ? id : Guid.Empty;

    [HttpGet]
    public async Task<ActionResult<List<TenantDto>>> GetTenants()
    {
        if (IsSuperAdmin)
        {
            var tenants = await _tenantService.GetTenantsAsync();
            return Ok(tenants.Select(t => new TenantDto(t.Id, t.Name, t.GestorMaxApiKeyEncrypted, t.GestorMaxDatabaseId, t.CreatedAt)).ToList());
        }

        if (UserTenantId != Guid.Empty)
        {
            var tenant = await _tenantService.GetTenantByIdAsync(UserTenantId);
            if (tenant != null)
            {
                return Ok(new List<TenantDto> 
                { 
                    new TenantDto(tenant.Id, tenant.Name, tenant.GestorMaxApiKeyEncrypted, tenant.GestorMaxDatabaseId, tenant.CreatedAt) 
                });
            }
        }

        return Ok(new List<TenantDto>());
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<TenantDto>> GetTenant(Guid id)
    {
        if (!IsSuperAdmin && id != UserTenantId)
        {
            return Forbid();
        }

        var tenant = await _tenantService.GetTenantByIdAsync(id);
        if (tenant == null) return NotFound();
        return Ok(new TenantDto(tenant.Id, tenant.Name, tenant.GestorMaxApiKeyEncrypted, tenant.GestorMaxDatabaseId, tenant.CreatedAt));
    }

    [HttpPost]
    public async Task<IActionResult> CreateTenant([FromBody] CreateTenantRequest request)
    {
        if (!IsSuperAdmin)
        {
            return Forbid();
        }

        await _tenantService.CreateTenantAsync(request.Name, request.GestorMaxApiKey, request.GestorMaxDatabaseId);
        return Ok();
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateTenant(Guid id, [FromBody] UpdateTenantRequest request)
    {
        if (!IsSuperAdmin && id != UserTenantId)
        {
            return Forbid();
        }

        await _tenantService.UpdateTenantAsync(id, request.Name, request.GestorMaxApiKey, request.GestorMaxDatabaseId);
        return Ok();
    }

    [HttpPost("{id}/sync-contacts")]
    public async Task<IActionResult> SyncContacts(Guid id)
    {
        if (!IsSuperAdmin && id != UserTenantId)
        {
            return Forbid();
        }

        await _erpSyncService.SyncContactsAsync(id);
        return Ok(new { Message = "Sincronización de contactos completada." });
    }

    [HttpPost("{id}/sync-labors")]
    public async Task<IActionResult> SyncLabors(Guid id)
    {
        if (!IsSuperAdmin && id != UserTenantId)
        {
            return Forbid();
        }

        await _erpSyncService.SyncLaborTypesAsync(id);
        return Ok(new { Message = "Sincronización de labores completada." });
    }

    [HttpPost("ensure-admins")]
    public async Task<IActionResult> EnsureAdminsExist()
    {
        if (!IsSuperAdmin)
        {
            return Forbid();
        }

        var count = await _tenantService.EnsureAdminsExistAsync();
        return Ok(new { Message = $"Se verificaron las empresas y se crearon {count} usuarios administradores iniciales." });
    }
}

