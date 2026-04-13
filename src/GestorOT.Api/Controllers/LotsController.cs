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

    [HttpPost]
    public async Task<ActionResult<LotDto>> CreateLot(LotDto dto)
    {
        Polygon? geometry = null;
        if (!string.IsNullOrEmpty(dto.WktGeometry))
        {
            var reader = new WKTReader();
            geometry = (Polygon)reader.Read(dto.WktGeometry);
            geometry.SRID = 4326;
        }

        var lot = new Lot
        {
            Id = Guid.NewGuid(),
            FieldId = dto.FieldId,
            Name = dto.Name,
            Status = dto.Status,
            Geometry = geometry
        };

        _context.Lots.Add(lot);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetLot), new { id = lot.Id },
            new LotDto(lot.Id, lot.FieldId, lot.Name, lot.Status, dto.WktGeometry, null, 0));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateLot(Guid id, LotDto dto)
    {
        var lot = await _context.Lots.FindAsync(id);
        if (lot == null) return NotFound();

        lot.Name = dto.Name;
        lot.Status = dto.Status;
        lot.FieldId = dto.FieldId;
        if (!string.IsNullOrEmpty(dto.WktGeometry))
        {
            var reader = new WKTReader();
            lot.Geometry = (Polygon)reader.Read(dto.WktGeometry);
            lot.Geometry.SRID = 4326;
        }

        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteLot(Guid id)
    {
        var lot = await _context.Lots.FindAsync(id);
        if (lot == null) return NotFound();

        var hasWorkOrders = await _context.Labors.AnyAsync(l => l.LotId == id);
        if (hasWorkOrders)
            return BadRequest("No se puede eliminar un lote con labores asociadas.");

        _context.Lots.Remove(lot);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}
