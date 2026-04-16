using GestorOT.Application.Interfaces;
using GestorOT.Application.Services;
using GestorOT.Shared.Dtos;
using Microsoft.EntityFrameworkCore;

namespace GestorOT.Infrastructure.Services;

public class DashboardQueryService : IDashboardQueryService
{
    private readonly IApplicationDbContext _context;

    public DashboardQueryService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<DashboardStatsDto> GetStatsAsync(CancellationToken ct = default)
    {
        // 1 query: Fields count
        var fieldsCount = await _context.Fields.CountAsync(CancellationToken.None);

        // 1 query: Total productive area from all lots in the context (using CampaignLots)
        var totalProductiveArea = await _context.CampaignLots
            .AsNoTracking()
            .SumAsync(cl => cl.ProductiveArea, CancellationToken.None);

        // 1 query: Lots total + active
        var lotStats = await _context.Lots
            .AsNoTracking()
            .GroupBy(_ => 1)
            .Select(g => new { Total = g.Count(), Active = g.Count(l => l.Status == "Active") })
            .FirstOrDefaultAsync(CancellationToken.None);

        // 1 query: WorkOrders grouped by status
        var workOrderCounts = await _context.WorkOrders
            .AsNoTracking()
            .GroupBy(w => w.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(CancellationToken.None);

        var pendingOrders    = workOrderCounts.FirstOrDefault(x => x.Status == "Pending")?.Count ?? 0;
        var inProgressOrders = workOrderCounts.FirstOrDefault(x => x.Status == "InProgress")?.Count ?? 0;
        var completedOrders  = workOrderCounts.FirstOrDefault(x => x.Status == "Completed")?.Count ?? 0;

        return new DashboardStatsDto(
            fieldsCount,
            lotStats?.Total ?? 0,
            lotStats?.Active ?? 0,
            pendingOrders,
            inProgressOrders,
            completedOrders,
            totalProductiveArea);
    }

    public async Task<List<RecentWorkOrderDto>> GetRecentOrdersAsync(int count = 10, CancellationToken ct = default)
    {
        return await _context.WorkOrders
            .AsNoTracking()
            .Include(w => w.Field)
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
