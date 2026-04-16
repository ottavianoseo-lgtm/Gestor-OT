using GestorOT.Application.Interfaces;
using GestorOT.Domain.Entities;
using GestorOT.Shared.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestorOT.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WorkOrderStatusesController : ControllerBase
{
    private readonly IApplicationDbContext _context;

    public WorkOrderStatusesController(IApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<List<WorkOrderStatusDto>>> GetStatuses()
    {
        return await _context.WorkOrderStatuses
            .AsNoTracking()
            .OrderBy(s => s.SortOrder)
            .Select(s => new WorkOrderStatusDto(s.Id, s.Name, s.ColorHex, s.IsEditable, s.IsDefault, s.SortOrder))
            .ToListAsync();
    }

    [HttpPost]
    public async Task<ActionResult<WorkOrderStatusDto>> CreateStatus(WorkOrderStatusDto dto)
    {
        var status = new WorkOrderStatus
        {
            Id = Guid.NewGuid(),
            Name = dto.Name,
            ColorHex = dto.ColorHex,
            IsEditable = dto.IsEditable,
            IsDefault = dto.IsDefault,
            SortOrder = dto.SortOrder
        };

        if (status.IsDefault)
        {
            // Reset other defaults for this tenant
            var others = await _context.WorkOrderStatuses.Where(s => s.IsDefault).ToListAsync();
            foreach (var o in others) o.IsDefault = false;
        }

        _context.WorkOrderStatuses.Add(status);
        await _context.SaveChangesAsync();

        dto.Id = status.Id;
        return Ok(dto);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateStatus(Guid id, WorkOrderStatusDto dto)
    {
        var status = await _context.WorkOrderStatuses.FindAsync(id);
        if (status == null) return NotFound();

        status.Name = dto.Name;
        status.ColorHex = dto.ColorHex;
        status.IsEditable = dto.IsEditable;
        status.IsDefault = dto.IsDefault;
        status.SortOrder = dto.SortOrder;

        if (status.IsDefault)
        {
            var others = await _context.WorkOrderStatuses.Where(s => s.Id != id && s.IsDefault).ToListAsync();
            foreach (var o in others) o.IsDefault = false;
        }

        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteStatus(Guid id)
    {
        var status = await _context.WorkOrderStatuses.FindAsync(id);
        if (status == null) return NotFound();

        var used = await _context.WorkOrders.AnyAsync(w => w.WorkOrderStatusId == id);
        if (used) return BadRequest("No se puede eliminar un estado en uso.");

        _context.WorkOrderStatuses.Remove(status);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}
