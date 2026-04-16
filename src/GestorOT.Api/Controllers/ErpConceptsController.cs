using GestorOT.Application.Interfaces;
using GestorOT.Domain.Entities;
using GestorOT.Shared.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestorOT.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ErpConceptsController : ControllerBase
{
    private readonly IApplicationDbContext _context;

    public ErpConceptsController(IApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<List<ErpConceptDto>>> GetConcepts([FromQuery] string? group)
    {
        var query = _context.ErpConcepts.AsNoTracking();

        if (!string.IsNullOrEmpty(group))
        {
            query = query.Where(c => c.GrupoConcepto == group);
        }

        var concepts = await query.ToListAsync();

        // Check which ones are already activated
        var activatedLaborIds = await _context.LaborTypes.Select(l => l.ExternalErpId).ToListAsync();
        var activatedInventoryIds = await _context.Inventories.Select(i => i.ExternalErpId).ToListAsync();

        return concepts.Select(c => new ErpConceptDto(
            c.Id,
            c.Description,
            c.Stock,
            c.UnitA,
            c.UnitB,
            c.GrupoConcepto,
            c.SubGrupoConcepto,
            c.ExternalErpId,
            c.LastSyncDate,
            activatedLaborIds.Contains(c.ExternalErpId) || activatedInventoryIds.Contains(c.ExternalErpId)
        )).ToList();
    }

    [HttpPost("{id:guid}/activate")]
    public async Task<IActionResult> ActivateConcept(Guid id)
    {
        var concept = await _context.ErpConcepts.FindAsync(id);
        if (concept == null) return NotFound();

        if (concept.GrupoConcepto == "LABOR")
        {
            var exists = await _context.LaborTypes.AnyAsync(l => l.ExternalErpId == concept.ExternalErpId);
            if (!exists)
            {
                _context.LaborTypes.Add(new LaborType
                {
                    Id = Guid.NewGuid(),
                    TenantId = concept.TenantId,
                    Name = concept.Description,
                    ExternalErpId = concept.ExternalErpId
                });
            }
        }
        else if (concept.GrupoConcepto == "INSUMOS")
        {
            var exists = await _context.Inventories.AnyAsync(i => i.ExternalErpId == concept.ExternalErpId);
            if (!exists)
            {
                _context.Inventories.Add(new Inventory
                {
                    Id = Guid.NewGuid(),
                    TenantId = concept.TenantId,
                    ItemName = concept.Description,
                    CurrentStock = concept.Stock,
                    Unit = concept.UnitA ?? "u",
                    UnitB = concept.UnitB ?? "u",
                    ExternalErpId = concept.ExternalErpId,
                    GrupoConcepto = concept.GrupoConcepto,
                    SubGrupoConcepto = concept.SubGrupoConcepto
                });
            }
        }

        await _context.SaveChangesAsync();
        return Ok();
    }
}
