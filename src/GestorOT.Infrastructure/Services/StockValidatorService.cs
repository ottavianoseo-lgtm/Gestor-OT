using GestorOT.Application.DTOs;
using GestorOT.Application.Interfaces;
using GestorOT.Application.Services;
using Microsoft.EntityFrameworkCore;

namespace GestorOT.Infrastructure.Services;

public class StockValidatorService : IStockValidatorService
{
    private readonly IApplicationDbContext _context;

    public StockValidatorService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<StockValidationResult> ValidateStockForWorkOrderAsync(Guid workOrderId, CancellationToken ct = default)
    {
        var workOrder = await _context.WorkOrders
            .AsNoTracking()
            .Include(w => w.Labors)
                .ThenInclude(l => l.Supplies)
            .FirstOrDefaultAsync(w => w.Id == workOrderId, ct);

        if (workOrder == null)
            return new StockValidationResult { IsValid = false };

        var requiredByProduct = workOrder.Labors
            .SelectMany(l => l.Supplies)
            .GroupBy(s => s.SupplyId)
            .Select(g => new { ProductId = g.Key, Total = g.Sum(s => s.PlannedTotal) })
            .ToList();

        var reservedByProduct = await _context.WorkOrders
            .AsNoTracking()
            .Where(w => w.StockReserved && w.Id != workOrderId &&
                        (w.Status == "Scheduled" || w.Status == "InProgress"))
            .SelectMany(w => w.Labors)
            .SelectMany(l => l.Supplies)
            .GroupBy(s => s.SupplyId)
            .Select(g => new { ProductId = g.Key, Reserved = g.Sum(s => s.PlannedTotal) })
            .ToListAsync(ct);

        var shortages = new List<StockShortage>();

        foreach (var req in requiredByProduct)
        {
            var inventory = await _context.Inventories
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.Id == req.ProductId, ct);

            if (inventory == null)
            {
                shortages.Add(new StockShortage
                {
                    ProductId = req.ProductId,
                    ProductName = "Producto no encontrado",
                    Available = 0,
                    Required = req.Total,
                    Deficit = (double)req.Total
                });
                continue;
            }

            var reserved = reservedByProduct
                .FirstOrDefault(r => r.ProductId == req.ProductId)?.Reserved ?? 0;

            var availableStock = inventory.CurrentStock - (double)reserved;
            var deficit = availableStock - (double)req.Total;

            if (deficit < 0)
            {
                shortages.Add(new StockShortage
                {
                    ProductId = req.ProductId,
                    ProductName = inventory.ItemName,
                    Available = availableStock,
                    Required = req.Total,
                    Deficit = Math.Abs(deficit)
                });
            }
        }

        return new StockValidationResult
        {
            IsValid = shortages.Count == 0,
            Shortages = shortages
        };
    }

    public async Task<bool> ReserveStockAsync(Guid workOrderId, CancellationToken ct = default)
    {
        var workOrder = await _context.WorkOrders.FindAsync([workOrderId], ct);
        if (workOrder == null) return false;

        workOrder.StockReserved = true;
        await _context.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> ReleaseStockAsync(Guid workOrderId, CancellationToken ct = default)
    {
        var workOrder = await _context.WorkOrders.FindAsync([workOrderId], ct);
        if (workOrder == null) return false;

        workOrder.StockReserved = false;
        await _context.SaveChangesAsync(ct);
        return true;
    }
}
