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
    private readonly GestorOT.Services.StockValidatorService _stockValidator;
    private readonly GestorOT.Services.IsoXmlExporterService _isoXmlExporter;

    public WorkOrdersController(
        ApplicationDbContext context,
        GestorOT.Services.StockValidatorService stockValidator,
        GestorOT.Services.IsoXmlExporterService isoXmlExporter)
    {
        _context = context;
        _stockValidator = stockValidator;
        _isoXmlExporter = isoXmlExporter;
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
            w.OTNumber,
            w.PlannedDate,
            w.ExpirationDate,
            w.EstimatedCostUSD,
            w.StockReserved,
            w.ContractorId,
            w.CampaignId
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
            var lines = workOrder.Labors
                .Where(l => l.Status == "Realized")
                .Select(l => new LaborSettlementLineDto(
                    l.LaborType,
                    l.EffectiveArea > 0 ? l.EffectiveArea : l.Hectares,
                    l.RateUnit,
                    l.Rate,
                    l.Rate * (l.EffectiveArea > 0 ? l.EffectiveArea : l.Hectares)
                )).ToList();

            settlementDto = new ServiceSettlementDto(
                settlement.Id,
                settlement.WorkOrderId,
                settlement.TotalHectares,
                settlement.TotalAmount,
                settlement.GeneratedAt,
                settlement.ErpSyncStatus,
                lines,
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
                l.Rate,
                l.RateUnit,
                l.Lot?.Name,
                l.Supplies.OrderBy(s => s.TankMixOrder).Select(s => new LaborSupplyDto(
                    s.Id,
                    s.LaborId,
                    s.SupplyId,
                    s.PlannedDose,
                    s.RealDose,
                    s.PlannedTotal,
                    s.RealTotal,
                    s.DoseUnit,
                    s.Supply?.ItemName,
                    s.Supply?.UnitB,
                    s.TankMixOrder,
                    s.IsSubstitute
                )).ToList(),
                l.PrescriptionMapUrl,
                l.MachineryUsedId,
                l.WeatherLogJson
            )).ToList(),
            settlementDto,
            workOrder.OTNumber,
            workOrder.PlannedDate,
            workOrder.ExpirationDate,
            workOrder.EstimatedCostUSD,
            workOrder.StockReserved,
            workOrder.CampaignId,
            workOrder.ContractorId
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
            OTNumber = dto.OTNumber ?? string.Empty,
            PlannedDate = dto.PlannedDate ?? dto.DueDate,
            ExpirationDate = dto.ExpirationDate ?? dto.DueDate,
            EstimatedCostUSD = dto.EstimatedCostUSD,
            StockReserved = dto.StockReserved,
            ContractorId = dto.ContractorId,
            CampaignId = dto.CampaignId
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
            workOrder.OTNumber,
            workOrder.PlannedDate,
            workOrder.ExpirationDate,
            workOrder.EstimatedCostUSD,
            workOrder.StockReserved,
            workOrder.ContractorId,
            workOrder.CampaignId
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
        workOrder.OTNumber = dto.OTNumber ?? workOrder.OTNumber;
        workOrder.PlannedDate = dto.PlannedDate ?? workOrder.PlannedDate;
        workOrder.ExpirationDate = dto.ExpirationDate ?? workOrder.ExpirationDate;
        workOrder.EstimatedCostUSD = dto.EstimatedCostUSD;
        workOrder.StockReserved = dto.StockReserved;
        workOrder.ContractorId = dto.ContractorId;
        workOrder.CampaignId = dto.CampaignId;

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
        var totalAmount = workOrder.Labors.Sum(l => l.Rate * (l.EffectiveArea > 0 ? l.EffectiveArea : l.Hectares));

        var existingSettlement = await _context.ServiceSettlements
            .FirstOrDefaultAsync(s => s.WorkOrderId == id);

        ServiceSettlement settlement;
        if (existingSettlement != null)
        {
            existingSettlement.TotalHectares = totalHectares;
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
                TotalAmount = totalAmount,
                GeneratedAt = DateTime.UtcNow,
                ErpSyncStatus = "Pending"
            };
            _context.ServiceSettlements.Add(settlement);
        }

        await _context.SaveChangesAsync();

        var lines = workOrder.Labors
            .Where(l => l.Status == "Realized")
            .Select(l => new LaborSettlementLineDto(
                l.LaborType,
                l.EffectiveArea > 0 ? l.EffectiveArea : l.Hectares,
                l.RateUnit,
                l.Rate,
                l.Rate * (l.EffectiveArea > 0 ? l.EffectiveArea : l.Hectares)
            )).ToList();

        return new ServiceSettlementDto(
            settlement.Id,
            settlement.WorkOrderId,
            settlement.TotalHectares,
            settlement.TotalAmount,
            settlement.GeneratedAt,
            settlement.ErpSyncStatus,
            lines,
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

    [HttpPost("{id:guid}/validate-stock")]
    public async Task<ActionResult<object>> ValidateStock(Guid id)
    {
        var result = await _stockValidator.ValidateStockForWorkOrder(id);
        return Ok(new
        {
            result.IsValid,
            Shortages = result.Shortages.Select(s => new
            {
                s.ProductId,
                s.ProductName,
                s.Available,
                s.Required,
                s.Deficit
            })
        });
    }

    [HttpPost("{id:guid}/reserve-stock")]
    public async Task<IActionResult> ReserveStock(Guid id)
    {
        var validation = await _stockValidator.ValidateStockForWorkOrder(id);
        if (!validation.IsValid)
            return BadRequest(new { Message = "Stock insuficiente", validation.Shortages });

        await _stockValidator.ReserveStock(id);
        return Ok(new { Message = "Stock reservado correctamente" });
    }

    [HttpGet("{id:guid}/export-isoxml")]
    public async Task<IActionResult> ExportIsoXml(Guid id)
    {
        try
        {
            var zipBytes = await _isoXmlExporter.ExportWorkOrderAsIsoXml(id);
            return File(zipBytes, "application/zip", $"OT_{id:N}.zip");
        }
        catch (InvalidOperationException)
        {
            return NotFound();
        }
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
