using GestorOT.Application.Interfaces;
using GestorOT.Application.Services;
using GestorOT.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GestorOT.Infrastructure.Services;

public class CampaignManagerService : ICampaignManagerService
{
    private readonly IApplicationDbContext _context;

    public CampaignManagerService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<int> ImportLotsFromPreviousCampaignAsync(
        Guid newCampaignId,
        Guid previousCampaignId,
        bool useSuperficieFromPrevious = false,
        CancellationToken ct = default)
    {
        var newCampaign = await _context.Campaigns.FindAsync([newCampaignId], ct);
        if (newCampaign == null)
            throw new InvalidOperationException("La campaña destino no existe.");

        if (newCampaign.Status == "Locked")
            throw new InvalidOperationException("No se pueden importar lotes a una campaña bloqueada.");

        var previousLots = await _context.CampaignLots
            .AsNoTracking()
            .Include(cl => cl.Lot)
            .Where(cl => cl.CampaignId == previousCampaignId)
            .ToListAsync(ct);

        if (previousLots.Count == 0)
            throw new InvalidOperationException("La campaña anterior no tiene lotes asignados.");

        var existingLotIds = await _context.CampaignLots
            .Where(cl => cl.CampaignId == newCampaignId)
            .Select(cl => cl.LotId)
            .ToHashSetAsync(ct);

        int imported = 0;
        foreach (var prev in previousLots)
        {
            if (existingLotIds.Contains(prev.LotId))
                continue;

            var superficie = useSuperficieFromPrevious
                ? prev.ProductiveArea
                : prev.Lot?.CadastralArea ?? prev.ProductiveArea;

            // Asegurar que el Campo de este lote esté en la campaña
            if (prev.Lot != null)
            {
                var fieldExists = await _context.CampaignFields
                    .AnyAsync(cf => cf.CampaignId == newCampaignId && cf.FieldId == prev.Lot.FieldId, ct);

                if (!fieldExists)
                {
                    // Verificar si ya lo agregamos en este loop (localmente en el ChangeTracker)
                    var alreadyInTracker = _context.CampaignFields.Local
                        .Any(cf => cf.CampaignId == newCampaignId && cf.FieldId == prev.Lot.FieldId);

                    if (!alreadyInTracker)
                    {
                        _context.CampaignFields.Add(new CampaignField
                        {
                            Id = Guid.NewGuid(),
                            CampaignId = newCampaignId,
                            FieldId = prev.Lot.FieldId,
                            TargetYieldTonHa = 0,
                            AllocatedHectares = 0
                        });
                    }
                }
            }

            _context.CampaignLots.Add(new CampaignLot
            {
                Id = Guid.NewGuid(),
                CampaignId = newCampaignId,
                LotId = prev.LotId,
                ProductiveArea = superficie,
                CropId = prev.CropId
            });

            imported++;
        }

        if (imported > 0)
        {
            await _context.SaveChangesAsync(ct);
            
            // Recalcular hectáreas para todos los campos afectados
            var affectedFieldIds = previousLots
                .Where(cl => cl.Lot != null)
                .Select(cl => cl.Lot!.FieldId)
                .Distinct();

            foreach (var fieldId in affectedFieldIds)
            {
                await RecalculateFieldHectares(newCampaignId, fieldId, ct);
            }
        }

        return imported;
    }

    private async Task RecalculateFieldHectares(Guid campaignId, Guid fieldId, CancellationToken ct)
    {
        var campaignField = await _context.CampaignFields
            .FirstOrDefaultAsync(cf => cf.CampaignId == campaignId && cf.FieldId == fieldId, ct);

        if (campaignField == null) return;

        var totalHa = await _context.CampaignLots
            .Where(cl => cl.CampaignId == campaignId && cl.Lot!.FieldId == fieldId)
            .SumAsync(cl => cl.ProductiveArea, ct);

        campaignField.AllocatedHectares = totalHa;
        await _context.SaveChangesAsync(ct);
    }
}
