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
        var areaMap = await GetLotAreasAsync(CancellationToken.None);

        var lots = await _context.Lots
            .AsNoTracking()
            .Include(l => l.Field)
            .OrderBy(l => l.Name)
            .ToListAsync(CancellationToken.None);

        var writer = new WKTWriter();
        return lots.Select(l => new LotDto(
            l.Id,
            l.FieldId,
            l.Name,
            l.Status,
            l.Geometry != null ? writer.Write(l.Geometry) : null,
            l.Field?.Name,
            areaMap.GetValueOrDefault(l.Id, 0),
            l.CadastralArea
        )).ToList();
    }

    public async Task<LotDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var lot = await _context.Lots
            .AsNoTracking()
            .Include(l => l.Field)
            .FirstOrDefaultAsync(l => l.Id == id, CancellationToken.None);

        if (lot == null)
            return null;

        double areaHa = lot.Geometry != null ? await GetLotAreaAsync(id, CancellationToken.None) : 0;

        var writer = new WKTWriter();
        return new LotDto(
            lot.Id,
            lot.FieldId,
            lot.Name,
            lot.Status,
            lot.Geometry != null ? writer.Write(lot.Geometry) : null,
            lot.Field?.Name,
            areaHa,
            lot.CadastralArea
        );
    }

    public async Task<GeoJsonFeatureCollection> GetGeoJsonAsync(CancellationToken ct = default)
    {
        var areaMap = await GetLotAreasAsync(CancellationToken.None);

        var lots = await _context.Lots
            .AsNoTracking()
            .Include(l => l.Field)
            .Where(l => l.Geometry != null)
            .ToListAsync(CancellationToken.None);

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

    public async Task<double> CalculateAreaFromWktAsync(string wkt, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(wkt)) return 0;
        
        var result = await _context.Database
            .SqlQueryRaw<double>(
                @"SELECT COALESCE(ST_Area(ST_GeomFromText({0}, 4326)::geography) / 10000.0, 0) AS ""Value""",
                wkt)
            .FirstOrDefaultAsync(ct);
            
        return Math.Round(result, 4);
    }

    public async Task<List<SurfaceHistoryDto>> GetSurfaceHistoryAsync(Guid lotId, CancellationToken ct = default)
    {
        return await _context.CampaignLots
            .AsNoTracking()
            .Include(cl => cl.Campaign)
            .Include(cl => cl.Lot)
            .Where(cl => cl.LotId == lotId)
            .OrderByDescending(cl => cl.Campaign!.StartDate)
            .Select(cl => new SurfaceHistoryDto(
                cl.Campaign!.Name,
                cl.Campaign.StartDate,
                cl.ProductiveArea,
                cl.Lot!.CadastralArea,
                cl.ProductiveArea - cl.Lot.CadastralArea
            ))
            .ToListAsync(ct);
    }

    public async Task<List<CampaignLotDto>> GetCampaignsByLotAsync(Guid lotId, CancellationToken ct = default)
    {
        return await _context.CampaignLots
            .AsNoTracking()
            .Include(cl => cl.Campaign)
            .Include(cl => cl.Lot)
            .Where(cl => cl.LotId == lotId)
            .OrderByDescending(cl => cl.Campaign!.StartDate)
            .Select(cl => new CampaignLotDto(
                cl.Id,
                cl.CampaignId,
                cl.LotId,
                cl.Lot!.FieldId,
                cl.Campaign!.Name,
                null,
                cl.Lot.CadastralArea,
                cl.ProductiveArea,
                cl.CropId
            ))
            .ToListAsync(ct);
    }

    private async Task<Dictionary<Guid, double>> GetLotAreasAsync(CancellationToken ct = default)
    {
        var areas = await _context.Database
            .SqlQueryRaw<LotAreaResult>(
                @"SELECT ""Id"", COALESCE(ST_Area(""Geometry""::geography) / 10000.0, 0) AS ""AreaHa"" FROM public.""Lots"" WHERE ""Geometry"" IS NOT NULL")
            .ToListAsync(CancellationToken.None);
        return areas.ToDictionary(x => x.Id, x => Math.Round(x.AreaHa, 4));
    }

    private async Task<double> GetLotAreaAsync(Guid lotId, CancellationToken ct = default)
    {
        var result = await _context.Database
            .SqlQueryRaw<double>(
                @"SELECT COALESCE(ST_Area(""Geometry""::geography) / 10000.0, 0) AS ""Value"" FROM public.""Lots"" WHERE ""Id"" = {0} AND ""Geometry"" IS NOT NULL",
                lotId)
            .FirstOrDefaultAsync(CancellationToken.None);
        return Math.Round(result, 4);
    }

    private static GeoJsonGeometry ParseGeometry(Polygon polygon)
    {
        var coords = polygon.Coordinates;
        var ring = coords.Select(c => new double[] { c.X, c.Y }).ToArray();
        return new GeoJsonGeometry("Polygon", new double[][][] { ring });
    }
}
