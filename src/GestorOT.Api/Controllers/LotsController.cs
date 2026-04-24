using GestorOT.Application.Interfaces;
using GestorOT.Application.Services;
using GestorOT.Domain.Entities;
using GestorOT.Shared.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

namespace GestorOT.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LotsController : ControllerBase
{
    private readonly IApplicationDbContext _context;
    private readonly ILotQueryService _queryService;

    public LotsController(IApplicationDbContext context, ILotQueryService queryService)
    {
        _context = context;
        _queryService = queryService;
    }

    [HttpGet]
    public async Task<ActionResult<List<LotDto>>> GetLots(CancellationToken ct)
    {
        return await _queryService.GetAllAsync(ct);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<LotDto>> GetLot(Guid id, CancellationToken ct)
    {
        var result = await _queryService.GetByIdAsync(id, ct);
        if (result == null) return NotFound();
        return result;
    }

    [HttpGet("geojson")]
    public async Task<ActionResult<GeoJsonFeatureCollection>> GetLotsGeoJson(CancellationToken ct)
    {
        return await _queryService.GetGeoJsonAsync(ct);
    }

    [HttpGet("{id:guid}/surface-history")]
    public async Task<ActionResult<List<SurfaceHistoryDto>>> GetSurfaceHistory(Guid id, CancellationToken ct)
    {
        return await _queryService.GetSurfaceHistoryAsync(id, ct);
    }

    [HttpGet("{id:guid}/campaigns")]
    public async Task<ActionResult<List<CampaignLotDto>>> GetLotCampaigns(Guid id, CancellationToken ct)
    {
        return await _queryService.GetCampaignsByLotAsync(id, ct);
    }

    [HttpPost]
    public async Task<ActionResult<LotDto>> CreateLot(LotDto dto)
    {
        Polygon? geometry = null;
        double areaHa = 0;
        var cadastralArea = dto.CadastralArea;

        if (!string.IsNullOrEmpty(dto.WktGeometry))
        {
            var reader = new WKTReader();
            geometry = (Polygon)reader.Read(dto.WktGeometry);
            geometry.SRID = 4326;

            areaHa = await _queryService.CalculateAreaFromWktAsync(dto.WktGeometry);
            if (cadastralArea == 0)
            {
                cadastralArea = (decimal)areaHa;
            }
        }

        var lot = new Lot
        {
            Id = Guid.NewGuid(),
            FieldId = dto.FieldId,
            Name = dto.Name,
            Status = dto.Status,
            Geometry = geometry,
            CadastralArea = cadastralArea
        };

        _context.Lots.Add(lot);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetLot), new { id = lot.Id },
            new LotDto(lot.Id, lot.FieldId, lot.Name, lot.Status, dto.WktGeometry, null, areaHa, lot.CadastralArea));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateLot(Guid id, LotDto dto)
    {
        // Use FirstOrDefaultAsync (not FindAsync) so tenant query filter is applied
        var lot = await _context.Lots.FirstOrDefaultAsync(l => l.Id == id);
        if (lot == null) return NotFound("El lote no existe.");

        lot.Name = dto.Name;
        lot.Status = dto.Status;
        lot.FieldId = dto.FieldId;

        if (!string.IsNullOrEmpty(dto.WktGeometry))
        {
            var reader = new WKTReader();
            lot.Geometry = (Polygon)reader.Read(dto.WktGeometry);
            lot.Geometry.SRID = 4326;

            // #20: Only update CadastralArea from GIS if explicitly provided as 0
            // AND the lot had no catastral area before (first time assigning geometry).
            // A user-provided value (even from the form) always wins.
            if (dto.CadastralArea > 0)
            {
                lot.CadastralArea = dto.CadastralArea;
            }
            else if (lot.CadastralArea == 0)
            {
                // Lot had no area at all — use calculated GIS area as initial value
                var areaHa = await _queryService.CalculateAreaFromWktAsync(dto.WktGeometry);
                lot.CadastralArea = (decimal)areaHa;
            }
            // If dto.CadastralArea == 0 but lot already has a value → keep existing (no silent overwrite)
        }
        else if (dto.CadastralArea > 0)
        {
            // Geometry not updated, but user explicitly edited the cadastral area
            lot.CadastralArea = dto.CadastralArea;
        }

        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteLot(Guid id)
    {
        var lot = await _context.Lots.FirstOrDefaultAsync(l => l.Id == id);
        if (lot == null) return NotFound("El lote no existe.");

        var hasWorkOrders = await _context.Labors.AnyAsync(l => l.LotId == id);
        if (hasWorkOrders)
            return BadRequest("No se puede eliminar un lote que tiene labores u órdenes de trabajo asociadas.");

        _context.Lots.Remove(lot);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}
