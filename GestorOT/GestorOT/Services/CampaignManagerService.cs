using GestorOT.Data;
using Microsoft.EntityFrameworkCore;

namespace GestorOT.Services;

public class CampaignManagerService
{
    private readonly ApplicationDbContext _context;

    public CampaignManagerService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<int> ImportLotsFromPreviousCampaignAsync(
        Guid newCampaignId,
        Guid previousCampaignId,
        bool useSuperficieFromPrevious = false)
    {
        var newCampaign = await _context.Campaigns.FindAsync(newCampaignId);
        if (newCampaign == null)
            throw new InvalidOperationException("La campaña destino no existe.");

        if (newCampaign.Status == "Locked")
            throw new InvalidOperationException("No se pueden importar lotes a una campaña bloqueada.");

        var previousLots = await _context.CampaignLots
            .AsNoTracking()
            .Include(cl => cl.Lot)
            .Where(cl => cl.CampaignId == previousCampaignId)
            .ToListAsync();

        if (previousLots.Count == 0)
            throw new InvalidOperationException("La campaña anterior no tiene lotes asignados.");

        var existingLotIds = await _context.CampaignLots
            .Where(cl => cl.CampaignId == newCampaignId)
            .Select(cl => cl.LotId)
            .ToHashSetAsync();

        int imported = 0;
        foreach (var prev in previousLots)
        {
            if (existingLotIds.Contains(prev.LotId))
                continue;

            var superficie = useSuperficieFromPrevious
                ? prev.SuperficieProductiva
                : prev.Lot?.CadastralArea ?? prev.SuperficieProductiva;

            _context.CampaignLots.Add(new CampaignLot
            {
                Id = Guid.NewGuid(),
                CampaignId = newCampaignId,
                LotId = prev.LotId,
                SuperficieProductiva = superficie,
                CropId = prev.CropId
            });

            imported++;
        }

        if (imported > 0)
            await _context.SaveChangesAsync();

        return imported;
    }
}
