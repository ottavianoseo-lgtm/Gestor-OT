using System.IO.Compression;
using System.Text.Json;
using GestorOT.Application.Interfaces;
using GestorOT.Application.Services;
using GestorOT.Domain.Entities;
using GestorOT.Shared.Dtos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

namespace GestorOT.Infrastructure.Services;

public class GeoJsonZipImportService : IGeoJsonZipImportService
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<GeoJsonZipImportService> _logger;

    public GeoJsonZipImportService(
        IApplicationDbContext context,
        ILogger<GeoJsonZipImportService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<GeoJsonZipPreviewResultDto> PreviewZipAsync(Stream zipStream, CancellationToken ct = default)
    {
        var existingLots = await _context.Lots
            .Include(l => l.Field)
            .AsNoTracking()
            .ToListAsync(ct);

        var lotsById = new Dictionary<Guid, Lot>();
        var lotsByExternalId = new Dictionary<string, Lot>(StringComparer.OrdinalIgnoreCase);

        foreach (var l in existingLots)
        {
            lotsById[l.Id] = l;
            if (!string.IsNullOrWhiteSpace(l.ExternalErpId))
                lotsByExternalId[l.ExternalErpId.Trim()] = l;
        }

        var previewItems = new List<GeoJsonZipFeaturePreviewDto>();
        var wktWriter = new WKTWriter();

        using var memoryStream = new MemoryStream();
        await zipStream.CopyToAsync(memoryStream, ct);
        memoryStream.Position = 0;

        bool isZip = false;
        try
        {
            using var testArchive = new ZipArchive(new MemoryStream(memoryStream.ToArray()), ZipArchiveMode.Read);
            isZip = testArchive.Entries.Count > 0;
        }
        catch
        {
            isZip = false;
        }

        if (isZip)
        {
            memoryStream.Position = 0;
            using var archive = new ZipArchive(memoryStream, ZipArchiveMode.Read);
            var geoEntries = archive.Entries
                .Where(e => !e.FullName.StartsWith("__MACOSX", StringComparison.OrdinalIgnoreCase)
                            && !string.IsNullOrWhiteSpace(e.Name)
                            && (e.Name.EndsWith(".geojson", StringComparison.OrdinalIgnoreCase)
                                || e.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)))
                .OrderBy(e => e.Name)
                .ToList();

            foreach (var entry in geoEntries)
            {
                using var entryStream = entry.Open();
                using var reader = new StreamReader(entryStream);
                var content = await reader.ReadToEndAsync(ct);

                ProcessGeoJsonContent(content, entry.Name, lotsById, lotsByExternalId, wktWriter, previewItems);
            }
        }
        else
        {
            // Archivo GeoJSON individual
            memoryStream.Position = 0;
            using var reader = new StreamReader(memoryStream);
            var content = await reader.ReadToEndAsync(ct);

            ProcessGeoJsonContent(content, "archivo.geojson", lotsById, lotsByExternalId, wktWriter, previewItems);
        }

        var matched = previewItems.Count(i => i.LotFound);
        var unmatched = previewItems.Count - matched;
        var fieldsCovered = previewItems
            .Where(i => !string.IsNullOrWhiteSpace(i.FieldName))
            .Select(i => i.FieldName!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(f => f)
            .ToList();

        var warningsCount = previewItems.Count(i => !string.IsNullOrEmpty(i.Warning));

        return new GeoJsonZipPreviewResultDto(
            previewItems.Count,
            matched,
            unmatched,
            fieldsCovered,
            warningsCount,
            previewItems);
    }

    private void ProcessGeoJsonContent(
        string content,
        string fileName,
        Dictionary<Guid, Lot> lotsById,
        Dictionary<string, Lot> lotsByExternalId,
        WKTWriter wktWriter,
        List<GeoJsonZipFeaturePreviewDto> previewItems)
    {
        try
        {
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;

            var features = new List<JsonElement>();
            if (root.TryGetProperty("type", out var typeProp))
            {
                var typeStr = typeProp.GetString();
                if (string.Equals(typeStr, "FeatureCollection", StringComparison.OrdinalIgnoreCase)
                    && root.TryGetProperty("features", out var featsProp)
                    && featsProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var f in featsProp.EnumerateArray())
                        features.Add(f);
                }
                else if (string.Equals(typeStr, "Feature", StringComparison.OrdinalIgnoreCase))
                {
                    features.Add(root);
                }
                else if (string.Equals(typeStr, "Polygon", StringComparison.OrdinalIgnoreCase)
                         || string.Equals(typeStr, "MultiPolygon", StringComparison.OrdinalIgnoreCase))
                {
                    features.Add(root);
                }
            }

            foreach (var feat in features)
            {
                string? loteId = null;
                string? propCampo = null;
                string? propLote = null;
                decimal? propSupDecl = null;
                decimal? propSupGis = null;

                if (feat.TryGetProperty("properties", out var props) && props.ValueKind == JsonValueKind.Object)
                {
                    foreach (var p in props.EnumerateObject())
                    {
                        var nameLower = p.Name.ToLowerInvariant().Trim();
                        if (nameLower is "lote_id" or "id" or "uuid" or "id_lote" or "loteid")
                        {
                            loteId = p.Value.GetString();
                        }
                        else if (nameLower is "campo" or "field" or "establecimiento")
                        {
                            propCampo = p.Value.GetString();
                        }
                        else if (nameLower is "lote" or "lot" or "nombre")
                        {
                            propLote = p.Value.GetString();
                        }
                        else if (nameLower.Contains("declarad") || nameLower.Contains("sup_decl"))
                        {
                            if (p.Value.TryGetDecimal(out var d)) propSupDecl = d;
                            else if (p.Value.TryGetDouble(out var dbl)) propSupDecl = (decimal)dbl;
                        }
                        else if (nameLower.Contains("superficie_gis") || nameLower.Contains("sup_gis") || nameLower.Contains("gis_ha"))
                        {
                            if (p.Value.TryGetDecimal(out var d)) propSupGis = d;
                            else if (p.Value.TryGetDouble(out var dbl)) propSupGis = (decimal)dbl;
                        }
                    }
                }

                // Si no vino lote_id en las propiedades, intentar extraer del nombre de archivo (ej. <uuid>.geojson)
                if (string.IsNullOrWhiteSpace(loteId))
                {
                    var fileBase = Path.GetFileNameWithoutExtension(fileName).Trim();
                    if (Guid.TryParse(fileBase, out _))
                    {
                        loteId = fileBase;
                    }
                }

                string geomRaw = feat.TryGetProperty("geometry", out var geomEl)
                    ? geomEl.GetRawText()
                    : feat.GetRawText();

                if (!GeoJsonGeometryParser.TryParse(geomRaw, out var geometry, out var error, out var reparada)
                    || geometry == null)
                {
                    previewItems.Add(new GeoJsonZipFeaturePreviewDto(
                        loteId ?? "",
                        fileName,
                        null,
                        propLote,
                        null,
                        propCampo,
                        false,
                        string.Empty,
                        0,
                        propSupDecl,
                        null,
                        null,
                        false,
                        $"Error en geometría: {error ?? "inválida"}"));
                    continue;
                }

                geometry.SRID = 4326;
                var wkt = wktWriter.Write(geometry);

                decimal gisAreaHa = propSupGis ?? ComputeAreaHa(geometry);

                // Buscar el lote
                Lot? matchedLot = null;
                if (!string.IsNullOrWhiteSpace(loteId))
                {
                    if (Guid.TryParse(loteId, out var g) && lotsById.TryGetValue(g, out var l1))
                    {
                        matchedLot = l1;
                    }
                    else if (lotsByExternalId.TryGetValue(loteId, out var l2))
                    {
                        matchedLot = l2;
                    }
                }

                if (matchedLot != null)
                {
                    decimal? declaredArea = matchedLot.CadastralArea > 0 ? matchedLot.CadastralArea : propSupDecl;
                    decimal? diffHa = null;
                    double? devPct = null;
                    string? warning = null;

                    if (reparada)
                    {
                        warning = "La geometría se normalizó automáticamente porque contenía anillos topológicamente imperfectos.";
                    }

                    if (declaredArea.HasValue && declaredArea.Value > 0)
                    {
                        diffHa = gisAreaHa - declaredArea.Value;
                        devPct = (double)(diffHa.Value / declaredArea.Value) * 100.0;

                        if (Math.Abs(diffHa.Value) > 5m || Math.Abs(devPct.Value) > 10.0)
                        {
                            var sign = diffHa.Value >= 0 ? "+" : "";
                            var devMsg = $"Diferencia de superficie: declarada {declaredArea.Value:N1} ha vs GIS {gisAreaHa:N1} ha ({sign}{diffHa.Value:N1} ha, {sign}{devPct.Value:N1}%).";
                            warning = warning != null ? $"{warning} {devMsg}" : devMsg;
                        }
                    }

                    previewItems.Add(new GeoJsonZipFeaturePreviewDto(
                        loteId ?? matchedLot.Id.ToString(),
                        fileName,
                        matchedLot.Id,
                        matchedLot.Name,
                        matchedLot.FieldId,
                        matchedLot.Field?.Name ?? propCampo,
                        true,
                        wkt,
                        gisAreaHa,
                        declaredArea,
                        diffHa,
                        devPct,
                        matchedLot.Geometry != null,
                        warning));
                }
                else
                {
                    previewItems.Add(new GeoJsonZipFeaturePreviewDto(
                        loteId ?? "",
                        fileName,
                        null,
                        propLote,
                        null,
                        propCampo,
                        false,
                        wkt,
                        gisAreaHa,
                        propSupDecl,
                        null,
                        null,
                        false,
                        $"No se encontró ningún lote con ID '{loteId ?? fileName}' en el sistema."));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al procesar archivo GeoJSON {FileName}", fileName);
            previewItems.Add(new GeoJsonZipFeaturePreviewDto(
                "",
                fileName,
                null,
                null,
                null,
                null,
                false,
                string.Empty,
                0,
                null,
                null,
                null,
                false,
                $"Error al leer el archivo: {ex.Message}"));
        }
    }

    public async Task<GeoJsonZipApplyResultDto> ApplyAsync(GeoJsonZipApplyRequestDto request, CancellationToken ct = default)
    {
        if (request.Items.Count == 0)
        {
            return new GeoJsonZipApplyResultDto(false, 0, new(), "No se enviaron elementos para aplicar.");
        }

        var wktReader = new WKTReader();
        var warnings = new List<string>();
        int updatedCount = 0;

        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            var isRelational = _context.Database.IsRelational();
            var tx = isRelational ? await _context.Database.BeginTransactionAsync(ct) : null;

            try
            {
                var lotIds = request.Items.Select(i => i.LotId).Distinct().ToList();
                var lots = await _context.Lots
                    .Where(l => lotIds.Contains(l.Id))
                    .ToListAsync(ct);

                var lotsMap = lots.ToDictionary(l => l.Id);

                List<CampaignLot>? campaignLots = null;
                if (request.CampaignId.HasValue)
                {
                    campaignLots = await _context.CampaignLots
                        .Where(cl => cl.CampaignId == request.CampaignId.Value && lotIds.Contains(cl.LotId))
                        .ToListAsync(ct);
                }

                var campLotsMap = campaignLots?.ToDictionary(cl => cl.LotId) ?? new Dictionary<Guid, CampaignLot>();

                foreach (var item in request.Items)
                {
                    if (!lotsMap.TryGetValue(item.LotId, out var lot))
                    {
                        warnings.Add($"Lote con ID {item.LotId} no encontrado al aplicar.");
                        continue;
                    }

                    try
                    {
                        var geom = wktReader.Read(item.Wkt);
                        geom.SRID = 4326;
                        lot.Geometry = geom;

                        if (campLotsMap.TryGetValue(lot.Id, out var campLot))
                        {
                            campLot.Geometry = geom;
                        }

                        updatedCount++;
                    }
                    catch (Exception ex)
                    {
                        warnings.Add($"Error al guardar geometría en lote '{lot.Name}': {ex.Message}");
                    }
                }

                await _context.SaveChangesAsync(ct);
                if (tx != null) await tx.CommitAsync(ct);

                _logger.LogInformation("Importación GeoJSON por lote_id: {Count} lotes actualizados con geometría.", updatedCount);

                return new GeoJsonZipApplyResultDto(true, updatedCount, warnings);
            }
            catch (Exception ex)
            {
                if (tx != null) await tx.RollbackAsync(ct);
                _logger.LogError(ex, "Error al aplicar geometrías GeoJSON");
                return new GeoJsonZipApplyResultDto(false, 0, warnings, ex.Message);
            }
        });
    }

    private static decimal ComputeAreaHa(Geometry geom)
    {
        if (geom is Polygon poly)
            return ComputePolygonAreaHa(poly);

        if (geom is MultiPolygon mp)
        {
            decimal sum = 0;
            for (int i = 0; i < mp.NumGeometries; i++)
            {
                if (mp.GetGeometryN(i) is Polygon p)
                    sum += ComputePolygonAreaHa(p);
            }
            return sum;
        }

        return 0;
    }

    private static decimal ComputePolygonAreaHa(Polygon poly)
    {
        var ring = poly.ExteriorRing.Coordinates;
        if (ring.Length < 3) return 0;
        double area = 0;
        for (int i = 0; i < ring.Length - 1; i++)
        {
            var p1 = ring[i];
            var p2 = ring[i + 1];
            double x1 = p1.X * Math.PI / 180.0;
            double y1 = p1.Y * Math.PI / 180.0;
            double x2 = p2.X * Math.PI / 180.0;
            double y2 = p2.Y * Math.PI / 180.0;
            area += (x2 - x1) * (2.0 + Math.Sin(y1) + Math.Sin(y2));
        }
        area = Math.Abs(area * 6371000.0 * 6371000.0 / 2.0);
        return Math.Round((decimal)(area / 10000.0), 2);
    }
}
