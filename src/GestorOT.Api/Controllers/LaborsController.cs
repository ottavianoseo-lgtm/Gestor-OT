using System.Text.Json;
using GestorOT.Application.Interfaces;
using GestorOT.Application.Services;
using GestorOT.Domain.Entities;
using GestorOT.Domain.Enums;
using GestorOT.Shared;
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
            .Include(l => l.Lot).ThenInclude(l => l.Field)
            .Include(l => l.WorkOrder)
            .Include(l => l.Supplies).ThenInclude(s => s.Supply)
            .Include(l => l.SourceStrategy)
            .Include(l => l.CampaignLot)
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
            .Include(l => l.Lot).ThenInclude(l => l.Field)
            .Include(l => l.WorkOrder)
            .Include(l => l.Supplies).ThenInclude(s => s.Supply)
            .Include(l => l.SourceStrategy)
            .Include(l => l.CampaignLot)
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
            WorkOrderId = (dto.WorkOrderId == Guid.Empty) ? null : dto.WorkOrderId,
            LotId = lotId,
            CampaignLotId = campaignLotId,
            ErpActivityId = dto.ErpActivityId,
            LaborTypeId = dto.LaborTypeId,
            ContactId = dto.ContactId,
            IsExternalBilling = dto.IsExternalBilling,
            Status = Enum.TryParse<LaborStatus>(dto.Status, out var status) ? status : LaborStatus.Planned,
            Mode = Enum.TryParse<LaborMode>(dto.Mode, out var m) ? m : (dto.Status == "Realized" ? LaborMode.Realized : LaborMode.Planned),
            PlannedLaborId = dto.PlannedLaborId,
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
            WeatherLogJson = dto.WeatherLogJson,
            Priority = dto.Priority
        };

        if (dto.Supplies != null)
        {
            foreach (var supplyDto in dto.Supplies)
            {
                var pDose = supplyDto.PlannedDose;
                var pHa = supplyDto.PlannedHectares > 0 ? supplyDto.PlannedHectares : labor.Hectares;
                
                // Si se crea como realizada y no trae dosis planeada, usamos la real
                if (labor.Status == LaborStatus.Realized && pDose == 0 && supplyDto.RealDose.HasValue)
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

        if (labor.Mode == LaborMode.Realized)
        {
            labor.Status = LaborStatus.Realized;
            
            if (labor.PlannedLaborId.HasValue)
            {
                var planned = await _context.Labors.FindAsync(labor.PlannedLaborId.Value);
                if (planned != null)
                {
                    planned.Status = LaborStatus.Validated;
                }
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
            .Include(l => l.Contact)
            .Include(l => l.Supplies)
                .ThenInclude(s => s.Supply)
            .Include(l => l.CampaignLot)
            .FirstAsync(l => l.Id == labor.Id);

        return Ok(new LaborSaveResponse(MapToDto(created), validation.Warnings));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<LaborSaveResponse>> UpdateLabor(Guid id, LaborDto dto)
    {
        var labor = await _context.Labors
            .Include(l => l.Supplies)
            .Include(l => l.WorkOrder)
                .ThenInclude(wo => wo!.WorkOrderStatus)
            .FirstOrDefaultAsync(l => l.Id == id);

        if (labor == null)
            return NotFound();

        if (labor.WorkOrder?.WorkOrderStatus?.IsEditable == false)
            return Conflict("La labor pertenece a una OT bloqueada.");

        // Sprint 2 Validations
        var validation = await ValidateLaborAsync(dto);
        if (validation.Error != null)
        {
            return BadRequest(validation.Error);
        }

        labor.WorkOrderId = (dto.WorkOrderId == Guid.Empty) ? null : dto.WorkOrderId;
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
        if (Enum.TryParse<LaborStatus>(dto.Status, out var st)) labor.Status = st;
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
        labor.Priority = dto.Priority;
        if (dto.Supplies != null)
        {
            // 1. Remove supplies not in the DTO
            var incomingIds = dto.Supplies.Where(s => s.Id != Guid.Empty).Select(s => s.Id).ToHashSet();
            var toRemove = labor.Supplies.Where(s => !incomingIds.Contains(s.Id)).ToList();
            foreach (var r in toRemove)
            {
                _context.LaborSupplies.Remove(r);
            }

            // 2. Update or Add
            foreach (var supplyDto in dto.Supplies)
            {
                var existing = labor.Supplies.FirstOrDefault(s => s.Id == supplyDto.Id && s.Id != Guid.Empty);
                if (existing != null)
                {
                    // Update existing
                    existing.SupplyId = supplyDto.SupplyId;
                    existing.PlannedDose = supplyDto.PlannedDose;
                    existing.PlannedHectares = supplyDto.PlannedHectares > 0 ? supplyDto.PlannedHectares : labor.Hectares;
                    existing.PlannedTotal = supplyDto.PlannedTotal > 0 ? supplyDto.PlannedTotal : supplyDto.PlannedDose * existing.PlannedHectares;
                    existing.RealDose = supplyDto.RealDose;
                    existing.RealHectares = supplyDto.RealHectares;
                    existing.RealTotal = supplyDto.RealTotal ?? (supplyDto.RealDose.HasValue ? supplyDto.RealDose.Value * (supplyDto.RealHectares ?? labor.Hectares) : null);
                    existing.UnitOfMeasure = supplyDto.UnitOfMeasure ?? "";
                    existing.TankMixOrder = supplyDto.TankMixOrder;
                    existing.IsSubstitute = supplyDto.IsSubstitute;
                }
                else
                {
                    // Always treat as new if not found in the loaded collection
                    var newSupply = new LaborSupply
                    {
                        Id = Guid.NewGuid(), // Ignore client ID for new supplies to be safe
                        LaborId = labor.Id,
                        SupplyId = supplyDto.SupplyId,
                        PlannedDose = supplyDto.PlannedDose,
                        PlannedHectares = supplyDto.PlannedHectares > 0 ? supplyDto.PlannedHectares : labor.Hectares,
                        PlannedTotal = supplyDto.PlannedTotal > 0 ? supplyDto.PlannedTotal : supplyDto.PlannedDose * (supplyDto.PlannedHectares > 0 ? supplyDto.PlannedHectares : labor.Hectares),
                        RealDose = supplyDto.RealDose,
                        RealHectares = supplyDto.RealHectares,
                        RealTotal = supplyDto.RealTotal ?? (supplyDto.RealDose.HasValue ? supplyDto.RealDose.Value * (supplyDto.RealHectares ?? labor.Hectares) : null),
                        UnitOfMeasure = supplyDto.UnitOfMeasure ?? "",
                        TankMixOrder = supplyDto.TankMixOrder,
                        IsSubstitute = supplyDto.IsSubstitute
                    };
                    _context.LaborSupplies.Add(newSupply); // Use the DbSet.Add to be explicit
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
            .Include(l => l.Contact)
            .Include(l => l.Supplies)
                .ThenInclude(s => s.Supply)
            .Include(l => l.CampaignLot)
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

        if (labor.Status == LaborStatus.Realized)
            return BadRequest("La labor ya fue realizada.");

        if (labor.Status != LaborStatus.AwaitingValidation)
            return BadRequest("La labor debe estar en estado 'AwaitingValidation' para ser realizada.");

        labor.Status = LaborStatus.Realized;
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
            Status = LaborStatus.Realized,
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
                if (source.Status == LaborStatus.Realized) return (ActionResult<Guid>)BadRequest("La labor ya fue realizada.");

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
                    Status = LaborStatus.Realized,
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

    [HttpPost("{id:guid}/submit-for-validation")]
    public async Task<ActionResult<object>> SubmitForValidation(Guid id)
    {
        var labor = await _context.Labors.FindAsync(id);
        if (labor == null) return NotFound();

        if (labor.Status == LaborStatus.Realized || labor.Status == LaborStatus.Validated)
        {
            return BadRequest("Esta labor ya ha sido realizada o validada.");
        }

        if (labor.Status == LaborStatus.AwaitingValidation)
        {
            return BadRequest("Esta labor ya se encuentra en proceso de validación.");
        }

        labor.Status = LaborStatus.AwaitingValidation;

        var rawToken = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32))
            .Replace("+", "-").Replace("/", "_").TrimEnd('=');
        
        var bytes = System.Text.Encoding.UTF8.GetBytes(rawToken);
        var hash = System.Security.Cryptography.SHA256.HashData(bytes);
        var tokenHash = Convert.ToHexStringLower(hash);

        var sharedToken = new SharedToken
        {
            Id = Guid.NewGuid(),
            WorkOrderId = (labor.WorkOrderId == null || labor.WorkOrderId == Guid.Empty) ? null : labor.WorkOrderId,
            TenantId = labor.TenantId == Guid.Empty ? _context.CurrentTenantId : labor.TenantId,
            TokenHash = tokenHash,
            ExpiresAt = DateTime.UtcNow.AddHours(72),
            IsRevoked = false,
            IsUsed = false,
            CreatedAt = DateTime.UtcNow,
            Metadata = JsonSerializer.Serialize(new { laborId = labor.Id, action = "validate" })
        };

        _context.SharedTokens.Add(sharedToken);
        await _context.SaveChangesAsync();

        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var publicUrl = $"{baseUrl}/public/labor-execution/{rawToken}";

        return Ok(new { Url = publicUrl, Token = rawToken });
    }

    [HttpPost("{id:guid}/unpin-original-plan")]
    public async Task<IActionResult> UnpinOriginalPlan(Guid id)
    {
        // Need to check roles. Manually check if not using [Authorize]
        if (!User.IsInRole("AdminCampaña")) return Forbid();

        var labor = await _context.Labors.FindAsync(id);
        if (labor == null) return NotFound();

        labor.IsOriginalPlan = false;
        
        var audit = new AuditLog
        {
            Id = Guid.NewGuid(),
            Action = "UnpinOriginalPlan",
            EntityType = "Labor",
            EntityId = id.ToString(),
            UserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
            UserEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value,
            Timestamp = DateTime.UtcNow
        };
        _context.AuditLogs.Add(audit);

        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteLabor(Guid id)
    {
        var labor = await _context.Labors
            .Include(l => l.WorkOrder)
                .ThenInclude(wo => wo!.WorkOrderStatus)
            .FirstOrDefaultAsync(l => l.Id == id);

        if (labor == null)
            return NotFound();

        if (labor.WorkOrder?.WorkOrderStatus?.IsEditable == false)
            return Conflict("La labor pertenece a una OT bloqueada.");

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
                l.Status.ToString(),
                l.WorkOrderId == null ? "#FFA500" : "#4CAF50",
                l.WorkOrderId != null,
                l.Type != null ? l.Type.Name : "Labor",
                l.Hectares,
                l.Lot != null ? l.Lot.Name : null,
                l.WorkOrder != null ? l.WorkOrder.Description : null,
                l.ErpActivityId,
                l.IsOriginalPlan
            ))
            .ToListAsync();

        return labors;
    }

    [HttpGet]
    public async Task<ActionResult<List<LaborDto>>> GetLabors(
        [FromQuery] bool? assigned = null,
        [FromQuery] string? status = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool? isOriginalPlan = null)
    {
        var query = _context.Labors
            .AsNoTracking()
            .Include(l => l.Lot)
                .ThenInclude(l => l!.Field)
            .Include(l => l.Type)
            .Include(l => l.Contact)
            .Include(l => l.WorkOrder)
            .Include(l => l.Supplies)
                .ThenInclude(s => s.Supply)
            .Include(l => l.CampaignLot)
            .AsQueryable();

        if (assigned.HasValue)
        {
            if (assigned.Value) query = query.Where(l => l.WorkOrderId != null);
            else query = query.Where(l => l.WorkOrderId == null);
        }

        if (!string.IsNullOrEmpty(status))
        {
            if (Enum.TryParse<LaborStatus>(status, out var s))
                query = query.Where(l => l.Status == s);
        }

        if (isOriginalPlan.HasValue)
        {
            query = query.Where(l => l.IsOriginalPlan == isOriginalPlan.Value);
        }

        query = sortBy?.ToLower() switch
        {
            "priority" => query.OrderBy(l => l.Priority).ThenBy(l => l.EstimatedDate),
            "date" => query.OrderBy(l => l.EstimatedDate).ThenBy(l => l.Priority),
            _ => query.OrderByDescending(l => l.CreatedAt)
        };

        var labors = await query.ToListAsync();
        return labors.Select(MapToDto).ToList();
    }

    [HttpGet("unassigned")]
    public async Task<ActionResult<List<LaborDto>>> GetUnassignedLabors([FromQuery] string? sortBy = null)
    {
        return await GetLabors(assigned: false, sortBy: sortBy);
    }

    [HttpGet("unassigned/count")]
    public async Task<ActionResult<int>> GetUnassignedCount()
    {
        var count = await _context.Labors
            .AsNoTracking()
            .CountAsync(l => l.WorkOrderId == null);
        return count;
    }

    [HttpGet("validate-rotation-activity")]
    public async Task<ActionResult<string?>> ValidateRotationActivity([FromQuery] Guid campaignLotId, [FromQuery] DateOnly date, [FromQuery] Guid activityId)
    {
        var result = await _validationService.ValidateLaborActivityMatchesRotationAsync(campaignLotId, date, activityId);
        Console.WriteLine($"[DEBUG] Validation Result for {campaignLotId}: '{result}'");
        return Ok(result);
    }

    [HttpPost("bulk-from-strategy")]
    public async Task<IActionResult> CreateBulkFromStrategy([FromBody] BulkFromStrategyRequest request)
    {
        if (request.StrategyId == Guid.Empty || request.CampaignLotIds.Count == 0)
            return BadRequest("Estrategia y lotes son obligatorios.");

        var strategy = await _context.CropStrategies
            .Include(s => s.Items)

            .FirstOrDefaultAsync(s => s.Id == request.StrategyId);

        if (strategy == null) return NotFound("Estrategia no encontrada.");

        var campaignLots = await _context.CampaignLots
            .Include(cl => cl.Lot)
            .Where(cl => request.CampaignLotIds.Contains(cl.Id))
            .ToListAsync();

        var status = Enum.TryParse<LaborStatus>(request.Status, out var st) ? st : LaborStatus.Planned;
        var createdCount = 0;
        var warnings = new List<string>();

        foreach (var lot in campaignLots)
        {
            foreach (var sItem in strategy.Items.OrderBy(i => i.DayOffset))
            {
                // Find override if any
                var ovr = request.LaborsOverride?.FirstOrDefault(o => o.CampaignLotId == lot.Id && o.StrategyItemId == sItem.Id);

                var executionDate = (ovr != null) ? ovr.Date : request.BaseDate.AddDays(sItem.DayOffset);
                var hectares = (ovr != null) ? ovr.Hectares : lot.ProductiveArea;
                var laborTypeId = (ovr != null) ? ovr.LaborTypeId : sItem.LaborTypeId;
                var contactId = (ovr != null) ? ovr.ContactId : null;
                var isExternal = (ovr != null) ? ovr.IsExternalBilling : false;

                // Validate activity match
                if (sItem.ErpActivityId.HasValue)
                {
                    var warning = await _validationService.ValidateLaborActivityMatchesRotationAsync(lot.Id, DateOnly.FromDateTime(executionDate), sItem.ErpActivityId.Value);
                    if (warning != null)
                    {
                        warnings.Add($"Lote {lot.Lot.Name}: {warning}");
                    }
                }

                var labor = new Labor
                {
                    Id = Guid.NewGuid(),
                    LotId = lot.LotId,
                    CampaignLotId = lot.Id,
                    LaborTypeId = laborTypeId,
                    Status = status,
                    Mode = status == LaborStatus.Realized ? LaborMode.Realized : LaborMode.Planned,
                    ExecutionDate = executionDate,
                    EstimatedDate = executionDate,
                    Hectares = hectares,
                    ContactId = contactId,
                    IsExternalBilling = isExternal,
                    SourceStrategyId = strategy.Id,
                    IsOriginalPlan = request.IsOriginalPlan,
                    CreatedAt = DateTime.UtcNow,
                    Notes = $"Generada desde estrategia: {strategy.Name}{(request.IsOriginalPlan ? " [PLANEAMIENTO ORIGINAL]" : "")}"
                };

                if (!string.IsNullOrEmpty(sItem.DefaultSuppliesJson))
                {
                    var defaults = System.Text.Json.JsonSerializer.Deserialize(sItem.DefaultSuppliesJson, AppJsonSerializerContext.Default.ListStrategySupplyDefault);
                    if (defaults != null)
                    {
                        foreach (var ds in defaults.Where(s => s.SupplyId != Guid.Empty))
                        {
                            labor.Supplies.Add(new LaborSupply
                            {
                                Id = Guid.NewGuid(),
                                LaborId = labor.Id,
                                SupplyId = ds.SupplyId,
                                PlannedDose = ds.Dose,
                                PlannedHectares = labor.Hectares,
                                PlannedTotal = ds.Dose * labor.Hectares,
                                UnitOfMeasure = ds.DoseUnit,
                                RealDose = status == LaborStatus.Realized ? ds.Dose : null,
                                RealHectares = status == LaborStatus.Realized ? labor.Hectares : null,
                                RealTotal = status == LaborStatus.Realized ? ds.Dose * labor.Hectares : null
                            });
                        }
                    }
                }

                _context.Labors.Add(labor);
                createdCount++;
            }
        }

        await _context.SaveChangesAsync();
        return Ok(new { Created = createdCount, Warnings = warnings });
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
                .SetProperty(l => l.Status, LaborStatus.Planned));

        return Ok(new { Updated = updated });
    }

    [HttpPatch("{id:guid}/priority")]
    public async Task<IActionResult> UpdatePriority(Guid id, [FromBody] PriorityRequest request)
    {
        var labor = await _context.Labors.FindAsync(id);
        if (labor == null) return NotFound();

        labor.Priority = (LaborPriority)request.Priority;
        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpPatch("{id:guid}/unassign")]
    public async Task<IActionResult> UnassignLabor(Guid id)
    {
        var labor = await _context.Labors.FindAsync(id);
        if (labor == null) return NotFound();

        labor.WorkOrderId = null;
        labor.Status = LaborStatus.Pending;
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
                warnings.Add("La superficie de la labor supera el área productiva del lote asignado.");
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
        var dto = new LaborDto(
            labor.Id,
            labor.WorkOrderId,
            labor.LotId,
            labor.CampaignLotId ?? Guid.Empty,
            labor.LaborTypeId,
            labor.ErpActivityId,
            labor.Status.ToString(),
            labor.Mode.ToString(),
            labor.ExecutionDate,
            labor.EstimatedDate,
            labor.Hectares,
            labor.EffectiveArea,
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
            labor.PlannedLaborId,
            labor.Priority,
            labor.SupplyWithdrawalNotes,
            labor.IsOriginalPlan,
            labor.Contact?.FullName,
            labor.WorkOrder?.OTNumber
        );
        dto.SourceStrategyId = labor.SourceStrategyId;
        dto.SourceStrategyName = labor.SourceStrategy?.Name;
        dto.CampaignId = labor.CampaignLot?.CampaignId;
        return dto;
    }
}

public class PriorityRequest
{
    public int Priority { get; set; }
}

public class BulkAssignRequest
{
    public List<Guid> LaborIds { get; set; } = new();
    public Guid WorkOrderId { get; set; }
}
