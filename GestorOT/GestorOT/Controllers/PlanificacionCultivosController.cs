using GestorOT.Data;
using GestorOT.Shared.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestorOT.Controllers;

[ApiController]
[Route("api/cultivos")]
public class CultivosController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public CultivosController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<List<CultivoDto>>> GetAll()
    {
        var cultivos = await _context.Cultivos
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new CultivoDto(c.Id, c.Name, c.Variedad, c.Ciclo, c.CreatedAt))
            .ToListAsync();
        return cultivos;
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<CultivoDto>> GetById(Guid id)
    {
        var c = await _context.Cultivos.FindAsync(id);
        if (c == null) return NotFound();
        return new CultivoDto(c.Id, c.Name, c.Variedad, c.Ciclo, c.CreatedAt);
    }

    [HttpPost]
    public async Task<ActionResult<CultivoDto>> Create(CultivoDto dto)
    {
        var entity = new Cultivo
        {
            Id = Guid.NewGuid(),
            Name = dto.Name,
            Variedad = dto.Variedad,
            Ciclo = dto.Ciclo,
            CreatedAt = DateTime.UtcNow
        };
        _context.Cultivos.Add(entity);
        await _context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = entity.Id },
            new CultivoDto(entity.Id, entity.Name, entity.Variedad, entity.Ciclo, entity.CreatedAt));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, CultivoDto dto)
    {
        var entity = await _context.Cultivos.FindAsync(id);
        if (entity == null) return NotFound();
        entity.Name = dto.Name;
        entity.Variedad = dto.Variedad;
        entity.Ciclo = dto.Ciclo;
        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var entity = await _context.Cultivos.FindAsync(id);
        if (entity == null) return NotFound();
        _context.Cultivos.Remove(entity);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}

