using System.Text;
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
    private readonly IHtmlLaborExporterService _htmlExporter;

    public WorkOrdersController(
        IApplicationDbContext context,
        IWorkOrderQueryService queryService,
        IStockValidatorService stockValidator,
        IIsoXmlExporterService isoXmlExporter,
        IHtmlLaborExporterService htmlExporter)
    {
        _context = context;
        _queryService = queryService;
        _stockValidator = stockValidator;
        _isoXmlExporter = isoXmlExporter;
        _htmlExporter = htmlExporter;
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
            FieldId = dto.FieldId,
            Description = dto.Description,
            Status = dto.Status,
            AssignedTo = dto.AssignedTo,
            DueDate = dto.DueDate,
            OTNumber = dto.OTNumber ?? string.Empty,
            PlannedDate = dto.PlannedDate ?? dto.DueDate,
            ExpirationDate = dto.ExpirationDate ?? dto.DueDate,
            StockReserved = dto.StockReserved,
            ContractorId = dto.ContractorId,
            ContactId = dto.ContactId,
            CampaignId = dto.CampaignId,
            AcceptsMultiplePeople = dto.AcceptsMultiplePeople,
            AcceptsMultipleDates = dto.AcceptsMultipleDates
        };

        _context.WorkOrders.Add(workOrder);
        await _context.SaveChangesAsync();

        var result = new WorkOrderDto(
            workOrder.Id, workOrder.FieldId, workOrder.Description, workOrder.Status,
            workOrder.AssignedTo, workOrder.DueDate, null, workOrder.OTNumber,
            workOrder.PlannedDate, workOrder.ExpirationDate,
            workOrder.StockReserved, workOrder.ContractorId, workOrder.ContactId, workOrder.CampaignId,
            workOrder.AcceptsMultiplePeople, workOrder.AcceptsMultipleDates);

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
        workOrder.FieldId = dto.FieldId;
        workOrder.OTNumber = dto.OTNumber ?? workOrder.OTNumber;
        workOrder.PlannedDate = dto.PlannedDate ?? workOrder.PlannedDate;
        workOrder.ExpirationDate = dto.ExpirationDate ?? workOrder.ExpirationDate;
        workOrder.StockReserved = dto.StockReserved;
        workOrder.ContractorId = dto.ContractorId;
        workOrder.ContactId = dto.ContactId;
        workOrder.CampaignId = dto.CampaignId;
        workOrder.AcceptsMultiplePeople = dto.AcceptsMultiplePeople;
        workOrder.AcceptsMultipleDates = dto.AcceptsMultipleDates;

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

    [HttpGet("{id:guid}/export-html")]
    public async Task<IActionResult> ExportHtml(Guid id, CancellationToken ct)
    {
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var html = await _htmlExporter.GenerateInteractiveHtmlAsync(id, baseUrl, ct);
        
        if (string.IsNullOrEmpty(html)) return NotFound();

        var bytes = Encoding.UTF8.GetBytes(html);
        return File(bytes, "text/html", $"OT_{id:N}.html");
    }

    [HttpPut("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] Guid statusId)
    {
        var workOrder = await _context.WorkOrders.FindAsync(id);
        if (workOrder == null) return NotFound();

        var status = await _context.WorkOrderStatuses.FindAsync(statusId);
        if (status == null) return BadRequest("Estado no válido.");

        workOrder.WorkOrderStatusId = statusId;
        workOrder.Status = status.Name;

        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpPut("{id:guid}/approvals")]
    public async Task<IActionResult> UpdateApprovals(Guid id, List<WorkOrderSupplyApprovalDto> dtos)
    {
        var workOrder = await _context.WorkOrders
            .Include(w => w.SupplyApprovals)
            .FirstOrDefaultAsync(w => w.Id == id);

        if (workOrder == null) return NotFound();

        foreach (var dto in dtos)
        {
            var approval = workOrder.SupplyApprovals.FirstOrDefault(a => a.Id == dto.Id);
            if (approval == null)
            {
                approval = new WorkOrderSupplyApproval
                {
                    Id = Guid.NewGuid(),
                    WorkOrderId = id,
                    SupplyId = dto.SupplyId
                };
                _context.WorkOrderSupplyApprovals.Add(approval);
            }

            approval.ApprovedWithdrawal = dto.ApprovedWithdrawal;
            approval.WithdrawalCenter = dto.WithdrawalCenter;
            approval.RealTotalUsed = dto.RealTotalUsed;
            // TotalCalculated usually comes from a consolidate logic, but let's allow setting it if needed
            approval.TotalCalculated = dto.TotalCalculated;
        }

        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{id:guid}/consolidate-supplies")]
    public async Task<IActionResult> ConsolidateSupplies(Guid id)
    {
        var workOrder = await _context.WorkOrders
            .Include(w => w.Labors)
                .ThenInclude(l => l.Supplies)
            .Include(w => w.SupplyApprovals)
            .FirstOrDefaultAsync(w => w.Id == id);

        if (workOrder == null) return NotFound();

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
                    WorkOrderId = id,
                    SupplyId = item.SupplyId,
                    ApprovedWithdrawal = item.Total // Default to total
                };
                _context.WorkOrderSupplyApprovals.Add(approval);
            }
            approval.TotalCalculated = item.Total;
        }

        // Cleanup approvals for supplies no longer in labors
        var laborSupplyIds = suppliesInLabors.Select(s => s.SupplyId).ToHashSet();
        var toRemove = workOrder.SupplyApprovals.Where(a => !laborSupplyIds.Contains(a.SupplyId)).ToList();
        _context.WorkOrderSupplyApprovals.RemoveRange(toRemove);

        await _context.SaveChangesAsync();
        return Ok();
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
