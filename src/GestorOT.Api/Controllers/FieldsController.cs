using GestorOT.Api.Extensions;
using GestorOT.Application.Interfaces;
using GestorOT.Application.Services;
using GestorOT.Domain.Entities;
using GestorOT.Shared.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestorOT.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FieldsController : ControllerBase
{
    private readonly IApplicationDbContext _context;
    private readonly ILotQueryService _queryService;

    public FieldsController(IApplicationDbContext context, ILotQueryService queryService)
    {
        _context = context;
        _queryService = queryService;
    }

    [HttpGet("geojson")]
    public async Task<ActionResult<GeoJsonFeatureCollection>> GetFieldsGeoJson(CancellationToken ct)
    {
        return await _queryService.GetFieldsGeoJsonAsync(ct);
    }

    [HttpGet]
    public async Task<ActionResult<List<FieldDto>>> GetFields()
    {
        var fields = await _context.Fields
            .AsNoTracking()
            .Include(f => f.Lots)
            .OrderBy(f => f.Name)
            .Select(f => new FieldDto(
                f.Id,
                f.Name,
                f.CreatedAt,
                f.Lots.Select(l => new LotSummaryDto(
                    l.Id,
                    l.Name,
                    l.Status,
                    l.CadastralArea
                )).ToList(),
                f.CodCentro
            ))
            .ToListAsync();

        return fields;
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<FieldDto>> GetField(Guid id)
    {
        var field = await _context.Fields
            .AsNoTracking()
            .Include(f => f.Lots)
            .Where(f => f.Id == id)
            .Select(f => new FieldDto(
                f.Id,
                f.Name,
                f.CreatedAt,
                f.Lots.Select(l => new LotSummaryDto(
                    l.Id,
                    l.Name,
                    l.Status,
                    l.CadastralArea
                )).ToList(),
                f.CodCentro
            ))
            .FirstOrDefaultAsync();

        if (field == null)
            return NotFound();

        return field;
    }

    [HttpPost]
    public async Task<ActionResult<FieldDto>> CreateField(FieldDto dto)
    {
        var field = new Field
        {
            Id = Guid.NewGuid(),
            Name = dto.Name,
            CreatedAt = DateTime.UtcNow,
            CodCentro = dto.CodCentro
        };

        _context.Fields.Add(field);
        await _context.SaveChangesAsync();

        var result = new FieldDto(
            field.Id,
            field.Name,
            field.CreatedAt,
            new List<LotSummaryDto>(),
            field.CodCentro
        );

        return CreatedAtAction(nameof(GetField), new { id = field.Id }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateField(Guid id, FieldDto dto)
    {
        var field = await _context.Fields.FirstOrDefaultAsync(f => f.Id == id);
        if (field == null)
            return NotFound();

        field.Name = dto.Name;
        field.CodCentro = dto.CodCentro;

        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteField(Guid id)
    {
        var field = await _context.Fields.FirstOrDefaultAsync(f => f.Id == id);
        if (field == null)
            return NotFound("El campo no existe.");

        var hasLots = await _context.Lots.AnyAsync(l => l.FieldId == id);
        if (hasLots)
            return BadRequest("No se puede eliminar un campo que todavía tiene lotes asociados. Elimine los lotes primero.");

        _context.Fields.Remove(field);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    // --- Búsqueda paginada (ver PagedQuery) ---

    private static readonly SortMap<Field> FieldSorts = new SortMap<Field>(f => f.Id)
        .Add("name", f => f.Name)
        .Add("lotCount", f => f.Lots.Count, defaultDesc: true)
        .Add("cadastralArea", f => f.Lots.Sum(l => l.CadastralArea), defaultDesc: true)
        .Add("createdAt", f => f.CreatedAt, defaultDesc: true);

    [HttpGet("search")]
    public Task<ActionResult<PagedResult<FieldListItemDto>>> SearchFields(
        [FromQuery] PagedQuery paging,
        [FromQuery] string? search = null,
        [FromQuery] Guid? campaignId = null,
        CancellationToken ct = default)
    {
        var query = _context.Fields.AsNoTracking();
        // Se busca también por nombre de lote: muchas veces se recuerda el lote y no el campo.
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = PagedQueryExtensions.ContainsPattern(search);
            query = query.Where(f => EF.Functions.ILike(f.Name, pattern) || f.Lots.Any(l => EF.Functions.ILike(l.Name, pattern)));
        }
        // Los campos asignados a la campaña ("Ver solo mis campos").
        if (campaignId.HasValue)
            query = query.Where(f => _context.CampaignFields.Any(cf => cf.CampaignId == campaignId && cf.FieldId == f.Id));

        return query.ToPagedAsync(paging, FieldSorts,
            f => new FieldListItemDto(f.Id, f.Name, f.CodCentro, f.Lots.Count, f.Lots.Sum(l => l.CadastralArea), f.CreatedAt), ct);
    }
}
