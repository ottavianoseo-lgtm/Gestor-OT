using GestorOT.Application.Interfaces;
using GestorOT.Application.Services;
using GestorOT.Domain.Entities;
using GestorOT.Shared.Dtos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using NetTopologySuite.Operation.Union;

namespace GestorOT.Infrastructure.Services;

public class LotBulkLinkService : ILotBulkLinkService
{
    private readonly IApplicationDbContext _context;
    private readonly ILotQueryService _lotQuery;
    private readonly ILogger<LotBulkLinkService> _logger;

    public LotBulkLinkService(
        IApplicationDbContext context,
        ILotQueryService lotQuery,
        ILogger<LotBulkLinkService> logger)
    {
        _context = context;
        _lotQuery = lotQuery;
        _logger = logger;
    }

    /// <summary>
    /// Los nombres se comparan sin distinguir mayúsculas, sin espacios de más y sin ceros a la
    /// izquierda: el .dbf suele traer "01" donde el lote se llama "1".
    /// </summary>
    private static string Normalize(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return string.Empty;

        var collapsed = string.Join(' ', name.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        var trimmed = collapsed.TrimStart('0');

        // "0" completo no debe quedar vacío.
        if (trimmed.Length == 0) trimmed = collapsed;

        return trimmed.ToUpperInvariant();
    }

    public async Task<LotMatchResultDto> ProposeAsync(LotMatchRequestDto request, CancellationToken ct = default)
    {
        // El cruce va siempre acotado al campo: los .dbf traen el lote como "1", "2", sin
        // prefijo del establecimiento, asi que a nivel global colisionaria entre campos.
        var lotsDelCampo = await _context.Lots
            .AsNoTracking()
            .Where(l => l.FieldId == request.FieldId)
            .Select(l => new { l.Id, l.Name, TieneGeometria = l.Geometry != null })
            .ToListAsync(ct);

        var porNombre = lotsDelCampo
            .GroupBy(l => Normalize(l.Name))
            .ToDictionary(g => g.Key, g => g.ToList());

        var proposals = new List<LotMatchProposalDto>();

        foreach (var feature in request.Features)
        {
            var clave = Normalize(feature.Name);
            porNombre.TryGetValue(clave, out var candidatos);
            candidatos ??= [];

            var (status, accion, lotId, lotName, tieneGeom) = candidatos.Count switch
            {
                1 => (LotMatchStatus.ExactMatch, LotLinkAction.Link, (Guid?)candidatos[0].Id, candidatos[0].Name, candidatos[0].TieneGeometria),
                > 1 => (LotMatchStatus.Ambiguous, LotLinkAction.Skip, null, null, false),
                // Sin match se propone crear, que es lo que el operador quiere en el alta inicial
                // de un campo. Si el nombre viene vacio no hay con que crearlo: se saltea.
                _ => string.IsNullOrWhiteSpace(feature.Name)
                    ? (LotMatchStatus.NoMatch, LotLinkAction.Skip, null, null, false)
                    : (LotMatchStatus.NoMatch, LotLinkAction.Create, null, null, false)
            };

            proposals.Add(new LotMatchProposalDto(
                feature.Name,
                feature.Wkt,
                feature.AreaHa,
                feature.SourceShapefile,
                status,
                accion,
                lotId,
                lotName,
                tieneGeom,
                candidatos.Select(c => new LotCandidateDto(c.Id, c.Name, c.TieneGeometria)).ToList()));
        }

        return new LotMatchResultDto(
            proposals,
            proposals.Count(p => p.SuggestedAction == LotLinkAction.Link),
            proposals.Count(p => p.SuggestedAction == LotLinkAction.Create),
            proposals.Count(p => p.Status == LotMatchStatus.Ambiguous));
    }

    public async Task<LotBulkLinkResultDto> ApplyAsync(LotBulkLinkRequestDto request, CancellationToken ct = default)
    {
        var aAplicar = request.Items.Where(i => i.Action != LotLinkAction.Skip).ToList();
        var salteados = request.Items.Count - aAplicar.Count;

        var overlapWarnings = new List<string>();

        // El chequeo de solapamiento va ANTES de abrir la transaccion: usa sus propias consultas
        // y conviene reportar todos los conflictos juntos, no cortar en el primero.
        if (!request.OverrideOverlap)
        {
            foreach (var item in aAplicar)
            {
                var overlap = await _lotQuery.CheckLotOverlapAsync(item.Wkt, request.FieldId, item.LotId, ct);
                if (overlap.HasOverlap)
                {
                    overlapWarnings.Add($"{item.FeatureName}: {overlap.Message}");
                }
            }

            if (overlapWarnings.Count > 0)
            {
                // No se aplica nada: que el operador decida una vez sobre todos los conflictos.
                return new LotBulkLinkResultDto(
                    false, 0, 0, 0, aAplicar.Count,
                    aAplicar.Select(i => new LotBulkLinkItemResultDto(i.FeatureName, "Rejected", i.LotId, "Solapamiento detectado")).ToList(),
                    overlapWarnings);
            }
        }

        var reader = new WKTReader();
        var writer = new WKTWriter();
        var resultados = new List<LotBulkLinkItemResultDto>();
        int creados = 0, actualizados = 0;

        var strategy = _context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            resultados.Clear();
            creados = 0;
            actualizados = 0;

            using var transaction = await _context.Database.BeginTransactionAsync(ct);
            try
            {
                foreach (var item in aAplicar)
                {
                    Geometry geometria;
                    try
                    {
                        geometria = reader.Read(item.Wkt);
                        geometria.SRID = 4326;
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException($"{item.FeatureName}: la geometría no se pudo leer ({ex.Message}).");
                    }

                    Lot lot;

                    if (item.Action == LotLinkAction.Create)
                    {
                        var nombre = string.IsNullOrWhiteSpace(item.NewLotName) ? item.FeatureName : item.NewLotName;
                        if (string.IsNullOrWhiteSpace(nombre))
                        {
                            throw new InvalidOperationException("Hay una feature sin nombre marcada para crear un lote. Asignale un nombre o descartala.");
                        }

                        lot = new Lot
                        {
                            Id = Guid.NewGuid(),
                            FieldId = request.FieldId,
                            Name = nombre.Trim(),
                            Status = "Active",
                            Geometry = geometria,
                            CodCentro = item.CodCentro
                        };

                        _context.Lots.Add(lot);
                        creados++;
                    }
                    else
                    {
                        var destino = await _context.Lots.FirstOrDefaultAsync(l => l.Id == item.LotId, ct);
                        if (destino == null)
                        {
                            throw new InvalidOperationException($"{item.FeatureName}: el lote destino no existe o no pertenece al tenant.");
                        }

                        // Combinar en vez de reemplazar, cuando se pidio y ya habia geometria.
                        if (request.CombineGeometry && destino.Geometry != null)
                        {
                            var union = UnaryUnionOp.Union(new[] { destino.Geometry, geometria });
                            union.SRID = 4326;
                            destino.Geometry = union;
                        }
                        else
                        {
                            destino.Geometry = geometria;
                        }

                        if (item.CodCentro.HasValue)
                        {
                            destino.CodCentro = item.CodCentro;
                        }

                        lot = destino;
                        actualizados++;
                    }

                    resultados.Add(new LotBulkLinkItemResultDto(
                        item.FeatureName,
                        item.Action == LotLinkAction.Create ? "Created" : "Updated",
                        lot.Id,
                        null));
                }

                await _context.SaveChangesAsync(ct);

                // La superficie sale de PostGIS, no del cliente, y recien despues de persistir la
                // geometria. Se hace en una segunda pasada por eso.
                await AsignarSuperficiesAsync(resultados, request, writer, ct);

                await _context.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);

                _logger.LogInformation(
                    "Vinculacion masiva en campo {FieldId}: {Creados} creados, {Actualizados} actualizados, {Salteados} salteados",
                    request.FieldId, creados, actualizados, salteados);

                return new LotBulkLinkResultDto(
                    true, creados, actualizados, salteados, 0, resultados, overlapWarnings);
            }
            catch (InvalidOperationException ex)
            {
                await transaction.RollbackAsync(ct);

                // Todo o nada: si algo falla no queda nada aplicado, y se dice cual fallo.
                return new LotBulkLinkResultDto(
                    false, 0, 0, salteados, aAplicar.Count,
                    aAplicar.Select(i => new LotBulkLinkItemResultDto(
                        i.FeatureName,
                        "Rejected",
                        i.LotId,
                        ex.Message.StartsWith(i.FeatureName, StringComparison.Ordinal) ? ex.Message : null)).ToList(),
                    new List<string> { ex.Message });
            }
            catch (Exception)
            {
                await transaction.RollbackAsync(ct);
                throw;
            }
        });
    }

    /// <summary>
    /// Calcula la superficie de cada lote tocado y, si hay campaña, crea o actualiza su CampaignLot.
    /// </summary>
    private async Task AsignarSuperficiesAsync(
        List<LotBulkLinkItemResultDto> resultados,
        LotBulkLinkRequestDto request,
        WKTWriter writer,
        CancellationToken ct)
    {
        foreach (var resultado in resultados)
        {
            if (resultado.LotId is not Guid lotId) continue;

            var lot = await _context.Lots.FirstOrDefaultAsync(l => l.Id == lotId, ct);
            if (lot?.Geometry == null) continue;

            var areaHa = await _lotQuery.CalculateAreaFromWktAsync(writer.Write(lot.Geometry), ct);

            if (lot.CadastralArea <= 0)
            {
                lot.CadastralArea = (decimal)areaHa;
            }

            if (request.CampaignId is not Guid campaignId || campaignId == Guid.Empty) continue;

            var campaignLot = await _context.CampaignLots
                .FirstOrDefaultAsync(cl => cl.CampaignId == campaignId && cl.LotId == lotId, ct);

            if (campaignLot == null)
            {
                _context.CampaignLots.Add(new CampaignLot
                {
                    Id = Guid.NewGuid(),
                    CampaignId = campaignId,
                    LotId = lotId,
                    ProductiveArea = (decimal)areaHa
                });
            }
            else
            {
                // La superficie real de la campania sale del poligono relevado.
                campaignLot.ProductiveArea = (decimal)areaHa;
            }
        }
    }
}
