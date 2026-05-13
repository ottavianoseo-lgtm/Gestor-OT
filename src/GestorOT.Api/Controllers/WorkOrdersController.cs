using System.Text;
using GestorOT.Application.Interfaces;
using GestorOT.Application.Services;
using GestorOT.Domain.Entities;
using GestorOT.Domain.Enums;
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

    [HttpGet("paged")]
    public async Task<ActionResult<PagedResult<WorkOrderDto>>> GetWorkOrdersPaged(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        return await _queryService.GetPagedAsync(page, pageSize, ct);
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
        if (dto.CampaignId == null || dto.CampaignId == Guid.Empty)
            return BadRequest("La orden de trabajo debe pertenecer a una campaña.");

        var campaign = await _context.Campaigns
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == dto.CampaignId.Value);

        if (campaign == null)
            return BadRequest("La campaña seleccionada no existe.");

        if (campaign.Status == "Locked")
            return BadRequest("No se pueden crear órdenes en una campaña bloqueada.");

        WorkOrderStatus? finalStatus = null;

        if (dto.WorkOrderStatusId.HasValue && dto.WorkOrderStatusId != Guid.Empty)
        {
            finalStatus = await _context.WorkOrderStatuses
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == dto.WorkOrderStatusId.Value);

            if (finalStatus == null)
                return BadRequest("El estado seleccionado no existe. Verifique la configuración de estados de OT.");
        }
        else if (!string.IsNullOrWhiteSpace(dto.Status))
        {
            finalStatus = await _context.WorkOrderStatuses
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Name == dto.Status);
        }

        finalStatus ??= await _context.WorkOrderStatuses
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.IsDefault);

        finalStatus ??= await _context.WorkOrderStatuses
            .AsNoTracking()
            .OrderBy(s => s.SortOrder)
            .FirstOrDefaultAsync();

        if (finalStatus == null)
            return BadRequest("No se encontró un estado válido para la orden de trabajo. Configure al menos un estado de OT en la sección de administración.");

        var workOrder = new WorkOrder
        {
            Id = Guid.NewGuid(),
            Name = dto.Name,
            Description = dto.Description,
            Status = finalStatus.Name,
            WorkOrderStatusId = finalStatus.Id,
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
            workOrder.Name, workOrder.AcceptsMultiplePeople, workOrder.AcceptsMultipleDates,
            workOrderStatusId: workOrder.WorkOrderStatusId);

        return CreatedAtAction(nameof(GetWorkOrder), new { id = workOrder.Id }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateWorkOrder(Guid id, WorkOrderDto dto)
    {
        var workOrder = await _context.WorkOrders
            .Include(w => w.WorkOrderStatus)
            .Include(w => w.Campaign)
            .FirstOrDefaultAsync(w => w.Id == id);
        if (workOrder == null) return NotFound();

        bool isStatusChanging = false;
        if (dto.WorkOrderStatusId.HasValue && dto.WorkOrderStatusId != Guid.Empty && dto.WorkOrderStatusId != workOrder.WorkOrderStatusId)
            isStatusChanging = true;
        else if (!string.IsNullOrWhiteSpace(dto.Status) && dto.Status != workOrder.Status)
            isStatusChanging = true;

        if (workOrder.WorkOrderStatus?.IsEditable == false && !isStatusChanging)
            return Conflict("La OT se encuentra en un estado que no permite modificaciones.");

        if (workOrder.Campaign?.Status == "Locked")
            return Conflict("No se pueden modificar órdenes de una campaña bloqueada.");

        workOrder.Name = dto.Name;
        workOrder.Description = dto.Description;

        if (dto.WorkOrderStatusId.HasValue && dto.WorkOrderStatusId != Guid.Empty
            && dto.WorkOrderStatusId != workOrder.WorkOrderStatusId)
        {
            var status = await _context.WorkOrderStatuses
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == dto.WorkOrderStatusId.Value);
            if (status == null)
                return BadRequest("El estado seleccionado no es válido.");
            workOrder.Status = status.Name;
            workOrder.WorkOrderStatusId = status.Id;
        }
        else if (!string.IsNullOrWhiteSpace(dto.Status) && dto.Status != workOrder.Status)
        {
            var status = await _context.WorkOrderStatuses
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Name == dto.Status);
            if (status == null)
                return BadRequest($"El estado '{dto.Status}' no es válido.");
            workOrder.Status = status.Name;
            workOrder.WorkOrderStatusId = status.Id;
        }
        workOrder.AssignedTo = dto.AssignedTo;
        workOrder.DueDate = dto.DueDate;
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
            .Include(w => w.WorkOrderStatus)
            .Include(w => w.Campaign)
            .FirstOrDefaultAsync(w => w.Id == id);

        if (workOrder == null) return NotFound();
        if (workOrder.WorkOrderStatus?.IsEditable == false)
            return Conflict("La OT se encuentra en un estado que no permite modificaciones.");
        if (workOrder.Campaign?.Status == "Locked")
            return Conflict("No se puede aprobar una OT de una campaña bloqueada.");

        var unrealizedLabors = workOrder.Labors.Where(l => l.Status != LaborStatus.Realized).ToList();
        if (unrealizedLabors.Count > 0)
            return BadRequest($"Hay {unrealizedLabors.Count} labor(es) sin realizar.");

        var approvedStatus = await _context.WorkOrderStatuses
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Name == "Approved");

        if (approvedStatus == null)
        {
            approvedStatus = await _context.WorkOrderStatuses
                .AsNoTracking()
                .FirstOrDefaultAsync(s => !s.IsEditable);
        }

        if (approvedStatus == null)
            return BadRequest("No se encontró un estado aprobado configurado. Configure al menos un estado no editable como 'Aprobado' o 'Cerrado' en la administración de estados de OT.");

        workOrder.WorkOrderStatusId = approvedStatus.Id;
        workOrder.Status = approvedStatus.Name;
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
            .Where(l => l.Status == LaborStatus.Realized)
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
        var workOrder = await _context.WorkOrders
            .Include(w => w.WorkOrderStatus)
            .Include(w => w.Campaign)
            .FirstOrDefaultAsync(w => w.Id == id);

        if (workOrder == null) return NotFound();
        if (workOrder.WorkOrderStatus?.IsEditable == false)
            return Conflict("La OT se encuentra en un estado que no permite modificaciones.");
        if (workOrder.Campaign?.Status == "Locked")
            return Conflict("No se pueden modificar órdenes de una campaña bloqueada.");

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
        var workOrder = await _context.WorkOrders
            .Include(w => w.WorkOrderStatus)
            .Include(w => w.Campaign)
            .FirstOrDefaultAsync(w => w.Id == id);
        if (workOrder == null) return NotFound();


        if (workOrder.Campaign?.Status == "Locked")
            return Conflict("No se pueden modificar órdenes de una campaña bloqueada.");

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
        var workOrder = await _context.WorkOrders
            .Include(w => w.WorkOrderStatus)
            .Include(w => w.Campaign)
            .FirstOrDefaultAsync(w => w.Id == id);

        if (workOrder == null) return NotFound();
        if (workOrder.WorkOrderStatus?.IsEditable == false)
            return BadRequest("La OT se encuentra en un estado que no permite eliminación.");
        if (workOrder.Campaign?.Status == "Locked")
            return BadRequest("No se pueden eliminar órdenes de una campaña bloqueada.");

        _context.WorkOrders.Remove(workOrder);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("export-csv")]
    public async Task<IActionResult> ExportCsv(CancellationToken ct)
    {
        var list = await _queryService.GetAllAsync(ct);
        var lines = new List<string>
        {
            "NroOT,Nombre,Descripcion,Estado,Asignado,Fecha,ContratistaID,CampaniaID"
        };
        foreach (var wo in list)
        {
            lines.Add($"{wo.OTNumber},{wo.Name},{wo.Description},{wo.Status},{wo.AssignedTo},{wo.DueDate:yyyy-MM-dd},{wo.ContractorId},{wo.CampaignId}");
        }
        var bytes = System.Text.Encoding.UTF8.GetBytes(string.Join("\r\n", lines));
        return File(bytes, "text/csv", $"ordenes-trabajo-{DateTime.Today:yyyyMMdd}.csv");
    }

    [HttpGet("campaign-deviations/{campaignId:guid}")]
    public async Task<ActionResult<CampaignDeviationReport>> GetCampaignDeviations(Guid campaignId)
    {
        var workOrders = await _context.WorkOrders
            .AsNoTracking()
            .Include(w => w.Labors)
                .ThenInclude(l => l.Supplies)
                    .ThenInclude(s => s.Supply)
            .Where(w => w.CampaignId == campaignId)
            .ToListAsync();

        var report = new CampaignDeviationReport
        {
            TotalWorkOrders = workOrders.Count,
            TotalLabors = workOrders.Sum(w => w.Labors.Count),
            RealizedLabors = workOrders.Sum(w => w.Labors.Count(l => l.Status == GestorOT.Domain.Enums.LaborStatus.Realized)),
            PlannedHectares = workOrders.Sum(w => w.Labors.Sum(l => l.Hectares)),
            RealizedHectares = workOrders.Sum(w => w.Labors.Where(l => l.Status == GestorOT.Domain.Enums.LaborStatus.Realized).Sum(l => l.Hectares))
        };

        var allSupplies = workOrders.SelectMany(w => w.Labors).SelectMany(l => l.Supplies);
        var supplyGroups = allSupplies.GroupBy(s => s.SupplyId);
        foreach (var group in supplyGroups)
        {
            var first = group.First();
            report.SupplySummary.Add(new SupplySummary
            {
                SupplyName = first.Supply?.ItemName ?? "Desconocido",
                PlannedTotal = group.Sum(s => s.PlannedTotal),
                RealTotal = group.Sum(s => s.RealTotal ?? 0),
                DeviationPercent = group.Sum(s => s.PlannedTotal) > 0
                    ? ((group.Sum(s => s.RealTotal ?? 0) - group.Sum(s => s.PlannedTotal)) / group.Sum(s => s.PlannedTotal)) * 100
                    : 0,
                Unit = first.UnitOfMeasure
            });
        }

        foreach (var wo in workOrders)
        {
            var planHa = wo.Labors.Sum(l => l.Hectares);
            var realHa = wo.Labors.Where(l => l.Status == GestorOT.Domain.Enums.LaborStatus.Realized).Sum(l => l.Hectares);
            report.WorkOrderDeviations.Add(new WorkOrderDeviation
            {
                WorkOrderId = wo.Id,
                Description = wo.Description ?? "",
                PlanHa = planHa,
                RealHa = realHa,
                HaDeviationPercent = planHa > 0 ? ((realHa - planHa) / planHa) * 100 : 0,
                TotalLabors = wo.Labors.Count,
                RealizedLabors = wo.Labors.Count(l => l.Status == GestorOT.Domain.Enums.LaborStatus.Realized)
            });
        }

        return Ok(report);
    }
}
