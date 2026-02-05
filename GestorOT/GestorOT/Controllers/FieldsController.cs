using GestorOT.Data;
using GestorOT.Shared.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestorOT.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FieldsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public FieldsController(ApplicationDbContext context)
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
                f.TotalArea,
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
                f.TotalArea,
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
            TotalArea = dto.TotalArea,
            CreatedAt = DateTime.UtcNow
        };

        _context.Fields.Add(field);
        await _context.SaveChangesAsync();

        var result = new FieldDto(
            field.Id,
            field.Name,
            field.TotalArea,
            field.CreatedAt,
            new List<LotSummaryDto>()
        );

        return CreatedAtAction(nameof(GetField), new { id = field.Id }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateField(Guid id, FieldDto dto)
    {
        var field = await _context.Fields.FindAsync(id);
        if (field == null)
            return NotFound();

        field.Name = dto.Name;
        field.TotalArea = dto.TotalArea;

        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteField(Guid id)
    {
        var field = await _context.Fields.FindAsync(id);
        if (field == null)
            return NotFound();

        _context.Fields.Remove(field);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}
