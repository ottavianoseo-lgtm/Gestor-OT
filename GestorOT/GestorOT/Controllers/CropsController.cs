using GestorOT.Data;
using GestorOT.Shared.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestorOT.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CropsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public CropsController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<List<CropDto>>> GetCrops()
    {
        var crops = await _context.Crops
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new CropDto(c.Id, c.Name, c.Type, c.CreatedAt))
            .ToListAsync();

        return crops;
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CropDto>> GetCrop(Guid id)
    {
        var c = await _context.Crops
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);

        if (c == null)
            return NotFound();

        return new CropDto(c.Id, c.Name, c.Type, c.CreatedAt);
    }

    [HttpPost]
    public async Task<ActionResult<CropDto>> CreateCrop(CropDto dto)
    {
        var crop = new Crop
        {
            Id = Guid.NewGuid(),
            Name = dto.Name,
            Type = dto.Type,
            CreatedAt = DateTime.UtcNow
        };

        _context.Crops.Add(crop);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetCrop), new { id = crop.Id },
            new CropDto(crop.Id, crop.Name, crop.Type, crop.CreatedAt));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateCrop(Guid id, CropDto dto)
    {
        var crop = await _context.Crops.FindAsync(id);
        if (crop == null)
            return NotFound();

        crop.Name = dto.Name;
        crop.Type = dto.Type;

        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteCrop(Guid id)
    {
        var crop = await _context.Crops.FindAsync(id);
        if (crop == null)
            return NotFound();

        var inUse = await _context.CampaignPlots.AnyAsync(cp => cp.CropId == id);
        if (inUse)
            return BadRequest("No se puede eliminar un cultivo que está asignado a lotes de campaña.");

        _context.Crops.Remove(crop);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}
