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
        var fieldsCount = await _context.Fields.AsNoTracking().CountAsync(ct);
        var lotsCount = await _context.Lots.AsNoTracking().CountAsync(ct);
        var activeLotsCount = await _context.Lots.AsNoTracking().CountAsync(l => l.Status == "Active", ct);
        var pendingOrders = await _context.WorkOrders.AsNoTracking().CountAsync(w => w.Status == "Pending", ct);
        var inProgressOrders = await _context.WorkOrders.AsNoTracking().CountAsync(w => w.Status == "InProgress", ct);
        var completedOrders = await _context.WorkOrders.AsNoTracking().CountAsync(w => w.Status == "Completed", ct);
        var totalArea = await _context.Fields.AsNoTracking().SumAsync(f => f.HectareasTotales, ct);

        return new DashboardStatsDto(fieldsCount, lotsCount, activeLotsCount, pendingOrders, inProgressOrders, completedOrders, totalArea);
    }

    public async Task<List<RecentWorkOrderDto>> GetRecentOrdersAsync(int count = 10, CancellationToken ct = default)
    {
        return await _context.WorkOrders
            .AsNoTracking()
            .Include(w => w.Lot)
            .ThenInclude(l => l!.Field)
            .OrderByDescending(w => w.DueDate)
            .Take(count)
            .Select(w => new RecentWorkOrderDto(
                w.Id,
                w.Description,
                w.Status,
                w.AssignedTo,
                w.DueDate,
                w.Lot != null ? w.Lot.Name : null,
                w.Lot != null && w.Lot.Field != null ? w.Lot.Field.Name : null
            ))
            .ToListAsync(ct);
    }
}
