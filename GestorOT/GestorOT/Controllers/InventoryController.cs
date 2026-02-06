using GestorOT.Data;
using GestorOT.Shared.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestorOT.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InventoryController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public InventoryController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<List<InventoryDto>>> GetInventory()
    {
        var items = await _context.Inventories
            .AsNoTracking()
            .OrderBy(i => i.Category)
            .ThenBy(i => i.ItemName)
            .Select(i => new InventoryDto(
                i.Id, i.Category, i.ItemName, i.CurrentStock, i.ReorderLevel,
                i.UnitA ?? "", i.UnitB ?? "", i.ConversionFactor
            ))
            .ToListAsync();

        return items;
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<InventoryDto>> GetItem(Guid id)
    {
        var item = await _context.Inventories
            .AsNoTracking()
            .Where(i => i.Id == id)
            .Select(i => new InventoryDto(
                i.Id, i.Category, i.ItemName, i.CurrentStock, i.ReorderLevel,
                i.UnitA ?? "", i.UnitB ?? "", i.ConversionFactor
            ))
            .FirstOrDefaultAsync();

        if (item == null) return NotFound();
        return item;
    }

    [HttpPost]
    public async Task<ActionResult<InventoryDto>> CreateItem(InventoryDto dto)
    {
        var item = new Inventory
        {
            Id = Guid.NewGuid(),
            Category = dto.Category,
            ItemName = dto.ItemName,
            CurrentStock = dto.CurrentStock,
            ReorderLevel = dto.ReorderLevel,
            UnitA = dto.UnitA,
            UnitB = dto.UnitB,
            ConversionFactor = dto.ConversionFactor > 0 ? dto.ConversionFactor : 1
        };

        _context.Inventories.Add(item);
        await _context.SaveChangesAsync();

        var result = new InventoryDto(
            item.Id, item.Category, item.ItemName, item.CurrentStock, item.ReorderLevel,
            item.UnitA, item.UnitB, item.ConversionFactor
        );

        return CreatedAtAction(nameof(GetItem), new { id = item.Id }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateItem(Guid id, InventoryDto dto)
    {
        var item = await _context.Inventories.FindAsync(id);
        if (item == null) return NotFound();

        item.Category = dto.Category;
        item.ItemName = dto.ItemName;
        item.CurrentStock = dto.CurrentStock;
        item.ReorderLevel = dto.ReorderLevel;
        item.UnitA = dto.UnitA;
        item.UnitB = dto.UnitB;
        item.ConversionFactor = dto.ConversionFactor > 0 ? dto.ConversionFactor : 1;

        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteItem(Guid id)
    {
        var item = await _context.Inventories.FindAsync(id);
        if (item == null) return NotFound();

        _context.Inventories.Remove(item);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}
