using System.Text.Json;
using GestorOT.Application.Interfaces;
using GestorOT.Domain.Entities;
using GestorOT.Domain.Enums;
using GestorOT.Shared;
using GestorOT.Shared.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestorOT.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StrategiesController : ControllerBase
{
    private readonly IApplicationDbContext _context;

    public StrategiesController(IApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<List<CropStrategyDto>>> GetStrategies()
    {
        var strategies = await _context.CropStrategies
            .AsNoTracking()
            .Include(s => s.Activity)
            .Include(s => s.Items)
                .ThenInclude(i => i.LaborType)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();

        var supplyIds = strategies
            .SelectMany(s => s.Items)
            .SelectMany(i => DeserializeSupplies(i.DefaultSuppliesJson))
            .Where(sd => sd.SupplyId != Guid.Empty)
            .Select(sd => sd.SupplyId)
            .Distinct()
            .ToList();

        var inventoryMap = new Dictionary<Guid, string>();
        if (supplyIds.Any())
        {
            var inventories = await _context.Inventories
                .AsNoTracking()
                .Where(inv => supplyIds.Contains(inv.Id))
                .Select(inv => new { inv.Id, inv.ItemName })
                .ToListAsync();
            inventoryMap = inventories.ToDictionary(x => x.Id, x => x.ItemName);
        }

        return strategies.Select(s => MapToDto(s, inventoryMap)).ToList();
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CropStrategyDto>> GetStrategy(Guid id)
    {
        var strategy = await _context.CropStrategies
            .AsNoTracking()
            .Include(s => s.Activity)
            .Include(s => s.Items)
                .ThenInclude(i => i.LaborType)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (strategy == null)
            return NotFound();

        var supplyIds = strategy.Items
            .SelectMany(i => DeserializeSupplies(i.DefaultSuppliesJson))
            .Where(sd => sd.SupplyId != Guid.Empty)
            .Select(sd => sd.SupplyId)
            .Distinct()
            .ToList();

        var inventoryMap = new Dictionary<Guid, string>();
        if (supplyIds.Any())
        {
            var inventories = await _context.Inventories
                .AsNoTracking()
                .Where(inv => supplyIds.Contains(inv.Id))
                .Select(inv => new { inv.Id, inv.ItemName })
                .ToListAsync();
            inventoryMap = inventories.ToDictionary(x => x.Id, x => x.ItemName);
        }

        return MapToDto(strategy, inventoryMap);
    }

    [HttpPost]
    public async Task<ActionResult<CropStrategyDto>> CreateStrategy(CropStrategyDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            return BadRequest("El nombre es obligatorio.");

        if (dto.ErpActivityId == Guid.Empty)
            return BadRequest("La actividad ERP es obligatoria.");

        if (dto.Items == null || dto.Items.Count == 0)
            return BadRequest("Debe definir al menos una labor.");

        foreach (var item in dto.Items)
        {
            if (item.LaborTypeId == Guid.Empty)
                return BadRequest("Cada labor debe tener un tipo de labor definido.");
        }

        var strategy = new CropStrategy
        {
            Id = Guid.NewGuid(),
            Name = dto.Name,
            ErpActivityId = dto.ErpActivityId,
            CreatedAt = DateTime.UtcNow
        };

        if (dto.Items != null)
        {
            foreach (var item in dto.Items)
            {
                strategy.Items.Add(new StrategyItem
                {
                    Id = Guid.NewGuid(),
                    CropStrategyId = strategy.Id,
                    LaborTypeId = item.LaborTypeId,
                    DayOffset = item.DayOffset,
                    DefaultSuppliesJson = item.DefaultSupplies != null
                        ? JsonSerializer.Serialize(item.DefaultSupplies.Where(s => s.SupplyId != Guid.Empty).ToList(), AppJsonSerializerContext.Default.ListStrategySupplyDefault)
                        : null
                });
            }
        }

        _context.CropStrategies.Add(strategy);
        await _context.SaveChangesAsync();

        var created = await _context.CropStrategies
            .AsNoTracking()
            .Include(s => s.Activity)
            .Include(s => s.Items)
                .ThenInclude(i => i.LaborType)
            .FirstAsync(s => s.Id == strategy.Id);

        var supplyIds = created.Items
            .SelectMany(i => DeserializeSupplies(i.DefaultSuppliesJson))
            .Where(sd => sd.SupplyId != Guid.Empty)
            .Select(sd => sd.SupplyId)
            .Distinct()
            .ToList();

        var inventoryMap = new Dictionary<Guid, string>();
        if (supplyIds.Any())
        {
            var inventories = await _context.Inventories
                .AsNoTracking()
                .Where(inv => supplyIds.Contains(inv.Id))
                .Select(inv => new { inv.Id, inv.ItemName })
                .ToListAsync();
            inventoryMap = inventories.ToDictionary(x => x.Id, x => x.ItemName);
        }

        return CreatedAtAction(nameof(GetStrategy), new { id = strategy.Id }, MapToDto(created, inventoryMap));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateStrategy(Guid id, CropStrategyDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            return BadRequest("El nombre es obligatorio.");

        if (dto.ErpActivityId == Guid.Empty)
            return BadRequest("La actividad ERP es obligatoria.");

        if (dto.Items == null || dto.Items.Count == 0)
            return BadRequest("Debe definir al menos una labor.");

        foreach (var item in dto.Items)
        {
            if (item.LaborTypeId == Guid.Empty)
                return BadRequest("Cada labor debe tener un tipo de labor definido.");
        }

        var strategy = await _context.CropStrategies
            .Include(s => s.Items)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (strategy == null)
            return NotFound();

        strategy.Name = dto.Name;
        strategy.ErpActivityId = dto.ErpActivityId;

        var existingItems = strategy.Items.ToList();
        strategy.Items.Clear();
        _context.StrategyItems.RemoveRange(existingItems);

        if (dto.Items != null)
        {
            foreach (var item in dto.Items)
            {
                _context.StrategyItems.Add(new StrategyItem
                {
                    Id = Guid.NewGuid(),
                    CropStrategyId = strategy.Id,
                    LaborTypeId = item.LaborTypeId,
                    DayOffset = item.DayOffset,
                    DefaultSuppliesJson = item.DefaultSupplies != null
                        ? JsonSerializer.Serialize(item.DefaultSupplies.Where(s => s.SupplyId != Guid.Empty).ToList(), AppJsonSerializerContext.Default.ListStrategySupplyDefault)
                        : null
                });
            }
        }

        await _context.SaveChangesAsync();
        return NoContent();
    }


    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteStrategy(Guid id)
    {
        var strategy = await _context.CropStrategies.FindAsync(id);
        if (strategy == null)
            return NotFound();

        _context.CropStrategies.Remove(strategy);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("apply")]
    public async Task<ActionResult<ApplyStrategyResult>> ApplyStrategy(ApplyStrategyRequest request)
    {
        var strategy = await _context.CropStrategies
            .AsNoTracking()
            .Include(s => s.Items)
            .FirstOrDefaultAsync(s => s.Id == request.StrategyId);

        if (strategy == null)
            return NotFound("Estrategia no encontrada.");

        var lots = await _context.Lots
            .AsNoTracking()
            .Include(l => l.Field)
            .Where(l => request.LotIds.Contains(l.Id))
            .ToListAsync();

        if (lots.Count == 0)
            return BadRequest("No se encontraron lotes válidos.");

        var workOrderIds = new List<Guid>();
        var laborsCreated = 0;

        foreach (var lot in lots)
        {
            var wo = new WorkOrder
            {
                Id = Guid.NewGuid(),
                FieldId = lot.FieldId,
                Description = $"{strategy.Name} — {lot.Name}",
                Status = "Pending",
                AssignedTo = request.AssignedTo,
                DueDate = request.StartDate.AddDays(strategy.Items.Any() ? strategy.Items.Max(i => i.DayOffset) + 7 : 30)
            };

            foreach (var item in strategy.Items)
            {
                var labor = new Labor
                {
                    Id = Guid.NewGuid(),
                    WorkOrderId = wo.Id,
                    LotId = lot.Id,
                    LaborTypeId = item.LaborTypeId,
                    Mode = LaborMode.Planned,
                    Status = LaborStatus.Planned,
                    ExecutionDate = request.StartDate.AddDays(item.DayOffset),
                    Hectares = lot.CadastralArea,
                    EffectiveArea = lot.CadastralArea,
                    CreatedAt = DateTime.UtcNow
                };

                if (!string.IsNullOrEmpty(item.DefaultSuppliesJson))
                {
                    var supplies = JsonSerializer.Deserialize(item.DefaultSuppliesJson,
                        AppJsonSerializerContext.Default.ListStrategySupplyDefault);

                    if (supplies != null)
                    {
                        foreach (var s in supplies.Where(s => s.SupplyId != Guid.Empty))
                        {
                            labor.Supplies.Add(new LaborSupply
                            {
                                Id = Guid.NewGuid(),
                                LaborId = labor.Id,
                                SupplyId = s.SupplyId,
                                PlannedDose = s.Dose,
                                PlannedTotal = s.Dose * labor.Hectares,
                                UnitOfMeasure = s.DoseUnit
                            });
                        }
                    }
                }

                wo.Labors.Add(labor);
                laborsCreated++;
            }

            _context.WorkOrders.Add(wo);
            workOrderIds.Add(wo.Id);
        }

        await _context.SaveChangesAsync();

        return new ApplyStrategyResult(workOrderIds.Count, laborsCreated, workOrderIds);
    }

    private static CropStrategyDto MapToDto(CropStrategy strategy, Dictionary<Guid, string>? inventoryMap = null)
    {
        return new CropStrategyDto(
            strategy.Id,
            strategy.Name,
            strategy.ErpActivityId ?? Guid.Empty,
            strategy.Activity?.Name,
            strategy.CreatedAt,
            strategy.Items.OrderBy(i => i.DayOffset).Select(i =>
            {
                List<StrategySupplyDefault> supplies = DeserializeSupplies(i.DefaultSuppliesJson);
                if (inventoryMap != null)
                {
                    foreach (var s in supplies)
                    {
                        if (string.IsNullOrEmpty(s.SupplyName) && s.SupplyId != Guid.Empty)
                            s.SupplyName = inventoryMap.GetValueOrDefault(s.SupplyId);
                    }
                }
                return new StrategyItemDto(i.Id, i.CropStrategyId, i.LaborTypeId, i.ErpActivityId, i.LaborType?.Name, null, i.DayOffset, supplies);
            }).ToList()
        );
    }

    private static List<StrategySupplyDefault> DeserializeSupplies(string? json)
    {
        if (string.IsNullOrEmpty(json)) return new();
        try
        {
            return JsonSerializer.Deserialize(json, AppJsonSerializerContext.Default.ListStrategySupplyDefault) ?? new();
        }
        catch
        {
            return new();
        }
    }
}
