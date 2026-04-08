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
            w.EstimatedCostUSD,
            w.AgreedRate,
            w.StockReserved,
            w.ContractorId,
            w.ContactId,
            w.CampaignId
        )).ToList();
    }

    public async Task<WorkOrderDetailDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var workOrder = await _context.WorkOrders
            .AsNoTracking()
            .Include(w => w.Field)
            .Include(w => w.Labors)
                .ThenInclude(l => l.Lot)
                    .ThenInclude(lot => lot!.Field)
            .Include(w => w.Labors)
                .ThenInclude(l => l.Type)
            .Include(w => w.Labors)
                .ThenInclude(l => l.Supplies)
                    .ThenInclude(s => s.Supply)
            .FirstOrDefaultAsync(w => w.Id == id, ct);

        if (workOrder == null)
            return null;

        return new WorkOrderDetailDto(
            workOrder.Id,
            workOrder.FieldId,
            workOrder.Description,
            workOrder.Status,
            workOrder.AssignedTo,
            workOrder.DueDate,
            workOrder.Field?.Name,
            workOrder.Labors.OrderBy(l => l.CreatedAt).Select(l => new LaborDto(
                l.Id,
                l.WorkOrderId,
                l.LotId,
                l.CampaignLotId,
                l.LaborTypeId,
                l.Status,
                l.ExecutionDate,
                l.EstimatedDate,
                l.Hectares,
                l.CreatedAt,
                l.Rate,
                l.RateUnit,
                l.Lot?.Name,
                l.Type?.Name,
                l.Supplies.OrderBy(s => s.TankMixOrder).Select(s => new LaborSupplyDto(
                    s.Id,
                    s.LaborId,
                    s.SupplyId,
                    s.PlannedDose,
                    s.RealDose,
                    s.PlannedTotal,
                    s.RealTotal,
                    s.UnitOfMeasure,
                    s.Supply?.ItemName,
                    s.Supply?.UnitB,
                    s.TankMixOrder,
                    s.IsSubstitute
                )).ToList(),
                l.PrescriptionMapUrl,
                l.MachineryUsedId,
                l.WeatherLogJson,
                l.Notes,
                l.Lot?.Field?.Name,
                l.PlannedDose,
                l.RealizedDose
            )).ToList(),
            workOrder.OTNumber,
            workOrder.PlannedDate,
            workOrder.ExpirationDate,
            workOrder.EstimatedCostUSD,
            workOrder.AgreedRate,
            workOrder.StockReserved,
            workOrder.CampaignId,
            workOrder.ContractorId,
            workOrder.ContactId
        );
    }
}
