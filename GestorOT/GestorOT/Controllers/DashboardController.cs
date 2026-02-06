using GestorOT.Data;
using GestorOT.Shared.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestorOT.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DashboardController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public DashboardController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet("stats")]
    public async Task<ActionResult<DashboardStatsDto>> GetStats()
    {
        var fieldsCount = await _context.Fields.AsNoTracking().CountAsync();
        var lotsCount = await _context.Lots.AsNoTracking().CountAsync();
        var activeLotsCount = await _context.Lots.AsNoTracking().CountAsync(l => l.Status == "Active");
        var pendingOrders = await _context.WorkOrders.AsNoTracking().CountAsync(w => w.Status == "Pending");
        var inProgressOrders = await _context.WorkOrders.AsNoTracking().CountAsync(w => w.Status == "InProgress");
        var completedOrders = await _context.WorkOrders.AsNoTracking().CountAsync(w => w.Status == "Completed");
        var totalArea = await _context.Fields.AsNoTracking().SumAsync(f => f.TotalArea);

        return new DashboardStatsDto(fieldsCount, lotsCount, activeLotsCount, pendingOrders, inProgressOrders, completedOrders, totalArea);
    }

    [HttpGet("recent-orders")]
    public async Task<ActionResult<List<RecentWorkOrderDto>>> GetRecentOrders()
    {
        var orders = await _context.WorkOrders
            .AsNoTracking()
            .Include(w => w.Lot)
            .ThenInclude(l => l!.Field)
            .OrderByDescending(w => w.DueDate)
            .Take(10)
            .Select(w => new RecentWorkOrderDto(
                w.Id,
                w.Description,
                w.Status,
                w.AssignedTo,
                w.DueDate,
                w.Lot != null ? w.Lot.Name : null,
                w.Lot != null && w.Lot.Field != null ? w.Lot.Field.Name : null
            ))
            .ToListAsync();

        return orders;
    }
}
