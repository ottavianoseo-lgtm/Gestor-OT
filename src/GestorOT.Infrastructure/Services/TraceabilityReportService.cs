using GestorOT.Application.Interfaces;
using GestorOT.Domain.Entities;
using GestorOT.Shared;
using GestorOT.Shared.Dtos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GestorOT.Infrastructure.Services;

public class TraceabilityReportService : ITraceabilityReportService
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<TraceabilityReportService> _logger;

    public TraceabilityReportService(
        IApplicationDbContext context,
        ILogger<TraceabilityReportService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<CampaignTraceabilityReportDto> GetCampaignTraceabilityAsync(Guid campaignId, CancellationToken ct = default)
    {
        var campaign = await _context.Campaigns
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == campaignId, ct);

        string campaignName = campaign?.Name ?? "Campaña";

        // 1. Get Campaign Lots to know all lots assigned to this campaign
        var campaignLots = await _context.CampaignLots
            .AsNoTracking()
            .Include(cl => cl.Lot!)
                .ThenInclude(l => l.Field)
            .Include(cl => cl.Rotations)
                .ThenInclude(r => r.ErpActivity)
            .Where(cl => cl.CampaignId == campaignId)
            .ToListAsync(ct);

        var campaignLotMap = campaignLots
            .Where(cl => cl.Lot != null)
            .ToDictionary(cl => cl.LotId, cl => cl);

        var campaignLotIds = campaignLots.Select(cl => cl.LotId).ToHashSet();

        // 2. Fetch all labors related to this campaign
        var labors = await _context.Labors
            .AsNoTracking()
            .Include(l => l.Type)
            .Include(l => l.WorkOrder)
            .Include(l => l.Contact)
            .Include(l => l.ErpActivity)
            .Include(l => l.Lot!)
                .ThenInclude(lot => lot.Field)
            .Include(l => l.Supplies)
                .ThenInclude(s => s.Supply)
            .Where(l =>
                (l.CampaignLot != null && l.CampaignLot.CampaignId == campaignId) ||
                (l.WorkOrder != null && l.WorkOrder.CampaignId == campaignId) ||
                (campaignLotIds.Contains(l.LotId)))
            .OrderBy(l => l.ExecutionDate ?? l.EstimatedDate ?? l.CreatedAt)
            .ToListAsync(ct);

        // Group labors by LotId
        var laborsByLot = labors.GroupBy(l => l.LotId).ToDictionary(g => g.Key, g => g.ToList());

        // Identify all fields involved
        var fieldDict = new Dictionary<Guid, (string Name, List<LotTraceabilityReportDto> Lots)>();

        // Ensure all lots in CampaignLots are represented even if they have 0 labors yet
        foreach (var cl in campaignLots)
        {
            if (cl.Lot == null) continue;
            var lot = cl.Lot;
            var field = lot.Field;
            var fieldId = field?.Id ?? Guid.Empty;
            var fieldName = field?.Name ?? "Campo Sin Nombre";

            if (!fieldDict.TryGetValue(fieldId, out var fieldEntry))
            {
                fieldEntry = (fieldName, new List<LotTraceabilityReportDto>());
                fieldDict[fieldId] = fieldEntry;
            }

            laborsByLot.TryGetValue(lot.Id, out var lotLabors);
            lotLabors ??= new List<Labor>();

            var cropName = cl.Rotations?.FirstOrDefault()?.ErpActivity?.Name;
            var lotReport = BuildLotReport(lot, fieldId, fieldName, cl.ProductiveArea, cropName, lotLabors);
            fieldEntry.Lots.Add(lotReport);
        }

        // Also check if any labor belonged to a lot not yet in campaignLots
        foreach (var (lotId, lotLabors) in laborsByLot)
        {
            if (campaignLotMap.ContainsKey(lotId)) continue; // Already processed
            var firstLabor = lotLabors.FirstOrDefault();
            var lot = firstLabor?.Lot;
            if (lot == null) continue;

            var field = lot.Field;
            var fieldId = field?.Id ?? Guid.Empty;
            var fieldName = field?.Name ?? "Campo Sin Nombre";

            if (!fieldDict.TryGetValue(fieldId, out var fieldEntry))
            {
                fieldEntry = (fieldName, new List<LotTraceabilityReportDto>());
                fieldDict[fieldId] = fieldEntry;
            }

            var cropName = firstLabor?.ErpActivity?.Name;
            var lotReport = BuildLotReport(lot, fieldId, fieldName, lot.CadastralArea, cropName, lotLabors);
            fieldEntry.Lots.Add(lotReport);
        }

        // Build FieldTraceabilityReportDto for each field
        var fieldsReport = new List<FieldTraceabilityReportDto>();
        foreach (var (fieldId, (fieldName, lots)) in fieldDict.OrderBy(f => f.Value.Name))
        {
            var allFieldLabors = lots.SelectMany(l => l.Timeline).ToList();

            // Aggregated labor totals for the field
            var fieldLaborTotals = lots
                .SelectMany(l => l.LaborTotals)
                .GroupBy(lt => lt.LaborTypeId)
                .Select(g => new LaborTypeTotalSummaryDto(
                    g.Key,
                    g.First().LaborTypeName,
                    g.Sum(x => x.TotalCount),
                    g.Sum(x => x.AccumulatedHectares)
                ))
                .OrderByDescending(x => x.AccumulatedHectares)
                .ToList();

            // Aggregated supply totals for the field
            var fieldSupplyTotals = lots
                .SelectMany(l => l.SupplyTotals)
                .GroupBy(st => st.SupplyId)
                .Select(g => new SupplyTotalSummaryDto(
                    g.Key,
                    g.First().SupplyName,
                    g.Sum(x => x.TotalQuantity),
                    g.Average(x => x.AverageDose),
                    g.First().Unit,
                    g.Sum(x => x.ApplicationsCount)
                ))
                .OrderByDescending(x => x.TotalQuantity)
                .ToList();

            fieldsReport.Add(new FieldTraceabilityReportDto(
                fieldId,
                fieldName,
                lots.Count,
                lots.Sum(l => l.ProductiveArea > 0 ? l.ProductiveArea : l.CadastralArea),
                fieldLaborTotals,
                fieldSupplyTotals,
                lots.OrderBy(l => l.LotName).ToList()
            ));
        }

        return new CampaignTraceabilityReportDto(campaignId, campaignName, fieldsReport);
    }

    public async Task<LotTraceabilityReportDto?> GetLotTraceabilityAsync(Guid campaignId, Guid lotId, CancellationToken ct = default)
    {
        var campaignReport = await GetCampaignTraceabilityAsync(campaignId, ct);
        foreach (var f in campaignReport.Fields)
        {
            var match = f.LotsBreakdown.FirstOrDefault(l => l.LotId == lotId);
            if (match != null) return match;
        }
        return null;
    }

    public async Task<FieldTraceabilityReportDto?> GetFieldTraceabilityAsync(Guid campaignId, Guid fieldId, CancellationToken ct = default)
    {
        var campaignReport = await GetCampaignTraceabilityAsync(campaignId, ct);
        return campaignReport.Fields.FirstOrDefault(f => f.FieldId == fieldId);
    }

    private static LotTraceabilityReportDto BuildLotReport(
        Lot lot,
        Guid fieldId,
        string fieldName,
        decimal productiveArea,
        string? cropName,
        List<Labor> labors)
    {
        var timeline = new List<LotLaborTraceDto>();

        foreach (var l in labors.OrderBy(x => x.ExecutionDate ?? x.EstimatedDate ?? x.CreatedAt))
        {
            var suppliesTrace = l.Supplies.Select(s => new LotSupplyTraceDto(
                s.SupplyId,
                s.Supply?.ItemName ?? "Insumo desconocido",
                s.PlannedDose,
                s.RealDose,
                s.PlannedTotal,
                s.RealTotal,
                UnitHelper.CleanDoseUnit(s.UnitOfMeasure) ?? s.Supply?.GetEffectiveUnit() ?? "u"
            )).ToList();

            timeline.Add(new LotLaborTraceDto(
                l.Id,
                l.WorkOrderId,
                l.WorkOrder?.OTNumber,
                l.ExecutionDate ?? l.EstimatedDate ?? l.CreatedAt,
                l.Type?.Name ?? "Labor sin tipo",
                l.Status.ToString(),
                l.Mode.ToString(),
                l.Hectares,
                l.Contact?.FullName ?? l.WorkOrder?.AssignedTo,
                l.ErpActivity?.Name ?? cropName,
                suppliesTrace
            ));
        }

        // Labor Type totals (repetidas)
        var laborTotals = labors
            .GroupBy(l => l.LaborTypeId)
            .Select(g => new LaborTypeTotalSummaryDto(
                g.Key,
                g.First().Type?.Name ?? "Labor sin tipo",
                g.Count(),
                g.Sum(x => x.Hectares)
            ))
            .OrderByDescending(x => x.AccumulatedHectares)
            .ToList();

        // Supply totals (repetidos)
        var allSupplies = labors.SelectMany(l => l.Supplies).ToList();
        var supplyTotals = allSupplies
            .GroupBy(s => s.SupplyId)
            .Select(g =>
            {
                var first = g.First();
                decimal totalQty = g.Sum(x => x.RealTotal ?? x.PlannedTotal);
                decimal avgDose = g.Average(x => x.RealDose ?? x.PlannedDose);
                string unit = UnitHelper.CleanDoseUnit(first.UnitOfMeasure) ?? first.Supply?.GetEffectiveUnit() ?? "u";

                return new SupplyTotalSummaryDto(
                    g.Key,
                    first.Supply?.ItemName ?? "Insumo desconocido",
                    Math.Round(totalQty, 2),
                    Math.Round(avgDose, 2),
                    unit,
                    g.Count()
                );
            })
            .OrderByDescending(x => x.TotalQuantity)
            .ToList();

        return new LotTraceabilityReportDto(
            lot.Id,
            lot.Name,
            fieldId,
            fieldName,
            lot.CadastralArea,
            productiveArea,
            cropName,
            laborTotals,
            supplyTotals,
            timeline
        );
    }
}
