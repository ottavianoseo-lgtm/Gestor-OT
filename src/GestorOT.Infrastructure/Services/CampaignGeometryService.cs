using GestorOT.Application.Interfaces;
using GestorOT.Application.Services;
using GestorOT.Domain.Entities;
using GestorOT.Shared;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.IO;

namespace GestorOT.Infrastructure.Services;

/// <inheritdoc />
public class CampaignGeometryService : ICampaignGeometryService
{
    private readonly IApplicationDbContext _context;
    private readonly ILotQueryService _lotQuery;

    public CampaignGeometryService(IApplicationDbContext context, ILotQueryService lotQuery)
    {
        _context = context;
        _lotQuery = lotQuery;
    }

    public async Task<List<string>> ApplyAsync(Guid campaignId, Lot lot, CancellationToken ct = default)
    {
        var avisos = new List<string>();

        if (campaignId == Guid.Empty || lot.Geometry == null) return avisos;

        var campaignExists = await _context.Campaigns.AnyAsync(c => c.Id == campaignId, ct);
        if (!campaignExists) return avisos;

        // La superficie sale de PostGIS sobre el elipsoide, nunca del cliente.
        var areaHa = await _lotQuery.CalculateAreaFromWktAsync(new WKTWriter().Write(lot.Geometry), ct);
        var superficieReal = (decimal)areaHa;

        var campaignLot = _context.CampaignLots.Local?.FirstOrDefault(cl => cl.CampaignId == campaignId && cl.LotId == lot.Id)
            ?? await _context.CampaignLots
                .FirstOrDefaultAsync(cl => cl.CampaignId == campaignId && cl.LotId == lot.Id, ct);

        if (campaignLot == null)
        {
            _context.CampaignLots.Add(new CampaignLot
            {
                Id = Guid.NewGuid(),
                CampaignId = campaignId,
                LotId = lot.Id,
                ProductiveArea = superficieReal,
                Geometry = lot.Geometry
            });
        }
        else
        {
            var anterior = campaignLot.ProductiveArea;

            campaignLot.Geometry = lot.Geometry;
            campaignLot.ProductiveArea = superficieReal;

            // Solo si baja: si sube, ninguna labor previa puede haber quedado excedida.
            if (superficieReal < anterior)
            {
                avisos.AddRange(await BuscarLaboresExcedidasAsync(campaignLot.Id, lot.Name, superficieReal, ct));
            }
        }

        var desvio = AvisoPorDesvio(lot.Name, areaHa, lot.CadastralArea);
        if (desvio != null) avisos.Add(desvio);

        return avisos;
    }

    public async Task ClearAsync(Guid campaignId, Lot lot, CancellationToken ct = default)
    {
        if (campaignId == Guid.Empty) return;

        var campaignLot = _context.CampaignLots.Local?.FirstOrDefault(cl => cl.CampaignId == campaignId && cl.LotId == lot.Id)
            ?? await _context.CampaignLots
                .FirstOrDefaultAsync(cl => cl.CampaignId == campaignId && cl.LotId == lot.Id, ct);

        if (campaignLot == null) return;

        campaignLot.Geometry = null;
    }

    /// <summary>
    /// Aviso cuando la superficie derivada del polígono se aparta demasiado de la catastral.
    /// Puede ser una inundación real, pero también un polígono mal trazado: por eso se avisa en
    /// vez de pisar el dato en silencio.
    ///
    /// Reutiliza los umbrales de <see cref="SurfaceDeviation"/>, los mismos que muestra el panel
    /// del lote, para que no convivan dos criterios de "esto está muy lejos".
    /// </summary>
    internal static string? AvisoPorDesvio(string lotName, double areaHa, decimal cadastralArea)
    {
        var desvio = SurfaceDeviation.Compare(areaHa, cadastralArea);

        // Sin catastral cargada no hay contra qué comparar, y no es un problema del polígono.
        if (!desvio.CanCompare || desvio.Level != SurfaceDeviationLevel.Critical) return null;

        return $"{lotName}: el polígono da {areaHa:N2} ha y la superficie catastral dice {cadastralArea:N2} ha " +
               $"({desvio.DeviationPercent:N1}% de desvío). Se guardó igual, pero puede ser un polígono mal trazado.";
    }

    private async Task<List<string>> BuscarLaboresExcedidasAsync(
        Guid campaignLotId,
        string lotName,
        decimal superficieReal,
        CancellationToken ct)
    {
        // Por CampaignLotId y no por lote + campaña: la labor ya apunta al CampaignLot, y ese
        // es el vinculo que define contra que superficie se dimensiono.
        var excedidas = await _context.Labors
            .AsNoTracking()
            .Where(l => l.CampaignLotId == campaignLotId && l.Hectares > superficieReal)
            .Select(l => new { l.Hectares })
            .ToListAsync(ct);

        if (excedidas.Count == 0) return new List<string>();

        return new List<string>
        {
            $"{lotName}: la superficie real quedo en {superficieReal:N2} ha y hay {excedidas.Count} labor(es) cargada(s) por encima " +
            $"(hasta {excedidas.Max(e => e.Hectares):N2} ha). Se guardo igual: revisalas."
        };
    }
}
