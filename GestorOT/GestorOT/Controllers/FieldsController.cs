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
            .Include(f => f.Lots)
            .OrderBy(f => f.Name)
            .ToListAsync();

        return fields.Select(f => new FieldDto
        {
            Id = f.Id,
            Name = f.Name,
            TotalArea = f.TotalArea,
            CreatedAt = f.CreatedAt,
            Lots = f.Lots.Select(l => new LotDto
            {
                Id = l.Id,
                FieldId = l.FieldId,
                Name = l.Name,
                Status = l.Status
            }).ToList()
        }).ToList();
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<FieldDto>> GetField(Guid id)
    {
        var field = await _context.Fields
            .Include(f => f.Lots)
            .FirstOrDefaultAsync(f => f.Id == id);

        if (field == null)
            return NotFound();

        return new FieldDto
        {
            Id = field.Id,
            Name = field.Name,
            TotalArea = field.TotalArea,
            CreatedAt = field.CreatedAt,
            Lots = field.Lots.Select(l => new LotDto
            {
                Id = l.Id,
                FieldId = l.FieldId,
                Name = l.Name,
                Status = l.Status
            }).ToList()
        };
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

        dto.Id = field.Id;
        dto.CreatedAt = field.CreatedAt;

        return CreatedAtAction(nameof(GetField), new { id = field.Id }, dto);
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
