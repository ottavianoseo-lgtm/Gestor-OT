using GestorOT.Data;
using GestorOT.Shared.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestorOT.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WorkOrdersController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public WorkOrdersController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<List<WorkOrderDto>>> GetWorkOrders()
    {
        var workOrders = await _context.WorkOrders
            .AsNoTracking()
            .Include(w => w.Lot)
            .OrderByDescending(w => w.DueDate)
            .ToListAsync();

        return workOrders.Select(w => new WorkOrderDto(
            w.Id,
            w.LotId,
            w.Description,
            w.Status,
            w.AssignedTo,
            w.DueDate,
            w.Lot?.Name,
            w.AgreedRate
        )).ToList();
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<WorkOrderDetailDto>> GetWorkOrder(Guid id)
    {
        var workOrder = await _context.WorkOrders
            .AsNoTracking()
            .Include(w => w.Lot)
                .ThenInclude(l => l!.Field)
            .Include(w => w.Labors)
                .ThenInclude(l => l.Lot)
            .Include(w => w.Labors)
                .ThenInclude(l => l.Supplies)
                    .ThenInclude(s => s.Supply)
            .FirstOrDefaultAsync(w => w.Id == id);

        if (workOrder == null)
            return NotFound();

        ServiceSettlementDto? settlementDto = null;
        var settlement = await _context.ServiceSettlements
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.WorkOrderId == id);
        if (settlement != null)
        {
            settlementDto = new ServiceSettlementDto(
                settlement.Id,
                settlement.WorkOrderId,
                settlement.TotalHectares,
                settlement.AgreedRate,
                settlement.TotalAmount,
                settlement.GeneratedAt,
                settlement.ErpSyncStatus,
                workOrder.Description,
                workOrder.Lot?.Name,
                workOrder.AssignedTo
            );
        }

        return new WorkOrderDetailDto(
            workOrder.Id,
            workOrder.LotId,
            workOrder.Description,
            workOrder.Status,
            workOrder.AssignedTo,
            workOrder.DueDate,
            workOrder.Lot?.Name,
            workOrder.Lot?.Field?.Name,
            workOrder.Labors.OrderBy(l => l.CreatedAt).Select(l => new LaborDto(
                l.Id,
                l.WorkOrderId,
                l.LotId,
                l.LaborType,
                l.Status,
                l.ExecutionDate,
                l.Hectares,
                l.CreatedAt,
                l.Lot?.Name,
                l.Supplies.Select(s => new LaborSupplyDto(
                    s.Id,
                    s.LaborId,
                    s.SupplyId,
                    s.PlannedDose,
                    s.RealDose,
                    s.PlannedTotal,
                    s.RealTotal,
                    s.DoseUnit,
                    s.Supply?.ItemName,
                    s.Supply?.UnitB
                )).ToList()
            )).ToList(),
            workOrder.AgreedRate,
            settlementDto
        );
    }

    [HttpPost]
    public async Task<ActionResult<WorkOrderDto>> CreateWorkOrder(WorkOrderDto dto)
    {
        var workOrder = new WorkOrder
        {
            Id = Guid.NewGuid(),
            LotId = dto.LotId,
            Description = dto.Description,
            Status = dto.Status,
            AssignedTo = dto.AssignedTo,
            DueDate = dto.DueDate,
            AgreedRate = dto.AgreedRate
        };

        _context.WorkOrders.Add(workOrder);
        await _context.SaveChangesAsync();

        var result = new WorkOrderDto(
            workOrder.Id,
            workOrder.LotId,
            workOrder.Description,
            workOrder.Status,
            workOrder.AssignedTo,
            workOrder.DueDate,
            null,
            workOrder.AgreedRate
        );

        return CreatedAtAction(nameof(GetWorkOrder), new { id = workOrder.Id }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateWorkOrder(Guid id, WorkOrderDto dto)
    {
        var workOrder = await _context.WorkOrders.FindAsync(id);
        if (workOrder == null)
            return NotFound();

        workOrder.Description = dto.Description;
        workOrder.Status = dto.Status;
        workOrder.AssignedTo = dto.AssignedTo;
        workOrder.DueDate = dto.DueDate;
        workOrder.LotId = dto.LotId;
        workOrder.AgreedRate = dto.AgreedRate;

        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{id:guid}/approve")]
    public async Task<ActionResult<ServiceSettlementDto>> ApproveWorkOrder(Guid id)
    {
        var workOrder = await _context.WorkOrders
            .Include(w => w.Labors)
                .ThenInclude(l => l.Supplies)
            .Include(w => w.Lot)
            .FirstOrDefaultAsync(w => w.Id == id);

        if (workOrder == null)
            return NotFound();

        if (workOrder.Status == "Approved")
            return BadRequest("La OT ya fue aprobada.");

        if (workOrder.Status == "Cancelled")
            return BadRequest("No se puede aprobar una OT cancelada.");

        var unrealizedLabors = workOrder.Labors.Where(l => l.Status != "Realized").ToList();
        if (unrealizedLabors.Count > 0)
            return BadRequest($"Hay {unrealizedLabors.Count} labor(es) sin realizar. Todas las labores deben estar realizadas para aprobar.");

        workOrder.Status = "Approved";

        var totalHectares = workOrder.Labors.Sum(l => l.EffectiveArea > 0 ? l.EffectiveArea : l.Hectares);
        var totalAmount = totalHectares * workOrder.AgreedRate;

        var existingSettlement = await _context.ServiceSettlements
            .FirstOrDefaultAsync(s => s.WorkOrderId == id);

        ServiceSettlement settlement;
        if (existingSettlement != null)
        {
            existingSettlement.TotalHectares = totalHectares;
            existingSettlement.AgreedRate = workOrder.AgreedRate;
            existingSettlement.TotalAmount = totalAmount;
            existingSettlement.GeneratedAt = DateTime.UtcNow;
            settlement = existingSettlement;
        }
        else
        {
            settlement = new ServiceSettlement
            {
                Id = Guid.NewGuid(),
                WorkOrderId = id,
                TotalHectares = totalHectares,
                AgreedRate = workOrder.AgreedRate,
                TotalAmount = totalAmount,
                GeneratedAt = DateTime.UtcNow,
                ErpSyncStatus = "Pending"
            };
            _context.ServiceSettlements.Add(settlement);
        }

        await _context.SaveChangesAsync();

        return new ServiceSettlementDto(
            settlement.Id,
            settlement.WorkOrderId,
            settlement.TotalHectares,
            settlement.AgreedRate,
            settlement.TotalAmount,
            settlement.GeneratedAt,
            settlement.ErpSyncStatus,
            workOrder.Description,
            workOrder.Lot?.Name,
            workOrder.AssignedTo
        );
    }

    [HttpGet("{id:guid}/discrepancy")]
    public async Task<ActionResult<DiscrepancyReportDto>> GetDiscrepancy(Guid id)
    {
        var workOrder = await _context.WorkOrders
            .AsNoTracking()
            .Include(w => w.Labors)
                .ThenInclude(l => l.Supplies)
                    .ThenInclude(s => s.Supply)
            .FirstOrDefaultAsync(w => w.Id == id);

        if (workOrder == null)
            return NotFound();

        var laborDiscrepancies = workOrder.Labors
            .Where(l => l.Status == "Realized")
            .Select(l => new LaborDiscrepancyDto(
                l.Id,
                l.LaborType,
                l.EffectiveArea > 0 ? l.EffectiveArea : l.Hectares,
                l.Supplies
                    .Where(s => s.RealDose.HasValue)
                    .Select(s =>
                    {
                        var discrepancy = s.PlannedDose > 0
                            ? ((s.RealDose!.Value - s.PlannedDose) / s.PlannedDose) * 100
                            : 0;
                        return new SupplyDiscrepancyDto(
                            s.Supply?.ItemName ?? "Insumo",
                            s.PlannedDose,
                            s.RealDose!.Value,
                            Math.Round(discrepancy, 2)
                        );
                    }).ToList()
            )).ToList();

        return new DiscrepancyReportDto(
            workOrder.Id,
            workOrder.Description,
            laborDiscrepancies
        );
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteWorkOrder(Guid id)
    {
        var workOrder = await _context.WorkOrders.FindAsync(id);
        if (workOrder == null)
            return NotFound();

        _context.WorkOrders.Remove(workOrder);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}
