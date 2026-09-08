using GestorOT.Application.Interfaces;
using GestorOT.Domain.Entities;
using GestorOT.Shared.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestorOT.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InventoryController : ControllerBase
{
    private readonly IApplicationDbContext _context;
    private readonly IErpSyncService _erpSyncService;
    private readonly ICurrentTenantService _currentTenantService;

    public InventoryController(
        IApplicationDbContext context,
        IErpSyncService erpSyncService,
        ICurrentTenantService currentTenantService)
    {
        _context = context;
        _erpSyncService = erpSyncService;
        _currentTenantService = currentTenantService;
    }

    [HttpGet]
    public async Task<ActionResult<List<InventoryDto>>> GetInventory(
        [FromQuery] string? search, 
        [FromQuery] int page = 1, 
        [FromQuery] int pageSize = 5000)
    {
        var query = _context.Inventories.AsNoTracking();

        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(i => i.ItemName.Contains(search) || i.Category.Contains(search));
        }

        if (pageSize > 0)
        {
            query = query.Skip((page - 1) * pageSize).Take(pageSize);
        }

        var items = await query
            .OrderBy(i => i.Category)
            .ThenBy(i => i.ItemName)
            .Select(i => new InventoryDto(
                i.Id, i.Category, i.ItemName, i.CurrentStock, i.ReorderLevel,
                i.UnitA ?? "", i.UnitB ?? "", i.ConversionFactor,
                i.GrupoConcepto, i.SubGrupoConcepto
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
                i.UnitA ?? "", i.UnitB ?? "", i.ConversionFactor,
                i.GrupoConcepto, i.SubGrupoConcepto
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

    [HttpGet("erp-groups")]
    public async Task<ActionResult<List<ErpGroupSummaryDto>>> GetErpGroups(CancellationToken ct)
    {
        var groups = await _erpSyncService.GetErpInventoryGroupsAsync(_currentTenantService.TenantId, ct);
        return Ok(groups);
    }

    [HttpPost("sync-groups")]
    public async Task<ActionResult<SyncInventoryGroupsResultDto>> SyncGroups(
        [FromBody] SyncInventoryGroupsRequestDto request, 
        CancellationToken ct)
    {
        if (request.SelectedGroups == null || !request.SelectedGroups.Any())
        {
            return BadRequest("Debe seleccionar al menos un grupo para sincronizar.");
        }

        var result = await _erpSyncService.SyncInventoryWithGroupsAsync(
            _currentTenantService.TenantId, 
            request.SelectedGroups, 
            request.CleanUnselected, 
            ct);

        return Ok(result);
    }
}
