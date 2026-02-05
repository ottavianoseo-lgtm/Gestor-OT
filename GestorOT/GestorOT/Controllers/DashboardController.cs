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
        var fieldsCount = await _context.Fields.CountAsync();
        var lotsCount = await _context.Lots.CountAsync();
        var pendingOrders = await _context.WorkOrders.CountAsync(w => w.Status == "Pending" || w.Status == "InProgress");
        var completedOrders = await _context.WorkOrders.CountAsync(w => w.Status == "Completed");

        return new DashboardStatsDto
        {
            FieldsCount = fieldsCount,
            LotsCount = lotsCount,
            PendingWorkOrders = pendingOrders,
            CompletedWorkOrders = completedOrders
        };
    }

    [HttpGet("recent-orders")]
    public async Task<ActionResult<List<WorkOrderDto>>> GetRecentOrders()
    {
        var orders = await _context.WorkOrders
            .Include(w => w.Lot)
            .OrderByDescending(w => w.DueDate)
            .Take(10)
            .ToListAsync();

        return orders.Select(w => new WorkOrderDto
        {
            Id = w.Id,
            LotId = w.LotId,
            Description = w.Description,
            Status = w.Status,
            AssignedTo = w.AssignedTo,
            DueDate = w.DueDate,
            LotName = w.Lot?.Name
        }).ToList();
    }
}
