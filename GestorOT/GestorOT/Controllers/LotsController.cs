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

        var writer = new WKTWriter();
        return lots.Select(l => new LotDto(
            l.Id,
            l.FieldId,
            l.Name,
            l.Status,
            l.Geometry != null ? writer.Write(l.Geometry) : null,
            l.Field?.Name,
            l.Geometry != null ? l.Geometry.Area * 10000 : 0
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

        var writer = new WKTWriter();
        return new LotDto(
            lot.Id,
            lot.FieldId,
            lot.Name,
            lot.Status,
            lot.Geometry != null ? writer.Write(lot.Geometry) : null,
            lot.Field?.Name,
            lot.Geometry != null ? lot.Geometry.Area * 10000 : 0
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
                ["fieldName"] = l.Field?.Name ?? "",
                ["area"] = Math.Round(l.Geometry!.Area * 10000, 2)
            },
            l.Geometry != null ? ParseGeometry((Polygon)l.Geometry) : null
        )).ToList();

        return new GeoJsonFeatureCollection("FeatureCollection", features);
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

        var result = new LotDto(
            lot.Id,
            lot.FieldId,
            lot.Name,
            lot.Status,
            dto.WktGeometry,
            null,
            geometry != null ? geometry.Area * 10000 : 0
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
        if (lot == null)
            return NotFound();

        _context.Lots.Remove(lot);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("{id:guid}/history")]
    public async Task<ActionResult<List<PlotHistoryDto>>> GetPlotHistory(Guid id)
    {
        var exists = await _context.Lots.AnyAsync(l => l.Id == id);
        if (!exists)
            return NotFound();

        var history = await _context.CampaignPlots
            .AsNoTracking()
            .Include(cp => cp.Campaign)
            .Include(cp => cp.Crop)
            .Where(cp => cp.PlotId == id)
            .OrderByDescending(cp => cp.Campaign!.StartDate)
            .Select(cp => new PlotHistoryDto(
                cp.CampaignId,
                cp.Campaign != null ? cp.Campaign.Name : "",
                cp.Campaign != null ? cp.Campaign.StartDate : DateOnly.MinValue,
                cp.Campaign != null ? cp.Campaign.EndDate : null,
                cp.Crop != null ? cp.Crop.Name : null,
                cp.ProductiveSurfaceHa,
                cp.EstimatedStartDate,
                cp.EstimatedEndDate
            ))
            .ToListAsync();

        return history;
    }

    private static GeoJsonGeometry ParseGeometry(Polygon polygon)
    {
        var coords = polygon.Coordinates;
        var ring = coords.Select(c => new double[] { c.X, c.Y }).ToArray();
        return new GeoJsonGeometry("Polygon", new double[][][] { ring });
    }
}
