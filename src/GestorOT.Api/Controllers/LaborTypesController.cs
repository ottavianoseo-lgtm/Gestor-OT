using GestorOT.Application.Interfaces;
using GestorOT.Domain.Entities;
using GestorOT.Shared.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestorOT.Api.Controllers;

[ApiController]
[Route("api/labor-types")]
[Route("api/[controller]")]
public class LaborTypesController : ControllerBase
{
    private readonly IApplicationDbContext _context;

    public LaborTypesController(IApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<List<LaborTypeDto>>> GetLaborTypes(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 5000)
    {
        var query = _context.LaborTypes.AsNoTracking();

        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(lt => 
                lt.Name.Contains(search) || 
                (lt.Description != null && lt.Description.Contains(search)) ||
                (lt.ExternalErpId != null && lt.ExternalErpId.Contains(search)));
        }

        if (pageSize > 0)
        {
            query = query.Skip((page - 1) * pageSize).Take(pageSize);
        }

        var items = await query
            .OrderBy(lt => lt.Name)
            .Select(lt => new LaborTypeDto(
                lt.Id,
                lt.Name,
                lt.Description,
                lt.ExternalErpId
            ))
            .ToListAsync();

        return items;
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<LaborTypeDto>> GetLaborType(Guid id)
    {
        var item = await _context.LaborTypes
            .AsNoTracking()
            .Where(lt => lt.Id == id)
            .Select(lt => new LaborTypeDto(
                lt.Id,
                lt.Name,
                lt.Description,
                lt.ExternalErpId
            ))
            .FirstOrDefaultAsync();

        if (item == null) return NotFound();
        return item;
    }

    [HttpPost]
    public async Task<ActionResult<LaborTypeDto>> CreateLaborType(LaborTypeDto dto)
    {
        var item = new LaborType
        {
            Id = Guid.NewGuid(),
            Name = dto.Name.Trim(),
            Description = dto.Description,
            ExternalErpId = dto.ExternalErpId
        };

        _context.LaborTypes.Add(item);
        await _context.SaveChangesAsync();

        var result = new LaborTypeDto(item.Id, item.Name, item.Description, item.ExternalErpId);
        return CreatedAtAction(nameof(GetLaborType), new { id = item.Id }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateLaborType(Guid id, LaborTypeDto dto)
    {
        var item = await _context.LaborTypes.FindAsync(id);
        if (item == null) return NotFound();

        item.Name = dto.Name.Trim();
        item.Description = dto.Description;
        item.ExternalErpId = dto.ExternalErpId;

        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteLaborType(Guid id)
    {
        var item = await _context.LaborTypes.FindAsync(id);
        if (item == null) return NotFound();

        var isUsed = await _context.Labors.AnyAsync(l => l.LaborTypeId == id);
        if (isUsed)
        {
            return BadRequest("No se puede eliminar un tipo de labor que ya está en uso por órdenes de trabajo o labores.");
        }

        _context.LaborTypes.Remove(item);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}
