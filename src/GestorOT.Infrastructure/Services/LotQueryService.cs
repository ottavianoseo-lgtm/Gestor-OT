using GestorOT.Application.Interfaces;
using GestorOT.Application.Services;
using GestorOT.Shared.Dtos;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

namespace GestorOT.Infrastructure.Services;

public class LotQueryService : ILotQueryService
{
    private readonly IApplicationDbContext _context;

    public LotQueryService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<LotDto>> GetAllAsync(CancellationToken ct = default)
    {
        var areaMap = await GetLotAreasAsync(ct);

        var lots = await _context.Lots
            .AsNoTracking()
            .Include(l => l.Field)
            .OrderBy(l => l.Name)
            .ToListAsync(ct);

        var writer = new WKTWriter();
        return lots.Select(l => new LotDto(
            l.Id,
            l.FieldId,
            l.Name,
            l.Status,
            l.Geometry != null ? writer.Write(l.Geometry) : null,
            l.Field?.Name,
            areaMap.GetValueOrDefault(l.Id, 0)
        )).ToList();
    }

    public async Task<LotDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var lot = await _context.Lots
            .AsNoTracking()
            .Include(l => l.Field)
            .FirstOrDefaultAsync(l => l.Id == id, ct);

        if (lot == null)
            return null;

        double areaHa = lot.Geometry != null ? await GetLotAreaAsync(id, ct) : 0;

        var writer = new WKTWriter();
        return new LotDto(
            lot.Id,
            lot.FieldId,
            lot.Name,
            lot.Status,
            lot.Geometry != null ? writer.Write(lot.Geometry) : null,
            lot.Field?.Name,
            areaHa
        );
    }

    public async Task<GeoJsonFeatureCollection> GetGeoJsonAsync(CancellationToken ct = default)
    {
        var areaMap = await GetLotAreasAsync(ct);

        var lots = await _context.Lots
            .AsNoTracking()
            .Include(l => l.Field)
            .Where(l => l.Geometry != null)
            .ToListAsync(ct);

        var features = lots.Select(l => new GeoJsonFeature(
            "Feature",
            new Dictionary<string, object>
            {
                ["id"] = l.Id.ToString(),
                ["name"] = l.Name,
                ["status"] = l.Status,
                ["fieldName"] = l.Field?.Name ?? "",
                ["area"] = areaMap.GetValueOrDefault(l.Id, 0)
            },
            l.Geometry != null ? ParseGeometry((Polygon)l.Geometry) : null
        )).ToList();

        return new GeoJsonFeatureCollection("FeatureCollection", features);
    }

    private async Task<Dictionary<Guid, double>> GetLotAreasAsync(CancellationToken ct = default)
    {
        var areas = await _context.Database
            .SqlQueryRaw<LotAreaResult>(
                @"SELECT ""Id"", COALESCE(ST_Area(""Geometry""::geography) / 10000.0, 0) AS ""AreaHa"" FROM public.""Lots"" WHERE ""Geometry"" IS NOT NULL")
            .ToListAsync(ct);
        return areas.ToDictionary(x => x.Id, x => Math.Round(x.AreaHa, 4));
    }

    private async Task<double> GetLotAreaAsync(Guid lotId, CancellationToken ct = default)
    {
        var result = await _context.Database
            .SqlQueryRaw<double>(
                @"SELECT COALESCE(ST_Area(""Geometry""::geography) / 10000.0, 0) AS ""Value"" FROM public.""Lots"" WHERE ""Id"" = {0} AND ""Geometry"" IS NOT NULL",
                lotId)
            .FirstOrDefaultAsync(ct);
        return Math.Round(result, 4);
    }

    private static GeoJsonGeometry ParseGeometry(Polygon polygon)
    {
        var coords = polygon.Coordinates;
        var ring = coords.Select(c => new double[] { c.X, c.Y }).ToArray();
        return new GeoJsonGeometry("Polygon", new double[][][] { ring });
    }
}
