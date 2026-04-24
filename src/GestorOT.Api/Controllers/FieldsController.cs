using GestorOT.Application.Interfaces;
using GestorOT.Domain.Entities;
using GestorOT.Shared.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestorOT.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FieldsController : ControllerBase
{
    private readonly IApplicationDbContext _context;

    public FieldsController(IApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<List<FieldDto>>> GetFields()
    {
        var fields = await _context.Fields
            .AsNoTracking()
            .Include(f => f.Lots)
            .OrderBy(f => f.Name)
            .Select(f => new FieldDto(
                f.Id,
                f.Name,
                f.CreatedAt,
                f.Lots.Select(l => new LotSummaryDto(
                    l.Id,
                    l.Name,
                    l.Status
                )).ToList()
            ))
            .ToListAsync();

        return fields;
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<FieldDto>> GetField(Guid id)
    {
        var field = await _context.Fields
            .AsNoTracking()
            .Include(f => f.Lots)
            .Where(f => f.Id == id)
            .Select(f => new FieldDto(
                f.Id,
                f.Name,
                f.CreatedAt,
                f.Lots.Select(l => new LotSummaryDto(
                    l.Id,
                    l.Name,
                    l.Status
                )).ToList()
            ))
            .FirstOrDefaultAsync();

        if (field == null)
            return NotFound();

        return field;
    }

    [HttpPost]
    public async Task<ActionResult<FieldDto>> CreateField(FieldDto dto)
    {
        var field = new Field
        {
            Id = Guid.NewGuid(),
            Name = dto.Name,
            CreatedAt = DateTime.UtcNow
        };

        _context.Fields.Add(field);
        await _context.SaveChangesAsync();

        var result = new FieldDto(
            field.Id,
            field.Name,
            field.CreatedAt,
            new List<LotSummaryDto>()
        );

        return CreatedAtAction(nameof(GetField), new { id = field.Id }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateField(Guid id, FieldDto dto)
    {
        var field = await _context.Fields.FirstOrDefaultAsync(f => f.Id == id);
        if (field == null)
            return NotFound();

        field.Name = dto.Name;

        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteField(Guid id)
    {
        var field = await _context.Fields.FirstOrDefaultAsync(f => f.Id == id);
        if (field == null)
            return NotFound("El campo no existe.");

        var hasLots = await _context.Lots.AnyAsync(l => l.FieldId == id);
        if (hasLots)
            return BadRequest("No se puede eliminar un campo que todavía tiene lotes asociados. Elimine los lotes primero.");

        _context.Fields.Remove(field);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}
