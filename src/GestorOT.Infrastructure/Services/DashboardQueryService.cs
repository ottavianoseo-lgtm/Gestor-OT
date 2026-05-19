using GestorOT.Application.Interfaces;
using GestorOT.Application.Services;
using GestorOT.Shared.Dtos;
using Microsoft.EntityFrameworkCore;

namespace GestorOT.Infrastructure.Services;

public class DashboardQueryService : IDashboardQueryService
{
    private readonly IApplicationDbContext _context;
    private readonly ICampaignContextService _campaignContext;

    public DashboardQueryService(IApplicationDbContext context, ICampaignContextService campaignContext)
    {
        _context = context;
        _campaignContext = campaignContext;
    }

    public async Task<DashboardStatsDto> GetStatsAsync(CancellationToken ct = default)
    {
        var campaignId = _campaignContext.CurrentCampaignId;
        var hasCampaign = campaignId.HasValue && campaignId != Guid.Empty;

        // 1 query: Fields count — via CampaignFields when campaign is selected
        var fieldsCount = hasCampaign
            ? await _context.CampaignFields
                .Where(cf => cf.CampaignId == campaignId)
                .Select(cf => cf.FieldId)
                .Distinct()
                .CountAsync(CancellationToken.None)
            : await _context.Fields.CountAsync(CancellationToken.None);

        // 1 query: Total productive area — filtered by campaign
        var productiveQuery = _context.CampaignLots.AsNoTracking().AsQueryable();
        if (hasCampaign)
            productiveQuery = productiveQuery.Where(cl => cl.CampaignId == campaignId);
        var totalProductiveArea = await productiveQuery.SumAsync(cl => cl.ProductiveArea, CancellationToken.None);

        // 1 query: Lots total + active — via CampaignLots when campaign is selected
        int lotsTotal, lotsActive;
        if (hasCampaign)
        {
            var campaignLotIds = _context.CampaignLots
                .Where(cl => cl.CampaignId == campaignId)
                .Select(cl => cl.LotId);

            lotsTotal = await campaignLotIds.CountAsync(CancellationToken.None);
            lotsActive = await _context.Lots
                .Where(l => campaignLotIds.Contains(l.Id) && l.Status == "Active")
                .CountAsync(CancellationToken.None);
        }
        else
        {
            var lotStats = await _context.Lots
                .AsNoTracking()
                .GroupBy(_ => 1)
                .Select(g => new { Total = g.Count(), Active = g.Count(l => l.Status == "Active") })
                .FirstOrDefaultAsync(CancellationToken.None);
            lotsTotal = lotStats?.Total ?? 0;
            lotsActive = lotStats?.Active ?? 0;
        }

        // 1 query: WorkOrders grouped by status — filtered by campaign
        var woQuery = _context.WorkOrders.AsNoTracking().AsQueryable();
        if (hasCampaign)
            woQuery = woQuery.Where(w => w.CampaignId == campaignId);
        var workOrderCounts = await woQuery
            .GroupBy(w => w.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(CancellationToken.None);

        var pendingOrders    = workOrderCounts.FirstOrDefault(x => x.Status == "Pending")?.Count ?? 0;
        var inProgressOrders = workOrderCounts.FirstOrDefault(x => x.Status == "InProgress")?.Count ?? 0;
        var completedOrders  = workOrderCounts.FirstOrDefault(x => x.Status == "Completed")?.Count ?? 0;

        return new DashboardStatsDto(
            fieldsCount,
            lotsTotal,
            lotsActive,
            pendingOrders,
            inProgressOrders,
            completedOrders,
            totalProductiveArea);
    }

    public async Task<List<RecentWorkOrderDto>> GetRecentOrdersAsync(int count = 10, CancellationToken ct = default)
    {
        var campaignId = _campaignContext.CurrentCampaignId;
        var hasCampaign = campaignId.HasValue && campaignId != Guid.Empty;

        var query = _context.WorkOrders
            .AsNoTracking()
            .Include(w => w.Field)
            .AsQueryable();

        if (hasCampaign)
            query = query.Where(w => w.CampaignId == campaignId);

        return await query
            .OrderByDescending(w => w.DueDate)
            .Take(count)
            .Select(w => new RecentWorkOrderDto(
                w.Id,
                w.Description,
                w.Status,
                w.AssignedTo,
                w.DueDate,
                w.Field != null ? w.Field.Name : null
            ))
            .ToListAsync(CancellationToken.None);
    }
}
