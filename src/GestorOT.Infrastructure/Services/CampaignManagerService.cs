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
            await _context.SaveChangesAsync(ct);

        return imported;
    }
}
