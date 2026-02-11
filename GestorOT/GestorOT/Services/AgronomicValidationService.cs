using GestorOT.Data;
using GestorOT.Shared.Dtos;
using Microsoft.EntityFrameworkCore;

namespace GestorOT.Services;

public class AgronomicValidationService
{
    private readonly ApplicationDbContext _context;

    public AgronomicValidationService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<TankMixAlertDto>> ValidateMix(List<Guid> supplyIds)
    {
        if (supplyIds.Count < 2)
            return new List<TankMixAlertDto>();

        var rules = await _context.TankMixRules
            .AsNoTracking()
            .Include(r => r.ProductA)
            .Include(r => r.ProductB)
            .Where(r => supplyIds.Contains(r.ProductAId) && supplyIds.Contains(r.ProductBId))
            .ToListAsync();

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
}
