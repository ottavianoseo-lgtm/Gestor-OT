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
            .Include(l => l.Field)
            .OrderBy(l => l.Name)
            .ToListAsync();

        return lots.Select(l => new LotDto
        {
            Id = l.Id,
            FieldId = l.FieldId,
            Name = l.Name,
            Status = l.Status,
            FieldName = l.Field?.Name,
            GeoJson = l.Geometry != null ? GeometryToGeoJson(l.Geometry) : null
        }).ToList();
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<LotDto>> GetLot(Guid id)
    {
        var lot = await _context.Lots
            .Include(l => l.Field)
            .FirstOrDefaultAsync(l => l.Id == id);

        if (lot == null)
            return NotFound();

        return new LotDto
        {
            Id = lot.Id,
            FieldId = lot.FieldId,
            Name = lot.Name,
            Status = lot.Status,
            FieldName = lot.Field?.Name,
            GeoJson = lot.Geometry != null ? GeometryToGeoJson(lot.Geometry) : null
        };
    }

    [HttpGet("geojson")]
    public async Task<ActionResult<GeoJsonFeatureCollection>> GetLotsGeoJson()
    {
        var lots = await _context.Lots
            .Include(l => l.Field)
            .Where(l => l.Geometry != null)
            .ToListAsync();

        var features = lots.Select(l => new GeoJsonFeature
        {
            Type = "Feature",
            Properties = new Dictionary<string, object>
            {
                ["id"] = l.Id.ToString(),
                ["name"] = l.Name,
                ["status"] = l.Status,
                ["fieldName"] = l.Field?.Name ?? ""
            },
            Geometry = l.Geometry != null ? ParseGeometry(l.Geometry) : null
        }).ToList();

        return new GeoJsonFeatureCollection { Features = features };
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

        dto.Id = lot.Id;
        return CreatedAtAction(nameof(GetLot), new { id = lot.Id }, dto);
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

    private static GeoJsonGeometry? ParseGeometry(Polygon polygon)
    {
        var coords = polygon.Coordinates;
        var ring = coords.Select(c => new[] { c.X, c.Y }).ToArray();
        
        return new GeoJsonGeometry
        {
            Type = "Polygon",
            Coordinates = new[] { ring }
        };
    }
}
