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
}
