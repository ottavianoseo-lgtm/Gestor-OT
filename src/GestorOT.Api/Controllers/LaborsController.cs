using GestorOT.Application.Interfaces;
using GestorOT.Domain.Entities;
using GestorOT.Shared.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestorOT.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LaborsController : ControllerBase
{
    private readonly IApplicationDbContext _context;

    public LaborsController(IApplicationDbContext context)
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
            CampaignLotId = dto.CampaignLotId,
            LaborTypeId = dto.LaborTypeId,
            Status = "Planned",
            ExecutionDate = dto.ExecutionDate,
            EstimatedDate = dto.EstimatedDate,
            Hectares = dto.Hectares,
            Rate = dto.Rate,
            RateUnit = dto.RateUnit ?? "ha",
            PlannedDose = dto.PlannedDose,
            RealizedDose = dto.RealizedDose,
            CreatedAt = DateTime.UtcNow,
            Notes = dto.Notes,
            PrescriptionMapUrl = dto.PrescriptionMapUrl,
            MachineryUsedId = dto.MachineryUsedId,
            WeatherLogJson = dto.WeatherLogJson
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
                    PlannedTotal = supplyDto.PlannedTotal > 0 ? supplyDto.PlannedTotal : supplyDto.PlannedDose * labor.Hectares,
                    UnitOfMeasure = supplyDto.UnitOfMeasure,
                    TankMixOrder = supplyDto.TankMixOrder,
                    IsSubstitute = supplyDto.IsSubstitute
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
        labor.CampaignLotId = dto.CampaignLotId;
        labor.LaborTypeId = dto.LaborTypeId;
        labor.ExecutionDate = dto.ExecutionDate;
        labor.EstimatedDate = dto.EstimatedDate;
        labor.Hectares = dto.Hectares;
        labor.PlannedDose = dto.PlannedDose;
        labor.RealizedDose = dto.RealizedDose;
        labor.Rate = dto.Rate;
        labor.RateUnit = dto.RateUnit ?? "ha";
        labor.Notes = dto.Notes;
        labor.PrescriptionMapUrl = dto.PrescriptionMapUrl;
        labor.MachineryUsedId = dto.MachineryUsedId;
        labor.WeatherLogJson = dto.WeatherLogJson;

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
                        existing.PlannedTotal = supplyDto.PlannedTotal > 0 ? supplyDto.PlannedTotal : supplyDto.PlannedDose * labor.Hectares;
                        existing.UnitOfMeasure = supplyDto.UnitOfMeasure;
                        existing.TankMixOrder = supplyDto.TankMixOrder;
                        existing.IsSubstitute = supplyDto.IsSubstitute;
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
                        PlannedTotal = supplyDto.PlannedTotal > 0 ? supplyDto.PlannedTotal : supplyDto.PlannedDose * labor.Hectares,
                        UnitOfMeasure = supplyDto.UnitOfMeasure,
                        TankMixOrder = supplyDto.TankMixOrder,
                        IsSubstitute = supplyDto.IsSubstitute
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
            CampaignLotId = source.CampaignLotId,
            LaborTypeId = source.LaborTypeId,
            Status = "Realized",
            ExecutionDate = DateTime.UtcNow,
            Hectares = source.Hectares,
            PlannedDose = source.PlannedDose,
            RealizedDose = source.PlannedDose,
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
                UnitOfMeasure = s.UnitOfMeasure
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

    [HttpGet("calendar")]
    public async Task<ActionResult<List<LaborCalendarDto>>> GetCalendarLabors(
        [FromQuery] DateTime start, [FromQuery] DateTime end)
    {
        var labors = await _context.Labors
            .AsNoTracking()
            .Include(l => l.Lot)
            .Include(l => l.Type)
            .Include(l => l.WorkOrder)
            .Where(l => (l.EstimatedDate != null && l.EstimatedDate >= start && l.EstimatedDate <= end)
                     || (l.EstimatedDate == null && l.ExecutionDate != null && l.ExecutionDate >= start && l.ExecutionDate <= end)
                     || (l.EstimatedDate == null && l.ExecutionDate == null && l.CreatedAt >= start && l.CreatedAt <= end))
            .OrderBy(l => l.EstimatedDate ?? l.ExecutionDate ?? l.CreatedAt)
            .Select(l => new LaborCalendarDto(
                l.Id,
                $"{(l.Type != null ? l.Type.Name : "Labor")} - {(l.Lot != null ? l.Lot.Name : "Sin lote")}",
                l.EstimatedDate ?? l.ExecutionDate ?? l.CreatedAt,
                l.Status,
                l.WorkOrderId == null ? "#FFA500" : "#4CAF50",
                l.WorkOrderId != null,
                l.Type != null ? l.Type.Name : "Labor",
                l.Hectares,
                l.Lot != null ? l.Lot.Name : null,
                l.WorkOrder != null ? l.WorkOrder.Description : null
            ))
            .ToListAsync();

        return labors;
    }

    [HttpGet("unassigned")]
    public async Task<ActionResult<List<LaborDto>>> GetUnassignedLabors()
    {
        var labors = await _context.Labors
            .AsNoTracking()
            .Include(l => l.Lot)
                .ThenInclude(l => l!.Field)
            .Include(l => l.Type)
            .Include(l => l.Supplies)
                .ThenInclude(s => s.Supply)
            .Where(l => l.WorkOrderId == null)
            .OrderByDescending(l => l.CreatedAt)
            .ToListAsync();

        return labors.Select(MapToDto).ToList();
    }

    [HttpGet("unassigned/count")]
    public async Task<ActionResult<int>> GetUnassignedCount()
    {
        var count = await _context.Labors
            .AsNoTracking()
            .CountAsync(l => l.WorkOrderId == null);
        return count;
    }

    [HttpPatch("assign-bulk")]
    public async Task<IActionResult> AssignBulk([FromBody] BulkAssignRequest request)
    {
        if (request.LaborIds == null || request.LaborIds.Count == 0)
            return BadRequest("Debe seleccionar al menos una labor.");

        var updated = await _context.Labors
            .Where(l => request.LaborIds.Contains(l.Id) && l.WorkOrderId == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(l => l.WorkOrderId, request.WorkOrderId)
                .SetProperty(l => l.Status, "Planned"));

        return Ok(new { Updated = updated });
    }

    [HttpPatch("{id:guid}/unassign")]
    public async Task<IActionResult> UnassignLabor(Guid id)
    {
        var labor = await _context.Labors.FindAsync(id);
        if (labor == null) return NotFound();

        labor.WorkOrderId = null;
        labor.Status = "Pending";
        await _context.SaveChangesAsync();
        return NoContent();
    }

    private static LaborDto MapToDto(Labor labor)
    {
        return new LaborDto(
            labor.Id,
            labor.WorkOrderId,
            labor.LotId,
            labor.CampaignLotId,
            labor.LaborTypeId,
            labor.Status,
            labor.ExecutionDate,
            labor.EstimatedDate,
            labor.Hectares,
            labor.CreatedAt,
            labor.Rate,
            labor.RateUnit,
            labor.Lot?.Name,
            labor.Type?.Name,
            labor.Supplies.OrderBy(s => s.TankMixOrder).Select(s => new LaborSupplyDto(
                s.Id,
                s.LaborId,
                s.SupplyId,
                s.PlannedDose,
                s.RealDose,
                s.PlannedTotal,
                s.RealTotal,
                s.UnitOfMeasure,
                s.Supply?.ItemName,
                s.Supply?.UnitB,
                s.TankMixOrder,
                s.IsSubstitute
            )).ToList(),
            labor.PrescriptionMapUrl,
            labor.MachineryUsedId,
            labor.WeatherLogJson,
            labor.Notes,
            labor.Lot?.Field?.Name,
            labor.PlannedDose,
            labor.RealizedDose
        );
    }
}

public class BulkAssignRequest
{
    public List<Guid> LaborIds { get; set; } = new();
    public Guid WorkOrderId { get; set; }
}
