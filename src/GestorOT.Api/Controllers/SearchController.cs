using GestorOT.Application.Interfaces;
using GestorOT.Shared.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestorOT.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SearchController : ControllerBase
{
    private readonly IApplicationDbContext _context;

    public SearchController(IApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<SearchResult>> Search([FromQuery] string q, [FromQuery] Guid? campaignId = null)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
            return Ok(new SearchResult());

        var term = q.ToLower();

        var workOrders = await _context.WorkOrders
            .AsNoTracking()
            .Include(w => w.Field)
            .Include(w => w.WorkOrderStatus)
            .Where(w => (w.Description != null && w.Description.ToLower().Contains(term))
                     || (w.OTNumber != null && w.OTNumber.ToLower().Contains(term))
                     || (w.AssignedTo != null && w.AssignedTo.ToLower().Contains(term))
                     || (w.Name != null && w.Name.ToLower().Contains(term)))
            .Take(10)
            .Select(w => new WorkOrderDto(
                w.Id, w.FieldId, w.Description, w.Status, w.AssignedTo, w.DueDate,
                w.Field != null ? w.Field.Name : null, w.OTNumber, w.PlannedDate, w.ExpirationDate,
                w.StockReserved, w.ContractorId, w.ContactId, w.CampaignId,
                w.Name, w.AcceptsMultiplePeople, w.AcceptsMultipleDates,
                w.WorkOrderStatus != null && !w.WorkOrderStatus.IsEditable))
            .ToListAsync();

        var labors = await _context.Labors
            .AsNoTracking()
            .Include(l => l.Lot)
            .Include(l => l.Type)
            .Include(l => l.Contact)
            .Where(l => (l.Notes != null && l.Notes.ToLower().Contains(term))
                     || (l.Lot != null && l.Lot.Name.ToLower().Contains(term))
                     || (l.Type != null && l.Type.Name.ToLower().Contains(term))
                     || (l.Contact != null && l.Contact.FullName.ToLower().Contains(term)))
            .Take(10)
            .Select(l => new LaborDto(
                l.Id, l.WorkOrderId, l.LotId, l.CampaignLotId ?? Guid.Empty,
                l.LaborTypeId, l.ErpActivityId, l.Status.ToString(), l.Mode.ToString(),
                l.ExecutionDate, l.EstimatedDate, l.Hectares, l.EffectiveArea, l.CreatedAt,
                l.Rate, l.RateUnit, l.Lot != null ? l.Lot.Name : null,
                l.Type != null ? l.Type.Name : null, null, new List<LaborSupplyDto>(),
                l.PrescriptionMapUrl, l.MachineryUsedId, l.WeatherLogJson, l.Notes,
                null, l.PlannedDose, l.RealizedDose, l.ContactId, l.IsExternalBilling,
                l.PlannedLaborId, l.Priority, l.SupplyWithdrawalNotes, l.IsOriginalPlan,
                null, null))
            .ToListAsync();

        var lots = await _context.Lots
            .AsNoTracking()
            .Include(l => l.Field)
            .Where(l => l.Name.ToLower().Contains(term)
                     || (l.Field != null && l.Field.Name.ToLower().Contains(term)))
            .Take(10)
            .Select(l => new LotDto(
                l.Id, l.FieldId, l.Name, l.Status, null, l.Field != null ? l.Field.Name : null, 0, l.CadastralArea))
            .ToListAsync();

        return Ok(new SearchResult(workOrders, labors, lots, workOrders.Count + labors.Count + lots.Count));
    }
}
