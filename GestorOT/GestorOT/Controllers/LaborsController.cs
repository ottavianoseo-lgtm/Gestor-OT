using GestorOT.Data;
using GestorOT.Shared.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestorOT.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LaborsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public LaborsController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet("by-workorder/{workOrderId:guid}")]
    public async Task<ActionResult<List<LaborDto>>> GetByWorkOrder(Guid workOrderId)
    {
        var labors = await _context.Labors
            .AsNoTracking()
            .Include(l => l.Lot)
            .Include(l => l.Supplies)
                .ThenInclude(s => s.Supply)
            .Where(l => l.WorkOrderId == workOrderId)
            .OrderBy(l => l.CreatedAt)
            .ToListAsync();

        return labors.Select(MapToDto).ToList();
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<LaborDto>> GetLabor(Guid id)
    {
        var labor = await _context.Labors
            .AsNoTracking()
            .Include(l => l.Lot)
            .Include(l => l.Supplies)
                .ThenInclude(s => s.Supply)
            .FirstOrDefaultAsync(l => l.Id == id);

        if (labor == null)
            return NotFound();

        return MapToDto(labor);
    }

    [HttpPost]
    public async Task<ActionResult<LaborDto>> CreateLabor(LaborDto dto)
    {
        var labor = new Labor
        {
            Id = Guid.NewGuid(),
            WorkOrderId = dto.WorkOrderId,
            LotId = dto.LotId,
            LaborType = dto.LaborType,
            Status = "Planned",
            ExecutionDate = dto.ExecutionDate,
            Hectares = dto.Hectares,
            Rate = dto.Rate,
            RateUnit = dto.RateUnit ?? "ha",
            CreatedAt = DateTime.UtcNow
        };

        if (dto.Supplies != null)
        {
            foreach (var supplyDto in dto.Supplies)
            {
                labor.Supplies.Add(new LaborSupply
                {
                    Id = Guid.NewGuid(),
                    LaborId = labor.Id,
                    SupplyId = supplyDto.SupplyId,
                    PlannedDose = supplyDto.PlannedDose,
                    PlannedTotal = supplyDto.PlannedDose * labor.Hectares,
                    DoseUnit = supplyDto.DoseUnit
                });
            }
        }

        _context.Labors.Add(labor);
        await _context.SaveChangesAsync();

        var created = await _context.Labors
            .AsNoTracking()
            .Include(l => l.Lot)
            .Include(l => l.Supplies)
                .ThenInclude(s => s.Supply)
            .FirstAsync(l => l.Id == labor.Id);

        return CreatedAtAction(nameof(GetLabor), new { id = labor.Id }, MapToDto(created));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateLabor(Guid id, LaborDto dto)
    {
        var labor = await _context.Labors
            .Include(l => l.Supplies)
            .FirstOrDefaultAsync(l => l.Id == id);

        if (labor == null)
            return NotFound();

        labor.LotId = dto.LotId;
        labor.LaborType = dto.LaborType;
        labor.ExecutionDate = dto.ExecutionDate;
        labor.Hectares = dto.Hectares;
        labor.Rate = dto.Rate;
        labor.RateUnit = dto.RateUnit ?? "ha";

        if (dto.Supplies != null)
        {
            var existingIds = dto.Supplies.Where(s => s.Id != Guid.Empty).Select(s => s.Id).ToHashSet();
            var toRemove = labor.Supplies.Where(s => !existingIds.Contains(s.Id)).ToList();
            foreach (var r in toRemove)
                _context.LaborSupplies.Remove(r);

            foreach (var supplyDto in dto.Supplies)
            {
                if (supplyDto.Id != Guid.Empty)
                {
                    var existing = labor.Supplies.FirstOrDefault(s => s.Id == supplyDto.Id);
                    if (existing != null)
                    {
                        existing.SupplyId = supplyDto.SupplyId;
                        existing.PlannedDose = supplyDto.PlannedDose;
                        existing.PlannedTotal = supplyDto.PlannedDose * labor.Hectares;
                        existing.DoseUnit = supplyDto.DoseUnit;
                    }
                }
                else
                {
                    labor.Supplies.Add(new LaborSupply
                    {
                        Id = Guid.NewGuid(),
                        LaborId = labor.Id,
                        SupplyId = supplyDto.SupplyId,
                        PlannedDose = supplyDto.PlannedDose,
                        PlannedTotal = supplyDto.PlannedDose * labor.Hectares,
                        DoseUnit = supplyDto.DoseUnit
                    });
                }
            }
        }

        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{id:guid}/realize")]
    public async Task<IActionResult> RealizeLabor(Guid id, [FromBody] List<LaborSupplyDto> realSupplies)
    {
        var labor = await _context.Labors
            .Include(l => l.Supplies)
            .FirstOrDefaultAsync(l => l.Id == id);

        if (labor == null)
            return NotFound();

        if (labor.Status == "Realized")
            return BadRequest("La labor ya fue realizada.");

        labor.Status = "Realized";
        labor.ExecutionDate = DateTime.UtcNow;

        foreach (var realSupply in realSupplies)
        {
            var existing = labor.Supplies.FirstOrDefault(s => s.Id == realSupply.Id);
            if (existing != null)
            {
                existing.RealDose = realSupply.RealDose ?? realSupply.PlannedDose;
                existing.RealTotal = (realSupply.RealDose ?? realSupply.PlannedDose) * labor.Hectares;
            }
        }

        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{id:guid}/replicate")]
    public async Task<ActionResult<LaborDto>> ReplicateLabor(Guid id)
    {
        var source = await _context.Labors
            .AsNoTracking()
            .Include(l => l.Supplies)
            .FirstOrDefaultAsync(l => l.Id == id);

        if (source == null)
            return NotFound();

        var newLabor = new Labor
        {
            Id = Guid.NewGuid(),
            WorkOrderId = source.WorkOrderId,
            LotId = source.LotId,
            LaborType = source.LaborType,
            Status = "Realized",
            ExecutionDate = DateTime.UtcNow,
            Hectares = source.Hectares,
            Rate = source.Rate,
            RateUnit = source.RateUnit,
            CreatedAt = DateTime.UtcNow
        };

        foreach (var s in source.Supplies)
        {
            newLabor.Supplies.Add(new LaborSupply
            {
                Id = Guid.NewGuid(),
                LaborId = newLabor.Id,
                SupplyId = s.SupplyId,
                PlannedDose = s.PlannedDose,
                PlannedTotal = s.PlannedTotal,
                RealDose = s.PlannedDose,
                RealTotal = s.PlannedTotal,
                DoseUnit = s.DoseUnit
            });
        }

        _context.Labors.Add(newLabor);
        await _context.SaveChangesAsync();

        var created = await _context.Labors
            .AsNoTracking()
            .Include(l => l.Lot)
            .Include(l => l.Supplies)
                .ThenInclude(su => su.Supply)
            .FirstAsync(l => l.Id == newLabor.Id);

        return CreatedAtAction(nameof(GetLabor), new { id = newLabor.Id }, MapToDto(created));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteLabor(Guid id)
    {
        var labor = await _context.Labors.FindAsync(id);
        if (labor == null)
            return NotFound();

        _context.Labors.Remove(labor);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    private static LaborDto MapToDto(Labor labor)
    {
        return new LaborDto(
            labor.Id,
            labor.WorkOrderId,
            labor.LotId,
            labor.LaborType,
            labor.Status,
            labor.ExecutionDate,
            labor.Hectares,
            labor.CreatedAt,
            labor.Rate,
            labor.RateUnit,
            labor.Lot?.Name,
            labor.Supplies.Select(s => new LaborSupplyDto(
                s.Id,
                s.LaborId,
                s.SupplyId,
                s.PlannedDose,
                s.RealDose,
                s.PlannedTotal,
                s.RealTotal,
                s.DoseUnit,
                s.Supply?.ItemName,
                s.Supply?.UnitB
            )).ToList()
        );
    }
}
