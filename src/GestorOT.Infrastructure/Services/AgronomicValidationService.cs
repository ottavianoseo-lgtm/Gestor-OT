using GestorOT.Application.Interfaces;
using GestorOT.Application.Services;
using GestorOT.Shared.Dtos;
using Microsoft.EntityFrameworkCore;

namespace GestorOT.Infrastructure.Services;

public class AgronomicValidationService : IAgronomicValidationService
{
    private readonly IApplicationDbContext _context;

    public AgronomicValidationService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<TankMixAlertDto>> ValidateMixAsync(List<Guid> supplyIds, CancellationToken ct = default)
    {
        if (supplyIds.Count < 2)
            return new List<TankMixAlertDto>();

        var rules = await _context.TankMixRules
            .AsNoTracking()
            .Include(r => r.ProductA)
            .Include(r => r.ProductB)
            .Where(r => supplyIds.Contains(r.ProductAId) && supplyIds.Contains(r.ProductBId))
            .ToListAsync(ct);

        return rules.Select(r => new TankMixAlertDto(
            r.Id,
            r.ProductAId,
            r.ProductA?.ItemName ?? "Producto A",
            r.ProductBId,
            r.ProductB?.ItemName ?? "Producto B",
            r.Severity,
            r.WarningMessage
        )).ToList();
    }

    public async Task<bool> ValidateLaborSurfaceAsync(Guid campaignLotId, decimal hectares, CancellationToken ct = default)
    {
        var campaignLot = await _context.CampaignLots
            .AsNoTracking()
            .FirstOrDefaultAsync(cl => cl.Id == campaignLotId, ct);

        if (campaignLot == null) return true; // Lot not found, maybe standalone labor

        return hectares <= campaignLot.ProductiveArea;
    }

    public async Task<string?> ValidateLaborActivityMatchesRotationAsync(Guid campaignLotId, DateOnly date, Guid activityId, CancellationToken ct = default)
    {
        var activeRotation = await _context.Rotations
            .AsNoTracking()
            .Include(r => r.ErpActivity)
            .Where(r => r.CampaignLotId == campaignLotId && r.StartDate <= date && r.EndDate >= date)
            .FirstOrDefaultAsync(ct);

        if (activeRotation == null) 
        {
            Console.WriteLine($"[DEBUG] No rotation found for Lot {campaignLotId} on {date}");
            return null; // No rotation for this date
        }

        if (activeRotation.ErpActivityId != activityId)
        {
            Console.WriteLine($"[DEBUG] Activity mismatch for Lot {campaignLotId}: RotationActivity={activeRotation.ErpActivityId}, LaborsActivity={activityId}");
            return $"La actividad seleccionada no coincide con el cultivo proyectado ({activeRotation.ErpActivity?.Name}).";
        }

        return null;
    }

    public async Task<string?> ValidateLaborDatesInRotationAsync(Guid campaignLotId, DateTime? estimatedDate, DateTime? executionDate, CancellationToken ct = default)
    {
        // Use execution date if available, else estimated date
        var dateToValidate = executionDate ?? estimatedDate;
        if (dateToValidate == null) return null;

        var dateOnly = DateOnly.FromDateTime(dateToValidate.Value);

        var activeRotation = await _context.Rotations
            .AsNoTracking()
            .Where(r => r.CampaignLotId == campaignLotId && r.StartDate <= dateOnly && r.EndDate >= dateOnly)
            .FirstOrDefaultAsync(ct);

        if (activeRotation == null)
        {
            return "No existe una rotación activa para la fecha seleccionada.";
        }

        return null;
    }
}
