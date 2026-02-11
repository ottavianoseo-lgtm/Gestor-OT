using GestorOT.Data;
using GestorOT.Shared.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestorOT.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SettlementsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public SettlementsController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<List<ServiceSettlementDto>>> GetSettlements()
    {
        var settlements = await _context.ServiceSettlements
            .AsNoTracking()
            .Include(s => s.WorkOrder)
                .ThenInclude(w => w!.Lot)
            .OrderByDescending(s => s.GeneratedAt)
            .ToListAsync();

        return settlements.Select(s => new ServiceSettlementDto(
            s.Id,
            s.WorkOrderId,
            s.TotalHectares,
            s.TotalAmount,
            s.GeneratedAt,
            s.ErpSyncStatus,
            null,
            s.WorkOrder?.Description,
            s.WorkOrder?.Lot?.Name,
            s.WorkOrder?.AssignedTo
        )).ToList();
    }

    [HttpGet("by-workorder/{workOrderId:guid}")]
    public async Task<ActionResult<ServiceSettlementDto?>> GetByWorkOrder(Guid workOrderId)
    {
        var s = await _context.ServiceSettlements
            .AsNoTracking()
            .Include(s => s.WorkOrder)
                .ThenInclude(w => w!.Lot)
            .FirstOrDefaultAsync(s => s.WorkOrderId == workOrderId);

        if (s == null)
            return Ok((ServiceSettlementDto?)null);

        return new ServiceSettlementDto(
            s.Id,
            s.WorkOrderId,
            s.TotalHectares,
            s.TotalAmount,
            s.GeneratedAt,
            s.ErpSyncStatus,
            null,
            s.WorkOrder?.Description,
            s.WorkOrder?.Lot?.Name,
            s.WorkOrder?.AssignedTo
        );
    }
}
