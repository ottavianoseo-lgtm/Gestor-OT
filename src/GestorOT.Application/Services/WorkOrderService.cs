using GestorOT.Application.Interfaces;
using GestorOT.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GestorOT.Application.Services;

public interface IWorkOrderService
{
    Task ConsolidateSuppliesAsync(Guid workOrderId);
}

public class WorkOrderService : IWorkOrderService
{
    private readonly IApplicationDbContext _context;

    public WorkOrderService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task ConsolidateSuppliesAsync(Guid workOrderId)
    {
        var workOrder = await _context.WorkOrders
            .Include(w => w.Labors)
                .ThenInclude(l => l.Supplies)
            .Include(w => w.SupplyApprovals)
            .FirstOrDefaultAsync(w => w.Id == workOrderId);

        if (workOrder == null) return;

        var suppliesInLabors = workOrder.Labors
            .SelectMany(l => l.Supplies)
            .GroupBy(s => s.SupplyId)
            .Select(g => new { SupplyId = g.Key, Total = g.Sum(s => s.PlannedTotal) })
            .ToList();

        foreach (var item in suppliesInLabors)
        {
            var approval = workOrder.SupplyApprovals.FirstOrDefault(a => a.SupplyId == item.SupplyId);
            if (approval == null)
            {
                approval = new WorkOrderSupplyApproval
                {
                    Id = Guid.NewGuid(),
                    WorkOrderId = workOrderId,
                    SupplyId = item.SupplyId,
                    ApprovedWithdrawal = item.Total
                };
                _context.WorkOrderSupplyApprovals.Add(approval);
            }
            approval.TotalCalculated = item.Total;
        }

        var laborSupplyIds = suppliesInLabors.Select(s => s.SupplyId).ToHashSet();
        var toRemove = workOrder.SupplyApprovals.Where(a => !laborSupplyIds.Contains(a.SupplyId)).ToList();
        _context.WorkOrderSupplyApprovals.RemoveRange(toRemove);

        await _context.SaveChangesAsync();
    }
}