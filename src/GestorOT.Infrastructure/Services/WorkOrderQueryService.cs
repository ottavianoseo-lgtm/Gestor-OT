using GestorOT.Application.Interfaces;
using GestorOT.Application.Services;
using GestorOT.Shared.Dtos;
using Microsoft.EntityFrameworkCore;

namespace GestorOT.Infrastructure.Services;

public class WorkOrderQueryService : IWorkOrderQueryService
{
    private readonly IApplicationDbContext _context;

    public WorkOrderQueryService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<WorkOrderDto>> GetAllAsync(CancellationToken ct = default)
    {
        var workOrders = await _context.WorkOrders
            .AsNoTracking()
            .Include(w => w.Field)
            .OrderByDescending(w => w.DueDate)
            .ToListAsync(ct);

        return workOrders.Select(w => new WorkOrderDto(
            w.Id,
            w.FieldId,
            w.Description,
            w.Status,
            w.AssignedTo,
            w.DueDate,
            w.Field?.Name,
            w.OTNumber,
            w.PlannedDate,
            w.ExpirationDate,
            w.StockReserved,
            w.ContractorId,
            w.ContactId,
            w.CampaignId,
            w.AcceptsMultiplePeople,
            w.AcceptsMultipleDates
        )).ToList();
    }

    public async Task<WorkOrderDetailDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var workOrder = await _context.WorkOrders
            .AsNoTracking()
            .Include(w => w.Field)
            .Include(w => w.SupplyApprovals)
                .ThenInclude(a => a.Supply)
            .Include(w => w.Labors)
                .ThenInclude(l => l.Lot)
                    .ThenInclude(lot => lot!.Field)
            .Include(w => w.Labors)
                .ThenInclude(l => l.Type)
            .Include(w => w.Labors)
                .ThenInclude(l => l.ErpActivity)
            .Include(w => w.Labors)
                .ThenInclude(l => l.Supplies)
                    .ThenInclude(s => s.Supply)
            .FirstOrDefaultAsync(w => w.Id == id, ct);

        if (workOrder == null)
            return null;

        var laborsDto = workOrder.Labors.OrderBy(l => l.CreatedAt).Select(l => new LaborDto(
            l.Id,
            l.WorkOrderId,
            l.LotId,
            l.CampaignLotId ?? Guid.Empty,
            l.LaborTypeId,
            l.ErpActivityId,
            l.Status,
            l.Mode.ToString(),
            l.ExecutionDate,
            l.EstimatedDate,
            l.Hectares,
            l.CreatedAt,
            l.Rate,
            l.RateUnit,
            l.Lot?.Name,
            l.Type?.Name,
            l.ErpActivity?.Name,
            l.Supplies.OrderBy(s => s.TankMixOrder).Select(s => new LaborSupplyDto(
                s.Id,
                s.LaborId,
                s.SupplyId,
                s.PlannedHectares,
                s.RealHectares,
                s.PlannedDose,
                s.RealDose,
                s.PlannedTotal,
                s.RealTotal,
                s.CalculatedDose,
                s.CalculatedTotal,
                s.UnitOfMeasure,
                s.Supply?.ItemName,
                s.Supply?.UnitA,
                s.TankMixOrder,
                s.IsSubstitute
            )).ToList(),
            l.PrescriptionMapUrl,
            l.MachineryUsedId,
            l.WeatherLogJson,
            l.Notes,
            l.Lot?.Field?.Name,
            l.PlannedDose,
            l.RealizedDose,
            l.ContactId,
            l.IsExternalBilling,
            l.PlannedLaborId
        )).ToList();

        // Step 19: Rule of three for Realized labors
        foreach (var approval in workOrder.SupplyApprovals.Where(a => a.RealTotalUsed.HasValue))
        {
            var supplyId = approval.SupplyId;
            var realTotalUsed = approval.RealTotalUsed!.Value;

            // Get all labors that use this supply
            var laborsWithSupply = laborsDto.Where(l => l.Supplies.Any(s => s.SupplyId == supplyId)).ToList();
            
            // Calculate total planned across all labors for this supply
            var totalPlannedForSupply = laborsWithSupply
                .Sum(l => l.Supplies.Where(s => s.SupplyId == supplyId).Sum(s => s.PlannedTotal));

            if (totalPlannedForSupply > 0)
            {
                foreach (var labor in laborsWithSupply)
                {
                    foreach (var supply in labor.Supplies.Where(s => s.SupplyId == supplyId))
                    {
                        var proportion = supply.PlannedTotal / totalPlannedForSupply;
                        supply.CalculatedTotal = realTotalUsed * proportion;
                        
                        // Coef = Total / Area
                        var area = supply.RealHectares ?? labor.Hectares;
                        if (area > 0)
                        {
                            supply.CalculatedDose = supply.CalculatedTotal / area;
                        }
                    }
                }
            }
        }

        var supplyApprovalsDto = workOrder.SupplyApprovals
            .OrderBy(a => a.Supply?.ItemName)
            .Select(a => new WorkOrderSupplyApprovalDto(
                a.Id, 
                a.WorkOrderId, 
                a.SupplyId, 
                a.Supply?.ItemName, 
                a.TotalCalculated, 
                a.ApprovedWithdrawal, 
                a.WithdrawalCenter, 
                a.RealTotalUsed
            )).ToList();

        return new WorkOrderDetailDto(
            workOrder.Id,
            workOrder.FieldId,
            workOrder.Description,
            workOrder.Status,
            workOrder.AssignedTo,
            workOrder.DueDate,
            workOrder.Field?.Name,
            laborsDto,
            workOrder.OTNumber,
            workOrder.PlannedDate,
            workOrder.ExpirationDate,
            workOrder.StockReserved,
            workOrder.CampaignId,
            workOrder.ContractorId,
            workOrder.ContactId,
            supplyApprovalsDto,
            workOrder.AcceptsMultiplePeople,
            workOrder.AcceptsMultipleDates
        );
    }
}
