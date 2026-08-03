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
                ["fieldId"] = l.FieldId.ToString(),
                ["fieldName"] = l.Field?.Name ?? "",
                ["area"] = areaMap.GetValueOrDefault(l.Id, 0)
            },
            l.Geometry != null ? ParseGeometry(l.Geometry) : null
        )).ToList();

        return new GeoJsonFeatureCollection("FeatureCollection", features);
    }

    public async Task<GeoJsonFeatureCollection> GetFieldsGeoJsonAsync(CancellationToken ct = default)
    {
        var fields = await _context.Fields
            .AsNoTracking()
            .Include(f => f.Lots)
            .Where(f => f.Lots.Any(l => l.Geometry != null))
            .ToListAsync(ct);

        var features = new List<GeoJsonFeature>();

        foreach (var field in fields)
        {
            var validGeometries = field.Lots
                .Where(l => l.Geometry != null && !l.Geometry.IsEmpty)
                .Select(l => l.Geometry!)
                .ToList();

            if (validGeometries.Count == 0) continue;

            Geometry? compositeGeometry;
            if (validGeometries.Count == 1)
            {
                compositeGeometry = validGeometries[0];
            }
            else
            {
                compositeGeometry = NetTopologySuite.Operation.Union.UnaryUnionOp.Union(validGeometries);
            }

            var fieldLotIds = field.Lots.Select(l => l.Id).ToList();
            var realNonOverlappingArea = await CalculateNonOverlappingAreaAsync(fieldLotIds, ct);
            var areaToUse = realNonOverlappingArea > 0 ? realNonOverlappingArea : (double)field.Lots.Sum(l => l.CadastralArea);

            features.Add(new GeoJsonFeature(
                "Feature",
                new Dictionary<string, object>
                {
                    ["id"] = field.Id.ToString(),
                    ["name"] = field.Name,
                    ["lotsCount"] = field.Lots.Count,
                    ["area"] = areaToUse
                },
                ParseGeometry(compositeGeometry)
            ));
        }

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

    public async Task<double> CalculateNonOverlappingAreaAsync(List<Guid> lotIds, CancellationToken ct = default)
    {
        if (lotIds == null || lotIds.Count == 0) return 0;

        try
        {
            var lotGeoms = await _context.Lots
                .AsNoTracking()
                .Where(l => lotIds.Contains(l.Id) && l.Geometry != null && !l.Geometry.IsEmpty)
                .Select(l => l.Geometry!)
                .ToListAsync(ct);

            if (lotGeoms.Count == 0)
            {
                var fallbackLots = await _context.Lots
                    .AsNoTracking()
                    .Where(l => lotIds.Contains(l.Id))
                    .SumAsync(l => (double)l.CadastralArea, ct);
                return Math.Round(fallbackLots, 4);
            }

            var union = lotGeoms.Count == 1 ? lotGeoms[0] : NetTopologySuite.Operation.Union.UnaryUnionOp.Union(lotGeoms);
            if (union == null || union.IsEmpty) return 0;

            var writer = new WKTWriter();
            var unionWkt = writer.Write(union);
            return await CalculateAreaFromWktAsync(unionWkt, ct);
        }
        catch
        {
            return 0;
        }
    }

    public async Task<double> CalculateNetNewAreaAsync(string newWkt, List<Guid> existingLotIds, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(newWkt)) return 0;

        var reader = new WKTReader();
        var newGeom = reader.Read(newWkt);
        if (newGeom == null || newGeom.IsEmpty) return 0;

        if (existingLotIds == null || existingLotIds.Count == 0)
        {
            return await CalculateAreaFromWktAsync(newWkt, ct);
        }

        var existingGeoms = await _context.Lots
            .AsNoTracking()
            .Where(l => existingLotIds.Contains(l.Id) && l.Geometry != null && !l.Geometry.IsEmpty)
            .Select(l => l.Geometry!)
            .ToListAsync(ct);

        if (existingGeoms.Count == 0)
        {
            return await CalculateAreaFromWktAsync(newWkt, ct);
        }

        var unionExisting = existingGeoms.Count == 1 ? existingGeoms[0] : NetTopologySuite.Operation.Union.UnaryUnionOp.Union(existingGeoms);
        var netGeom = newGeom.Difference(unionExisting);

        if (netGeom == null || netGeom.IsEmpty) return 0;

        var writer = new WKTWriter();
        var netWkt = writer.Write(netGeom);
        return await CalculateAreaFromWktAsync(netWkt, ct);
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
                cl.CampaignId,
                cl.LotId,
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
                Id: cl.Id,
                CampaignId: cl.CampaignId,
                LotId: cl.LotId,
                FieldId: cl.Lot!.FieldId,
                LotName: cl.Lot.Name,
                FieldName: null,
                CadastralArea: cl.Lot.CadastralArea,
                ProductiveArea: cl.ProductiveArea,
                CropId: cl.CropId,
                CampaignName: cl.Campaign!.Name
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

    private static GeoJsonGeometry? ParseGeometry(Geometry? geometry)
    {
        if (geometry == null) return null;

        if (geometry is Polygon polygon)
        {
            var coords = polygon.Coordinates;
            var ring = coords.Select(c => new double[] { c.X, c.Y }).ToArray();
            return new GeoJsonGeometry("Polygon", new double[][][] { ring });
        }
        else if (geometry is MultiPolygon multiPolygon)
        {
            var polyRings = new List<double[][]>();
            for (int i = 0; i < multiPolygon.NumGeometries; i++)
            {
                if (multiPolygon.GetGeometryN(i) is Polygon poly)
                {
                    var ring = poly.Coordinates.Select(c => new double[] { c.X, c.Y }).ToArray();
                    polyRings.Add(ring);
                }
            }
            return new GeoJsonGeometry("MultiPolygon", polyRings.ToArray());
        }
        else
        {
            var coords = geometry.Coordinates;
            var ring = coords.Select(c => new double[] { c.X, c.Y }).ToArray();
            return new GeoJsonGeometry("Polygon", new double[][][] { ring });
        }
    }
}