[ApiController]
[Route("api/planificacion-cultivos")]
public class PlanificacionCultivosController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public PlanificacionCultivosController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<List<PlanificacionCultivoDto>>> GetAll(
        [FromQuery] Guid? campanaId = null, [FromQuery] Guid? loteId = null)
    {
        var query = _context.PlanificacionCultivos
            .AsNoTracking()
            .Include(p => p.Lote)
            .Include(p => p.Campana)
            .Include(p => p.Cultivo)
            .AsQueryable();

        if (campanaId.HasValue)
            query = query.Where(p => p.CampanaId == campanaId.Value);
        if (loteId.HasValue)
            query = query.Where(p => p.LoteId == loteId.Value);

        var result = await query
            .OrderBy(p => p.Lote!.Name)
            .Select(p => new PlanificacionCultivoDto(
                p.Id,
                p.LoteId,
                p.CampanaId,
                p.CultivoId,
                p.SuperficieSembradaHa,
                p.SuperficieGeometriaHa,
                p.Lote != null ? p.Lote.Name : null,
                p.Campana != null ? p.Campana.Name : null,
                p.Cultivo != null ? p.Cultivo.Name : null,
                p.CreatedAt
            ))
            .ToListAsync();

        return result;
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<PlanificacionCultivoDto>> GetById(Guid id)
    {
        var p = await _context.PlanificacionCultivos
            .AsNoTracking()
            .Include(x => x.Lote)
            .Include(x => x.Campana)
            .Include(x => x.Cultivo)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (p == null) return NotFound();

        return new PlanificacionCultivoDto(
            p.Id, p.LoteId, p.CampanaId, p.CultivoId,
            p.SuperficieSembradaHa, p.SuperficieGeometriaHa,
            p.Lote?.Name, p.Campana?.Name, p.Cultivo?.Name, p.CreatedAt);
    }

    [HttpPost]
    public async Task<ActionResult<PlanificacionCultivoDto>> Create(PlanificacionCultivoDto dto)
    {
        var lot = await _context.Lots.FindAsync(dto.LoteId);
        if (lot == null) return BadRequest("Lote no encontrado");

        decimal geoHa = 0;
        if (lot.Geometry != null)
        {
            geoHa = (decimal)(lot.Geometry.Area * 111320 * 111320 * Math.Cos(lot.Geometry.Centroid.Y * Math.PI / 180) / 10000);
        }

        var entity = new PlanificacionCultivo
        {
            Id = Guid.NewGuid(),
            LoteId = dto.LoteId,
            CampanaId = dto.CampanaId,
            CultivoId = dto.CultivoId,
            SuperficieSembradaHa = dto.SuperficieSembradaHa,
            SuperficieGeometriaHa = geoHa,
            CreatedAt = DateTime.UtcNow
        };

        _context.PlanificacionCultivos.Add(entity);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = entity.Id },
            new PlanificacionCultivoDto(entity.Id, entity.LoteId, entity.CampanaId,
                entity.CultivoId, entity.SuperficieSembradaHa, entity.SuperficieGeometriaHa,
                lot.Name, null, null, entity.CreatedAt));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, PlanificacionCultivoDto dto)
    {
        var entity = await _context.PlanificacionCultivos.FindAsync(id);
        if (entity == null) return NotFound();

        entity.CultivoId = dto.CultivoId;
        entity.SuperficieSembradaHa = dto.SuperficieSembradaHa;

        if (dto.LoteId != entity.LoteId)
        {
            var lot = await _context.Lots.FindAsync(dto.LoteId);
            if (lot == null) return BadRequest("Lote no encontrado");
            entity.LoteId = dto.LoteId;

            if (lot.Geometry != null)
            {
                entity.SuperficieGeometriaHa = (decimal)(lot.Geometry.Area * 111320 * 111320 *
                    Math.Cos(lot.Geometry.Centroid.Y * Math.PI / 180) / 10000);
            }
        }

        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var entity = await _context.PlanificacionCultivos.FindAsync(id);
        if (entity == null) return NotFound();
        _context.PlanificacionCultivos.Remove(entity);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("~/api/campos/{campoId}/campanas/{campanaId}/superficie")]
    public async Task<ActionResult<SuperficieCampoDto>> GetSuperficieCampo(Guid campoId, Guid campanaId)
    {
        var campo = await _context.Fields.AsNoTracking().FirstOrDefaultAsync(f => f.Id == campoId);
        if (campo == null) return NotFound("Campo no encontrado");

        var lotIds = await _context.Lots
            .AsNoTracking()
            .Where(l => l.FieldId == campoId && l.Status == "Active")
            .Select(l => l.Id)
            .ToListAsync();

        var planificaciones = await _context.PlanificacionCultivos
            .AsNoTracking()
            .Include(p => p.Lote)
            .Include(p => p.Cultivo)
            .Include(p => p.Campana)
            .Where(p => lotIds.Contains(p.LoteId) && p.CampanaId == campanaId)
            .Select(p => new PlanificacionCultivoDto(
                p.Id, p.LoteId, p.CampanaId, p.CultivoId,
                p.SuperficieSembradaHa, p.SuperficieGeometriaHa,
                p.Lote != null ? p.Lote.Name : null,
                p.Campana != null ? p.Campana.Name : null,
                p.Cultivo != null ? p.Cultivo.Name : null,
                p.CreatedAt
            ))
            .ToListAsync();

        return new SuperficieCampoDto(
            campoId,
            campanaId,
            campo.Name,
            planificaciones.Sum(p => p.SuperficieSembradaHa),
            planificaciones.Sum(p => p.SuperficieGeometriaHa),
            planificaciones.Count,
            planificaciones
        );
    }

    [HttpGet("~/api/lotes/{loteId}/rotacion")]
    public async Task<ActionResult<RotacionHistorialDto>> GetRotacion(Guid loteId)
    {
        var lote = await _context.Lots.AsNoTracking().FirstOrDefaultAsync(l => l.Id == loteId);
        if (lote == null) return NotFound("Lote no encontrado");

        var historial = await _context.PlanificacionCultivos
            .AsNoTracking()
            .Include(p => p.Campana)
            .Include(p => p.Cultivo)
            .Where(p => p.LoteId == loteId)
            .OrderBy(p => p.Campana!.StartDate)
            .Select(p => new RotacionEntryDto(
                p.CampanaId,
                p.Campana != null ? p.Campana.Name : string.Empty,
                p.Campana != null ? p.Campana.StartDate : DateOnly.MinValue,
                p.CultivoId,
                p.Cultivo != null ? p.Cultivo.Name : string.Empty,
                p.SuperficieSembradaHa
            ))
            .ToListAsync();

        return new RotacionHistorialDto(loteId, lote.Name, historial);
    }
}
