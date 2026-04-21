using GestorOT.Application.Interfaces;
using GestorOT.Application.Services;
using GestorOT.Domain.Entities;
using GestorOT.Domain.Enums;
using GestorOT.Shared.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestorOT.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LaborsController : ControllerBase
{
    private readonly IApplicationDbContext _context;
    private readonly IAgronomicValidationService _validationService;
    private readonly IWorkOrderService _workOrderService;

    public LaborsController(IApplicationDbContext context, IAgronomicValidationService validationService, IWorkOrderService workOrderService)
    {
        _context = context;
        _validationService = validationService;
        _workOrderService = workOrderService;
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
    public async Task<ActionResult<LaborSaveResponse>> CreateLabor(LaborDto dto)
    {
        // Debug
        Console.WriteLine($"CreateLabor: CampaignLotId={dto.CampaignLotId}, LotId={dto.LotId}");

        var lotId = dto.LotId;
        var campaignLotId = dto.CampaignLotId;

        // Si mandan CampaignLotId pero no LotId, recuperamos el LotId
        if (campaignLotId != null && campaignLotId != Guid.Empty && lotId == Guid.Empty)
        {
            var campaignLot = await _context.CampaignLots
                .FirstOrDefaultAsync(cl => cl.Id == campaignLotId);
            
            if (campaignLot != null)
                lotId = campaignLot.LotId;
            else
                return BadRequest("El CampaignLotId proporcionado no existe.");
        }
        // Si mandan LotId pero no CampaignLotId (fallback original)
        else if ((campaignLotId == null || campaignLotId == Guid.Empty) && lotId != Guid.Empty)
        {
            var campaignLot = await _context.CampaignLots
                .FirstOrDefaultAsync(cl => cl.LotId == lotId);
            
            if (campaignLot != null)
                campaignLotId = campaignLot.Id;
            else
                return BadRequest("No se encontró CampaignLotId y no pudo ser inferido a partir del LotId.");
        }
        else if (lotId == Guid.Empty && (campaignLotId == null || campaignLotId == Guid.Empty))
        {
            return BadRequest("Debe proporcionar al menos un LotId o un CampaignLotId.");
        }

        // Sprint 2 Validations
        var validation = await ValidateLaborAsync(dto with { LotId = lotId, CampaignLotId = campaignLotId });
        if (validation.Error != null)
        {
            return BadRequest(validation.Error);
        }

        var labor = new Labor
        {
            Id = Guid.NewGuid(),
            WorkOrderId = dto.WorkOrderId,
            LotId = lotId,
            CampaignLotId = campaignLotId,
            ErpActivityId = dto.ErpActivityId,
            LaborTypeId = dto.LaborTypeId,
            ContactId = dto.ContactId,
            IsExternalBilling = dto.IsExternalBilling,
            Status = dto.Status ?? "Planned",
            Mode = Enum.TryParse<LaborMode>(dto.Mode, out var m) ? m : (dto.Status == "Realized" ? LaborMode.Realized : LaborMode.Planned),
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
                var pDose = supplyDto.PlannedDose;
                var pHa = supplyDto.PlannedHectares > 0 ? supplyDto.PlannedHectares : labor.Hectares;
                
                // Si se crea como realizada y no trae dosis planeada, usamos la real
                if (labor.Status == "Realized" && pDose == 0 && supplyDto.RealDose.HasValue)
                {
                    pDose = supplyDto.RealDose.Value;
                }

                labor.Supplies.Add(new LaborSupply
                {
                    Id = Guid.NewGuid(),
                    LaborId = labor.Id,
                    SupplyId = supplyDto.SupplyId,
                    PlannedDose = pDose,
                    PlannedTotal = supplyDto.PlannedTotal > 0 ? supplyDto.PlannedTotal : pDose * pHa,
                    PlannedHectares = pHa,
                    RealDose = supplyDto.RealDose,
                    RealHectares = supplyDto.RealHectares,
                    RealTotal = supplyDto.RealTotal ?? (supplyDto.RealDose.HasValue ? supplyDto.RealDose.Value * (supplyDto.RealHectares ?? labor.Hectares) : null),
                    UnitOfMeasure = supplyDto.UnitOfMeasure,
                    TankMixOrder = supplyDto.TankMixOrder,
                    IsSubstitute = supplyDto.IsSubstitute
                });
            }
        }

        _context.Labors.Add(labor);
        await _context.SaveChangesAsync();

        if (labor.WorkOrderId.HasValue)
        {
            await _workOrderService.ConsolidateSuppliesAsync(labor.WorkOrderId.Value);
        }

        var created = await _context.Labors
            .AsNoTracking()
            .Include(l => l.Lot)
            .Include(l => l.Supplies)
                .ThenInclude(s => s.Supply)
            .FirstAsync(l => l.Id == labor.Id);

        return Ok(new LaborSaveResponse(MapToDto(created), validation.Warnings));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<LaborSaveResponse>> UpdateLabor(Guid id, LaborDto dto)
    {
        var labor = await _context.Labors
            .Include(l => l.Supplies)
            .FirstOrDefaultAsync(l => l.Id == id);

        if (labor == null)
            return NotFound();

        // Sprint 2 Validations
        var validation = await ValidateLaborAsync(dto);
        if (validation.Error != null)
        {
            return BadRequest(validation.Error);
        }

        labor.LotId = dto.LotId;
        
        var campaignLotId = dto.CampaignLotId;
        if (campaignLotId == null || campaignLotId == Guid.Empty)
        {
            var campaignLot = await _context.CampaignLots
                .FirstOrDefaultAsync(cl => cl.LotId == dto.LotId);
            if (campaignLot != null)
                campaignLotId = campaignLot.Id;
        }
        
        labor.CampaignLotId = campaignLotId;
        labor.ErpActivityId = dto.ErpActivityId;
        labor.LaborTypeId = dto.LaborTypeId;
        labor.ContactId = dto.ContactId;
        labor.IsExternalBilling = dto.IsExternalBilling;
        labor.Status = dto.Status ?? labor.Status;
        if (Enum.TryParse<LaborMode>(dto.Mode, out var m2)) labor.Mode = m2;
        else if (dto.Status == "Realized") labor.Mode = LaborMode.Realized;
        
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
            // 1. Remover insumos que ya no están en el DTO
            var incomingIds = dto.Supplies.Where(s => s.Id != Guid.Empty).Select(s => s.Id).ToHashSet();
            var toRemove = labor.Supplies.Where(s => !incomingIds.Contains(s.Id)).ToList();
            foreach (var r in toRemove)
            {
                _context.LaborSupplies.Remove(r);
            }

            // 2. Actualizar existentes o agregar nuevos
            foreach (var supplyDto in dto.Supplies)
            {
                var existing = labor.Supplies.FirstOrDefault(s => s.Id == supplyDto.Id && s.Id != Guid.Empty);
                if (existing != null)
                {
                    existing.SupplyId = supplyDto.SupplyId;
                    existing.PlannedDose = supplyDto.PlannedDose;
                    existing.PlannedTotal = supplyDto.PlannedTotal > 0 ? supplyDto.PlannedTotal : supplyDto.PlannedDose * supplyDto.PlannedHectares;
                    existing.PlannedHectares = supplyDto.PlannedHectares > 0 ? supplyDto.PlannedHectares : labor.Hectares;
                    existing.RealDose = supplyDto.RealDose;
                    existing.RealHectares = supplyDto.RealHectares;
                    existing.RealTotal = supplyDto.RealTotal ?? (supplyDto.RealDose.HasValue ? supplyDto.RealDose.Value * (supplyDto.RealHectares ?? labor.Hectares) : null);
                    existing.UnitOfMeasure = supplyDto.UnitOfMeasure;
                    existing.TankMixOrder = supplyDto.TankMixOrder;
                    existing.IsSubstitute = supplyDto.IsSubstitute;
                }
                else
                {
                    labor.Supplies.Add(new LaborSupply
                    {
                        Id = Guid.NewGuid(),
                        LaborId = labor.Id,
                        SupplyId = supplyDto.SupplyId,
                        PlannedDose = supplyDto.PlannedDose,
                        PlannedTotal = supplyDto.PlannedTotal > 0 ? supplyDto.PlannedTotal : supplyDto.PlannedDose * (supplyDto.PlannedHectares > 0 ? supplyDto.PlannedHectares : labor.Hectares),
                        PlannedHectares = supplyDto.PlannedHectares > 0 ? supplyDto.PlannedHectares : labor.Hectares,
                        RealDose = supplyDto.RealDose,
                        RealHectares = supplyDto.RealHectares,
                        RealTotal = supplyDto.RealTotal ?? (supplyDto.RealDose.HasValue ? supplyDto.RealDose.Value * (supplyDto.RealHectares ?? labor.Hectares) : null),
                        UnitOfMeasure = supplyDto.UnitOfMeasure,
                        TankMixOrder = supplyDto.TankMixOrder,
                        IsSubstitute = supplyDto.IsSubstitute
                    });
                }
            }
        }

        await _context.SaveChangesAsync();
        
        if (labor.WorkOrderId.HasValue)
        {
            await _workOrderService.ConsolidateSuppliesAsync(labor.WorkOrderId.Value);
        }
        
        var updated = await _context.Labors
            .AsNoTracking()
            .Include(l => l.Lot)
            .Include(l => l.Supplies)
                .ThenInclude(s => s.Supply)
            .FirstAsync(l => l.Id == labor.Id);

        return Ok(new LaborSaveResponse(MapToDto(updated), validation.Warnings));
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
            ErpActivityId = source.ErpActivityId,
            LaborTypeId = source.LaborTypeId,
            ContactId = source.ContactId,
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

    [HttpPost("{id:guid}/execute-standalone")]
    public async Task<ActionResult<Guid>> ExecuteStandalone(Guid id, [FromBody] List<LaborSupplyDto> realSupplies)
    {
        var strategy = _context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var source = await _context.Labors
                    .Include(l => l.Supplies)
                    .FirstOrDefaultAsync(l => l.Id == id);

                if (source == null) return (ActionResult<Guid>)NotFound();
                if (source.Mode != LaborMode.Planned) return (ActionResult<Guid>)BadRequest("Solo se pueden ejecutar labores planeadas.");
                if (source.Status == "Realized") return (ActionResult<Guid>)BadRequest("La labor ya fue realizada.");

                var newLabor = new Labor
                {
                    Id = Guid.NewGuid(),
                    WorkOrderId = source.WorkOrderId,
                    LotId = source.LotId,
                    CampaignLotId = source.CampaignLotId,
                    ErpActivityId = source.ErpActivityId,
                    LaborTypeId = source.LaborTypeId,
                    ContactId = source.ContactId,
                    IsExternalBilling = source.IsExternalBilling,
                    Mode = LaborMode.Realized,
                    Status = "Realized",
                    ExecutionDate = DateTime.UtcNow,
                    Hectares = source.Hectares,
                    EffectiveArea = source.Hectares, // Default to hectares
                    Rate = source.Rate,
                    RateUnit = source.RateUnit,
                    PlannedDose = source.PlannedDose,
                    RealizedDose = source.PlannedDose,
                    PlannedLaborId = source.Id,
                    CreatedAt = DateTime.UtcNow,
                    Notes = source.Notes,
                    PrescriptionMapUrl = source.PrescriptionMapUrl,
                    MachineryUsedId = source.MachineryUsedId,
                    WeatherLogJson = source.WeatherLogJson,
                    EvidencePhotosJson = source.EvidencePhotosJson,
                    MetadataExterna = source.MetadataExterna
                };

                foreach (var s in source.Supplies)
                {
                    var realSupply = realSupplies.FirstOrDefault(rs => rs.SupplyId == s.SupplyId);
                    var dose = realSupply?.RealDose ?? s.PlannedDose;

                    newLabor.Supplies.Add(new LaborSupply
                    {
                        Id = Guid.NewGuid(),
                        LaborId = newLabor.Id,
                        SupplyId = s.SupplyId,
                        PlannedDose = s.PlannedDose,
                        PlannedTotal = s.PlannedTotal,
                        PlannedHectares = s.PlannedHectares,
                        RealDose = dose,
                        RealTotal = dose * newLabor.Hectares,
                        RealHectares = newLabor.Hectares,
                        UnitOfMeasure = s.UnitOfMeasure,
                        TankMixOrder = s.TankMixOrder,
                        IsSubstitute = s.IsSubstitute
                    });
                }

                _context.Labors.Add(newLabor);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return (ActionResult<Guid>)Ok(newLabor.Id);
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteLabor(Guid id)
    {
        var labor = await _context.Labors.FindAsync(id);
        if (labor == null)
            return NotFound();

        var woId = labor.WorkOrderId;
        _context.Labors.Remove(labor);
        await _context.SaveChangesAsync();

        if (woId.HasValue)
        {
            await _workOrderService.ConsolidateSuppliesAsync(woId.Value);
        }

        return NoContent();
    }

    [HttpGet("calendar")]
    public async Task<ActionResult<List<LaborCalendarDto>>> GetCalendarLabors(
        [FromQuery] DateTime start, [FromQuery] DateTime end)
    {
        var startUtc = DateTime.SpecifyKind(start, DateTimeKind.Utc);
        var endUtc = DateTime.SpecifyKind(end, DateTimeKind.Utc);

        var labors = await _context.Labors
            .AsNoTracking()
            .Include(l => l.Lot)
            .Include(l => l.Type)
            .Include(l => l.WorkOrder)
            .Where(l => (l.EstimatedDate != null && l.EstimatedDate >= startUtc && l.EstimatedDate <= endUtc)
                     || (l.EstimatedDate == null && l.ExecutionDate != null && l.ExecutionDate >= startUtc && l.ExecutionDate <= endUtc)
                     || (l.EstimatedDate == null && l.ExecutionDate == null && l.CreatedAt >= startUtc && l.CreatedAt <= endUtc))
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

    private async Task<(string? Error, List<string> Warnings)> ValidateLaborAsync(LaborDto dto)
    {
        var warnings = new List<string>();
        
        // 1. OT Context Validations
        if (dto.WorkOrderId.HasValue && dto.WorkOrderId != Guid.Empty)
        {
            var ot = await _context.WorkOrders
                .Include(w => w.Labors)
                .FirstOrDefaultAsync(w => w.Id == dto.WorkOrderId.Value);

            if (ot != null)
            {
                // Check Responsible policy
                if (!ot.AcceptsMultiplePeople)
                {
                    // If OT has a responsible, labor should match or OT should be updated
                    if (ot.ContactId.HasValue && dto.ContactId.HasValue && ot.ContactId != dto.ContactId)
                    {
                        warnings.Add("La OT no acepta múltiples personas. El responsable de la labor difiere del responsable de la OT.");
                    }
                    
                    // Check other labors in this OT
                    var otherContacts = ot.Labors
                        .Where(l => l.Id != dto.Id && l.ContactId.HasValue)
                        .Select(l => l.ContactId)
                        .Distinct()
                        .ToList();

                    if (dto.ContactId.HasValue && otherContacts.Any(c => c != dto.ContactId))
                    {
                        warnings.Add("Ya existen labores con otros responsables asignados a esta OT.");
                    }
                }

                // Check Date policy
                if (!ot.AcceptsMultipleDates)
                {
                    var laborDate = dto.ExecutionDate ?? dto.EstimatedDate;
                    if (laborDate.HasValue)
                    {
                        var otDate = ot.PlannedDate;
                        if (Math.Abs((laborDate.Value - otDate).TotalDays) > 1) // Tolerance of 1 day
                        {
                            warnings.Add("La OT no acepta múltiples fechas. La fecha de la labor difiere significativamente de la fecha planificada de la OT.");
                        }
                    }
                }
            }
        }

        // 2. Agronomic Validations
        if (dto.CampaignLotId.HasValue && dto.CampaignLotId != Guid.Empty)
        {
            var isSurfaceValid = await _validationService.ValidateLaborSurfaceAsync(dto.CampaignLotId.Value, dto.Hectares);
            if (!isSurfaceValid)
            {
                return ("La superficie de la labor supera el área productiva del lote asignado.", warnings);
            }
        }

        // 1.3 Validation of Dates
        if (dto.CampaignLotId.HasValue && dto.CampaignLotId != Guid.Empty)
        {
            var dateError = await _validationService.ValidateLaborDatesInRotationAsync(dto.CampaignLotId.Value, dto.EstimatedDate, dto.ExecutionDate);
            if (dateError != null)
            {
                return (dateError, warnings);
            }
        }

        // 1.2 Validation of Activity
        if (dto.CampaignLotId.HasValue && dto.CampaignLotId != Guid.Empty && dto.ErpActivityId.HasValue)
        {
            var date = dto.ExecutionDate ?? dto.EstimatedDate ?? DateTime.Today;
            var activityWarning = await _validationService.ValidateLaborActivityMatchesRotationAsync(
                dto.CampaignLotId.Value, 
                DateOnly.FromDateTime(date), 
                dto.ErpActivityId.Value);
            
            if (activityWarning != null)
            {
                warnings.Add(activityWarning);
            }
        }

        return (null, warnings);
    }

    private static LaborDto MapToDto(Labor labor)
    {
        return new LaborDto(
            labor.Id,
            labor.WorkOrderId,
            labor.LotId,
            labor.CampaignLotId ?? Guid.Empty,
            labor.LaborTypeId,
            labor.ErpActivityId,
            labor.Status,
            labor.Mode.ToString(),
            labor.ExecutionDate,
            labor.EstimatedDate,
            labor.Hectares,
            labor.CreatedAt,
            labor.Rate,
            labor.RateUnit,
            labor.Lot?.Name,
            labor.Type?.Name,
            labor.ErpActivity?.Name,
            labor.Supplies.Select(s => new LaborSupplyDto(s.Id, s.LaborId, s.SupplyId, s.PlannedHectares, s.RealHectares, s.PlannedDose, s.RealDose, s.PlannedTotal, s.RealTotal, s.CalculatedDose, s.CalculatedTotal, s.UnitOfMeasure, s.Supply?.ItemName, s.Supply?.UnitA, s.TankMixOrder, s.IsSubstitute)).ToList(),
            labor.PrescriptionMapUrl,
            labor.MachineryUsedId,
            labor.WeatherLogJson,
            labor.Notes,
            labor.Lot?.Field?.Name,
            labor.PlannedDose,
            labor.RealizedDose,
            labor.ContactId,
            labor.IsExternalBilling,
            labor.PlannedLaborId
        );
    }
}

public class BulkAssignRequest
{
    public List<Guid> LaborIds { get; set; } = new();
    public Guid WorkOrderId { get; set; }
}
