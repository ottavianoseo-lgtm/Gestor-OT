using GestorOT.Data;
using GestorOT.Shared.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestorOT.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WorkOrdersController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public WorkOrdersController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<List<WorkOrderDto>>> GetWorkOrders()
    {
        var workOrders = await _context.WorkOrders
            .AsNoTracking()
            .Include(w => w.Lot)
            .OrderByDescending(w => w.DueDate)
            .ToListAsync();

        return workOrders.Select(w => new WorkOrderDto(
            w.Id,
            w.LotId,
            w.Description,
            w.Status,
            w.AssignedTo,
            w.DueDate,
            w.Lot?.Name
        )).ToList();
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<WorkOrderDto>> GetWorkOrder(Guid id)
    {
        var workOrder = await _context.WorkOrders
            .AsNoTracking()
            .Include(w => w.Lot)
            .FirstOrDefaultAsync(w => w.Id == id);

        if (workOrder == null)
            return NotFound();

        return new WorkOrderDto(
            workOrder.Id,
            workOrder.LotId,
            workOrder.Description,
            workOrder.Status,
            workOrder.AssignedTo,
            workOrder.DueDate,
            workOrder.Lot?.Name
        );
    }

    [HttpPost]
    public async Task<ActionResult<WorkOrderDto>> CreateWorkOrder(WorkOrderDto dto)
    {
        var workOrder = new WorkOrder
        {
            Id = Guid.NewGuid(),
            LotId = dto.LotId,
            Description = dto.Description,
            Status = dto.Status,
            AssignedTo = dto.AssignedTo,
            DueDate = dto.DueDate
        };

        _context.WorkOrders.Add(workOrder);
        await _context.SaveChangesAsync();

        var result = new WorkOrderDto(
            workOrder.Id,
            workOrder.LotId,
            workOrder.Description,
            workOrder.Status,
            workOrder.AssignedTo,
            workOrder.DueDate,
            null
        );

        return CreatedAtAction(nameof(GetWorkOrder), new { id = workOrder.Id }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateWorkOrder(Guid id, WorkOrderDto dto)
    {
        var workOrder = await _context.WorkOrders.FindAsync(id);
        if (workOrder == null)
            return NotFound();

        workOrder.Description = dto.Description;
        workOrder.Status = dto.Status;
        workOrder.AssignedTo = dto.AssignedTo;
        workOrder.DueDate = dto.DueDate;
        workOrder.LotId = dto.LotId;

        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteWorkOrder(Guid id)
    {
        var workOrder = await _context.WorkOrders.FindAsync(id);
        if (workOrder == null)
            return NotFound();

        _context.WorkOrders.Remove(workOrder);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}
