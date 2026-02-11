using GestorOT.Data;
using GestorOT.Shared.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestorOT.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TenantsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public TenantsController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<List<TenantDto>>> GetTenants()
    {
        var tenants = await _context.Tenants
            .AsNoTracking()
            .OrderBy(t => t.Name)
            .ToListAsync();

        return tenants.Select(t => new TenantDto(t.Id, t.Name, t.CreatedAt)).ToList();
    }

    [HttpPost]
    public async Task<ActionResult<TenantDto>> CreateTenant(TenantDto dto)
    {
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = dto.Name,
            CreatedAt = DateTime.UtcNow
        };

        _context.Tenants.Add(tenant);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetTenants), new TenantDto(tenant.Id, tenant.Name, tenant.CreatedAt));
    }
}
