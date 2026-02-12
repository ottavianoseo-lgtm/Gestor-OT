using GestorOT.Data;
using Microsoft.EntityFrameworkCore;

namespace GestorOT.Services;

public class StockValidationResult
{
    public bool IsValid { get; set; }
    public List<StockShortage> Shortages { get; set; } = new();
}

public class StockShortage
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public double Available { get; set; }
    public decimal Required { get; set; }
    public double Deficit { get; set; }
}

public class StockValidatorService
{
    private readonly ApplicationDbContext _context;

    public StockValidatorService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<StockValidationResult> ValidateStockForWorkOrder(Guid workOrderId)
    {
        var workOrder = await _context.WorkOrders
            .AsNoTracking()
            .Include(w => w.Labors)
                .ThenInclude(l => l.Supplies)
            .FirstOrDefaultAsync(w => w.Id == workOrderId);

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
            .ToListAsync();

        var shortages = new List<StockShortage>();

        foreach (var req in requiredByProduct)
        {
            var inventory = await _context.Inventories
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.Id == req.ProductId);

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

    public async Task<bool> ReserveStock(Guid workOrderId)
    {
        var workOrder = await _context.WorkOrders.FindAsync(workOrderId);
        if (workOrder == null) return false;

        workOrder.StockReserved = true;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ReleaseStock(Guid workOrderId)
    {
        var workOrder = await _context.WorkOrders.FindAsync(workOrderId);
        if (workOrder == null) return false;

        workOrder.StockReserved = false;
        await _context.SaveChangesAsync();
        return true;
    }
}
