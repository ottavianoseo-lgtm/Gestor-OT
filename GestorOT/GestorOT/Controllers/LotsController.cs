using GestorOT.Data;
using GestorOT.Shared.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

namespace GestorOT.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LotsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public LotsController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<List<LotDto>>> GetLots()
    {
        var lots = await _context.Lots
            .AsNoTracking()
            .Include(l => l.Field)
            .OrderBy(l => l.Name)
            .ToListAsync();

        return lots.Select(l => new LotDto(
            l.Id,
            l.FieldId,
            l.Name,
            l.Status,
            l.Geometry != null ? GeometryToGeoJson(l.Geometry) : null,
            l.Field?.Name
        )).ToList();
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<LotDto>> GetLot(Guid id)
    {
        var lot = await _context.Lots
            .AsNoTracking()
            .Include(l => l.Field)
            .FirstOrDefaultAsync(l => l.Id == id);

        if (lot == null)
            return NotFound();

        return new LotDto(
            lot.Id,
            lot.FieldId,
            lot.Name,
            lot.Status,
            lot.Geometry != null ? GeometryToGeoJson(lot.Geometry) : null,
            lot.Field?.Name
        );
    }

    [HttpGet("geojson")]
    public async Task<ActionResult<GeoJsonFeatureCollection>> GetLotsGeoJson()
    {
        var lots = await _context.Lots
            .AsNoTracking()
            .Include(l => l.Field)
            .Where(l => l.Geometry != null)
            .ToListAsync();

        var features = lots.Select(l => new GeoJsonFeature(
            "Feature",
            new Dictionary<string, object>
            {
                ["id"] = l.Id.ToString(),
                ["name"] = l.Name,
                ["status"] = l.Status,
                ["fieldName"] = l.Field?.Name ?? ""
            },
            l.Geometry != null ? ParseGeometry((Polygon)l.Geometry) : null
        )).ToList();

        return new GeoJsonFeatureCollection("FeatureCollection", features);
    }

    [HttpPost]
    public async Task<ActionResult<LotDto>> CreateLot(LotDto dto)
    {
        var lot = new Lot
        {
            Id = Guid.NewGuid(),
            FieldId = dto.FieldId,
            Name = dto.Name,
            Status = dto.Status,
            Geometry = !string.IsNullOrEmpty(dto.GeoJson) ? GeoJsonToGeometry(dto.GeoJson) : null
        };

        _context.Lots.Add(lot);
        await _context.SaveChangesAsync();

        var result = new LotDto(
            lot.Id,
            lot.FieldId,
            lot.Name,
            lot.Status,
            dto.GeoJson,
            null
        );

        return CreatedAtAction(nameof(GetLot), new { id = lot.Id }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateLot(Guid id, LotDto dto)
    {
        var lot = await _context.Lots.FindAsync(id);
        if (lot == null)
            return NotFound();

        lot.Name = dto.Name;
        lot.Status = dto.Status;
        lot.FieldId = dto.FieldId;
        if (!string.IsNullOrEmpty(dto.GeoJson))
            lot.Geometry = GeoJsonToGeometry(dto.GeoJson);

        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteLot(Guid id)
    {
        var lot = await _context.Lots.FindAsync(id);
        if (lot == null)
            return NotFound();

        _context.Lots.Remove(lot);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    private static string GeometryToGeoJson(Geometry geometry)
    {
        var writer = new GeoJsonWriter();
        return writer.Write(geometry);
    }

    private static Polygon? GeoJsonToGeometry(string geoJson)
    {
        var reader = new GeoJsonReader();
        return reader.Read<Polygon>(geoJson);
    }

    private static GeoJsonGeometry ParseGeometry(Polygon polygon)
    {
        var coords = polygon.Coordinates;
        var ring = coords.Select(c => new double[] { c.X, c.Y }).ToArray();
        
        return new GeoJsonGeometry("Polygon", new double[][][] { ring });
    }
}
