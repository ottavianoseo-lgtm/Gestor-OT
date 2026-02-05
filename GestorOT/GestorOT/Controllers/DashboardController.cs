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
        var pendingOrders = await _context.WorkOrders.AsNoTracking().CountAsync(w => w.Status == "Pending" || w.Status == "InProgress");
        var completedOrders = await _context.WorkOrders.AsNoTracking().CountAsync(w => w.Status == "Completed");

        return new DashboardStatsDto(fieldsCount, lotsCount, pendingOrders, completedOrders);
    }

    [HttpGet("recent-orders")]
    public async Task<ActionResult<List<WorkOrderDto>>> GetRecentOrders()
    {
        var orders = await _context.WorkOrders
            .AsNoTracking()
            .Include(w => w.Lot)
            .OrderByDescending(w => w.DueDate)
            .Take(10)
            .Select(w => new WorkOrderDto(
                w.Id,
                w.LotId,
                w.Description,
                w.Status,
                w.AssignedTo,
                w.DueDate,
                w.Lot != null ? w.Lot.Name : null
            ))
            .ToListAsync();

        return orders;
    }
}
