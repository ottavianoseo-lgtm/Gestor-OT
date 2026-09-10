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
        // Se excluyen los null: un LaborType cargado a mano no tiene codigo ERP, y con el null
        // adentro Contains() marcaba como activado a cualquier concepto sin codigo.
        var activatedLaborIds = await _context.LaborTypes
            .Where(l => l.ExternalErpId != null)
            .Select(l => l.ExternalErpId)
            .ToListAsync();
        var activatedInventoryIds = await _context.Inventories
            .Where(i => i.ExternalErpId != null)
            .Select(i => i.ExternalErpId)
            .ToListAsync();

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
            activatedLaborIds.Contains(c.ExternalErpId) || activatedInventoryIds.Contains(c.ExternalErpId),
            c.ExecutionMode
        )).ToList();
    }

    /// <summary>
    /// Marca un concepto como propio o de contratista sin activarlo ni desactivarlo. Se
    /// puede clasificar antes de que exista el LaborType (queda guardado acá y se usa como
    /// default cuando se active) y, si ya está activado, se propaga al LaborType para que el
    /// filtro de "Tipo de Labor (Tarea)" del editor lo vea sin pasar por Desactivar+Activar.
    /// </summary>
    [HttpPatch("{id:guid}/execution-mode")]
    public async Task<IActionResult> SetExecutionMode(Guid id, [FromQuery] LaborExecutionMode? mode)
    {
        var concept = await _context.ErpConcepts.FindAsync(id);
        if (concept == null) return NotFound();

        concept.ExecutionMode = mode;

        if (concept.ExternalErpId != null)
        {
            var laborTypes = await _context.LaborTypes
                .Where(l => l.ExternalErpId == concept.ExternalErpId)
                .ToListAsync();

            foreach (var laborType in laborTypes)
            {
                laborType.ExecutionMode = mode;
            }
        }

        await _context.SaveChangesAsync();
        return Ok();
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
                // Precedencia: lo que se pasó en esta llamada (el modal de activación),
                // despues lo que ya se marcó en la columna "Propia" del catálogo, y recién
                // ahí lo que el subgrupo del ERP deduce solo ("... (CONTRATISTA)" / "... (MAQ
                // PROPIA)").
                var resolvedMode = mode
                    ?? concept.ExecutionMode
                    ?? LaborExecutionModeExtensions.InferFromErpSubGroup(concept.SubGrupoConcepto);

                // Se deja el concepto en sync con lo que terminó decidiendo el LaborType, para
                // que la columna "Propia" del catálogo no muestre algo distinto de lo activado.
                concept.ExecutionMode = resolvedMode;

                _context.LaborTypes.Add(new LaborType
                {
                    Id = Guid.NewGuid(),
                    TenantId = concept.TenantId,
                    Name = concept.Description,
                    // El subgrupo distingue las dos variantes que el ERP crea para la misma
                    // tarea (por hectarea vs por UTA); sin el quedan dos filas iguales.
                    Description = concept.SubGrupoConcepto,
                    ExternalErpId = concept.ExternalErpId,
                    ExecutionMode = resolvedMode
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
            // Se saca la lista completa, no el primero: el sync viejo matcheaba por nombre y
            // dejo pares de LaborTypes con el mismo ExternalErpId. Borrando uno solo, el
            // gemelo sobrevivia y el concepto seguia figurando como activado aunque la
            // respuesta fuera Ok.
            var laborTypes = await _context.LaborTypes
                .Where(l => l.ExternalErpId == concept.ExternalErpId)
                .ToListAsync();

            if (laborTypes.Count > 0)
            {
                var ids = laborTypes.Select(l => l.Id).ToList();
                var isUsed = await _context.Labors.AnyAsync(l => ids.Contains(l.LaborTypeId));
                if (isUsed) return BadRequest("No se puede desactivar una labor que ya está siendo usada en órdenes de trabajo.");

                _context.LaborTypes.RemoveRange(laborTypes);
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
