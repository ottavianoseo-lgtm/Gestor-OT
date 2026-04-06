using GestorOT.Application.Interfaces;
using GestorOT.Application.Services;
using GestorOT.Domain.Entities;
using GestorOT.Shared.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestorOT.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WorkOrdersController : ControllerBase
{
    private readonly IApplicationDbContext _context;
    private readonly IWorkOrderQueryService _queryService;
    private readonly IStockValidatorService _stockValidator;
    private readonly IIsoXmlExporterService _isoXmlExporter;

    public WorkOrdersController(
        IApplicationDbContext context,
        IWorkOrderQueryService queryService,
        IStockValidatorService stockValidator,
        IIsoXmlExporterService isoXmlExporter)
    {
        _context = context;
        _queryService = queryService;
        _stockValidator = stockValidator;
        _isoXmlExporter = isoXmlExporter;
    }

    [HttpGet]
    public async Task<ActionResult<List<WorkOrderDto>>> GetWorkOrders(CancellationToken ct)
    {
        return await _queryService.GetAllAsync(ct);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<WorkOrderDetailDto>> GetWorkOrder(Guid id, CancellationToken ct)
    {
        var result = await _queryService.GetByIdAsync(id, ct);
        if (result == null) return NotFound();
        return result;
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
            EmployeeId = dto.EmployeeId,
            AgreedRate = dto.AgreedRate,
            CampaignId = dto.CampaignId
        };

        _context.WorkOrders.Add(workOrder);
        await _context.SaveChangesAsync();

        var result = new WorkOrderDto(
            workOrder.Id, workOrder.LotId, workOrder.Description, workOrder.Status,
            workOrder.AssignedTo, workOrder.DueDate, null, workOrder.OTNumber,
            workOrder.PlannedDate, workOrder.ExpirationDate, workOrder.EstimatedCostUSD,
            workOrder.AgreedRate,
            workOrder.StockReserved, workOrder.ContractorId, workOrder.EmployeeId, workOrder.CampaignId);

        return CreatedAtAction(nameof(GetWorkOrder), new { id = workOrder.Id }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateWorkOrder(Guid id, WorkOrderDto dto)
    {
        var workOrder = await _context.WorkOrders.FindAsync(id);
        if (workOrder == null) return NotFound();

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
        workOrder.EmployeeId = dto.EmployeeId;
        workOrder.AgreedRate = dto.AgreedRate;
        workOrder.CampaignId = dto.CampaignId;

        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> ApproveWorkOrder(Guid id)
    {
        var workOrder = await _context.WorkOrders
            .Include(w => w.Labors)
            .FirstOrDefaultAsync(w => w.Id == id);

        if (workOrder == null) return NotFound();
        if (workOrder.Status == "Approved") return BadRequest("La OT ya fue aprobada.");
        if (workOrder.Status == "Cancelled") return BadRequest("No se puede aprobar una OT cancelada.");

        var unrealizedLabors = workOrder.Labors.Where(l => l.Status != "Realized").ToList();
        if (unrealizedLabors.Count > 0)
            return BadRequest($"Hay {unrealizedLabors.Count} labor(es) sin realizar.");

        workOrder.Status = "Approved";
        await _context.SaveChangesAsync();

        var stockValidation = await _stockValidator.ValidateStockForWorkOrderAsync(id);
        if (!stockValidation.IsValid)
        {
            return Ok(new
            {
                Message = "OT Aprobada con advertencia de stock insuficiente.",
                Warnings = stockValidation.Shortages.Select(s => s.ProductName).ToList()
            });
        }

        return Ok(new { Message = "Orden de Trabajo aprobada correctamente." });
    }

    [HttpGet("{id:guid}/discrepancy")]
    public async Task<ActionResult<DiscrepancyReportDto>> GetDiscrepancy(Guid id)
    {
        var workOrder = await _context.WorkOrders
            .AsNoTracking()
            .Include(w => w.Labors).ThenInclude(l => l.Type)
            .Include(w => w.Labors).ThenInclude(l => l.Supplies).ThenInclude(s => s.Supply)
            .FirstOrDefaultAsync(w => w.Id == id);

        if (workOrder == null) return NotFound();

        var laborDiscrepancies = workOrder.Labors
            .Where(l => l.Status == "Realized")
            .Select(l => new LaborDiscrepancyDto(
                l.Id, l.Type?.Name ?? "Labor",
                l.EffectiveArea > 0 ? l.EffectiveArea : l.Hectares,
                l.Supplies.Where(s => s.RealDose.HasValue).Select(s =>
                {
                    var discrepancy = s.PlannedDose > 0
                        ? ((s.RealDose!.Value - s.PlannedDose) / s.PlannedDose) * 100 : 0;
                    return new SupplyDiscrepancyDto(
                        s.Supply?.ItemName ?? "Insumo", s.PlannedDose, s.RealDose!.Value,
                        Math.Round(discrepancy, 2));
                }).ToList()))
            .ToList();

        return new DiscrepancyReportDto(workOrder.Id, workOrder.Description, laborDiscrepancies);
    }

    [HttpPost("{id:guid}/validate-stock")]
    public async Task<ActionResult<object>> ValidateStock(Guid id)
    {
        var result = await _stockValidator.ValidateStockForWorkOrderAsync(id);
        return Ok(new
        {
            result.IsValid,
            Shortages = result.Shortages.Select(s => new { s.ProductId, s.ProductName, s.Available, s.Required, s.Deficit })
        });
    }

    [HttpPost("{id:guid}/reserve-stock")]
    public async Task<IActionResult> ReserveStock(Guid id)
    {
        var validation = await _stockValidator.ValidateStockForWorkOrderAsync(id);
        
        await _stockValidator.ReserveStockAsync(id);

        if (!validation.IsValid)
        {
            return Ok(new 
            { 
                Message = "Stock reservado con advertencias (insuficiente)", 
                Warnings = validation.Shortages 
            });
        }

        return Ok(new { Message = "Stock reservado correctamente" });
    }

    [HttpGet("{id:guid}/export-isoxml")]
    public async Task<IActionResult> ExportIsoXml(Guid id)
    {
        try
        {
            var zipBytes = await _isoXmlExporter.ExportWorkOrderAsIsoXmlAsync(id);
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
        if (workOrder == null) return NotFound();

        _context.WorkOrders.Remove(workOrder);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}
