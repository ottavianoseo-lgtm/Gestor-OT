using GestorOT.Application.Interfaces;
using GestorOT.Domain.Entities;
using GestorOT.Domain.Enums;
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
            var g = group.ToUpper().Trim();
            query = query.Where(c => c.GrupoConcepto != null && c.GrupoConcepto.ToUpper().Contains(g));
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
    public async Task<IActionResult> ActivateConcept(Guid id, [FromQuery] LaborExecutionMode? mode = null)
    {
        var concept = await _context.ErpConcepts.FindAsync(id);
        if (concept == null) return NotFound();

        var group = (concept.GrupoConcepto ?? "").ToUpper().Trim();

        if (group.Contains("LABOR"))
        {
            var exists = await _context.LaborTypes.AnyAsync(l => l.ExternalErpId == concept.ExternalErpId);
            if (!exists)
            {
                _context.LaborTypes.Add(new LaborType
                {
                    Id = Guid.NewGuid(),
                    TenantId = concept.TenantId,
                    Name = concept.Description,
                    // El subgrupo distingue las dos variantes que el ERP crea para la misma
                    // tarea (por hectarea vs por UTA); sin el quedan dos filas iguales.
                    Description = concept.SubGrupoConcepto,
                    ExternalErpId = concept.ExternalErpId,
                    // El subgrupo del ERP ya dice el modo ("... (CONTRATISTA)" / "... (MAQ
                    // PROPIA)"), así que se deduce de ahí. El parámetro solo pisa esa
                    // deducción, para los ERP donde el subgrupo no lo aclare.
                    ExecutionMode = mode ?? LaborExecutionModeExtensions.InferFromErpSubGroup(concept.SubGrupoConcepto)
                });
            }
        }
        else if (group.Contains("INSUMO"))
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

    [HttpPost("{id:guid}/deactivate")]
    public async Task<IActionResult> DeactivateConcept(Guid id)
    {
        var concept = await _context.ErpConcepts.FindAsync(id);
        if (concept == null) return NotFound();

        var group = (concept.GrupoConcepto ?? "").ToUpper().Trim();

        if (group.Contains("LABOR"))
        {
            var laborType = await _context.LaborTypes
                .FirstOrDefaultAsync(l => l.ExternalErpId == concept.ExternalErpId);
            
            if (laborType != null)
            {
                // Optional: Check if used in any labor before deleting
                var isUsed = await _context.Labors.AnyAsync(l => l.LaborTypeId == laborType.Id);
                if (isUsed) return BadRequest("No se puede desactivar una labor que ya está siendo usada en órdenes de trabajo.");

                _context.LaborTypes.Remove(laborType);
            }
        }
        else if (group.Contains("INSUMO"))
        {
            var inventory = await _context.Inventories
                .FirstOrDefaultAsync(i => i.ExternalErpId == concept.ExternalErpId);
            
            if (inventory != null)
            {
                var isUsed = await _context.LaborSupplies.AnyAsync(s => s.SupplyId == inventory.Id);
                if (isUsed) return BadRequest("No se puede desactivar un insumo que ya está siendo usado.");

                _context.Inventories.Remove(inventory);
            }
        }

        await _context.SaveChangesAsync();
        return Ok();
    }
}
