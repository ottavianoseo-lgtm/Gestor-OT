using System.Security.Cryptography;
using GestorOT.Application.Interfaces;
using GestorOT.Domain.Entities;
using GestorOT.Domain.Enums;
using GestorOT.Shared.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestorOT.Api.Controllers;

[ApiController]
[Route("api/share")]
public class ShareController : ControllerBase
{
    private readonly IApplicationDbContext _context;

    public ShareController(IApplicationDbContext context)
    {
        _context = context;
    }

    [HttpPost("generate/{workOrderId:guid}")]
    public async Task<ActionResult<ShareLinkDto>> GenerateLink(Guid workOrderId, [FromQuery] int expiryDays = 7)
    {
        var wo = await _context.WorkOrders.FindAsync(workOrderId);
        if (wo == null)
            return NotFound("Orden de trabajo no encontrada.");

        var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace("+", "-").Replace("/", "_").TrimEnd('=');

        var tokenHash = ComputeHash(rawToken);

        var sharedToken = new SharedToken
        {
            Id = Guid.NewGuid(),
            WorkOrderId = workOrderId,
            TenantId = wo.TenantId,
            TokenHash = tokenHash,
            ExpiresAt = DateTime.UtcNow.AddDays(expiryDays),
            IsRevoked = false,
            CreatedAt = DateTime.UtcNow
        };

        _context.SharedTokens.Add(sharedToken);
        await _context.SaveChangesAsync();

        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var publicUrl = $"{baseUrl}/public/ot/{rawToken}";

        return new ShareLinkDto(publicUrl, sharedToken.ExpiresAt);
    }

    [HttpGet("validate/{token}")]
    public async Task<ActionResult<PublicWorkOrderDto>> ValidateToken(string token)
    {
        var tokenHash = ComputeHash(token);

        var sharedToken = await _context.SharedTokens
            .AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash);

        if (sharedToken == null)
            return NotFound("Token inválido.");

        if (sharedToken.IsRevoked)
            return BadRequest("Este enlace ha sido revocado.");

        if (sharedToken.ExpiresAt < DateTime.UtcNow)
            return BadRequest("Este enlace ha expirado.");

        var wo = await _context.WorkOrders
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Include(w => w.Field)
            .Include(w => w.Labors)
                .ThenInclude(l => l.Type)
            .Include(w => w.Labors)
                .ThenInclude(l => l.Lot)
            .Include(w => w.Labors)
                .ThenInclude(l => l.Supplies)
                    .ThenInclude(s => s.Supply)
            .FirstOrDefaultAsync(w => w.Id == sharedToken.WorkOrderId);

        if (wo == null)
            return NotFound("Orden de trabajo no encontrada.");

        var labors = wo.Labors.OrderBy(l => l.CreatedAt).Select(l => new PublicLaborDto(
            l.Id,
            l.Type?.Name ?? "Labor",
            l.Status.ToString(),
            l.Hectares,
            l.LotId,
            l.Lot?.Name,
            l.Supplies.Select(s => new PublicLaborSupplyDto(
                s.Id,
                s.SupplyId,
                s.Supply?.ItemName ?? "Insumo",
                s.PlannedDose,
                s.RealDose,
                s.PlannedTotal,
                s.RealTotal,
                s.UnitOfMeasure,
                s.Supply?.UnitB
            )).ToList()
        )).ToList();

        return new PublicWorkOrderDto(
            wo.Id,
            wo.Description,
            wo.Status,
            wo.AssignedTo,
            wo.DueDate,
            wo.Field?.Name,
            labors
        );
    }

    [HttpPost("realize/{token}/labor/{laborId:guid}")]
    public async Task<IActionResult> RealizeLaborPublic(string token, Guid laborId, [FromBody] List<PublicLaborSupplyDto> realSupplies)
    {
        var tokenHash = ComputeHash(token);

        var sharedToken = await _context.SharedTokens
            .AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash);

        if (sharedToken == null)
            return NotFound("Token inválido.");

        if (sharedToken.IsRevoked || sharedToken.ExpiresAt < DateTime.UtcNow)
            return BadRequest("Este enlace no es válido.");

        var labor = await _context.Labors
            .IgnoreQueryFilters()
            .Include(l => l.Supplies)
            .FirstOrDefaultAsync(l => l.Id == laborId && l.WorkOrder!.Id == sharedToken.WorkOrderId);

        if (labor == null)
            return NotFound("Labor no encontrada.");

        if (labor.Status == LaborStatus.Realized)
            return BadRequest("La labor ya fue realizada.");

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

    [HttpPost("revoke/{workOrderId:guid}")]
    public async Task<IActionResult> RevokeLinks(Guid workOrderId)
    {
        var wo = await _context.WorkOrders.FindAsync(workOrderId);
        if (wo == null)
            return NotFound("Orden de trabajo no encontrada.");

        var tokens = await _context.SharedTokens
            .IgnoreQueryFilters()
            .Where(t => t.WorkOrderId == workOrderId && !t.IsRevoked)
            .ToListAsync();

        foreach (var t in tokens)
            t.IsRevoked = true;

        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("realize-from-html")]
    public async Task<IActionResult> RealizeFromHtml([FromBody] HtmlExecutionRequest request)
    {
        var sharedToken = await _context.SharedTokens
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.TokenHash == request.Token && t.WorkOrderId == request.WorkOrderId);

        if (sharedToken == null) return NotFound("Token inválido.");
        if (sharedToken.IsRevoked || sharedToken.IsUsed || sharedToken.ExpiresAt < DateTime.UtcNow)
            return BadRequest("El enlace no es válido, ya fue usado o expiró.");

        var strategy = _context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync<IActionResult>(async () =>
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                foreach (var laborReq in request.Labors)
                {
                    var source = await _context.Labors
                        .IgnoreQueryFilters()
                        .Include(l => l.Supplies)
                        .FirstOrDefaultAsync(l => l.Id == laborReq.Id && l.WorkOrderId == request.WorkOrderId);

                    if (source == null || source.Mode != LaborMode.Planned || source.Status == LaborStatus.Realized)
                        continue;

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
                        Hectares = laborReq.RealHectares,
                        EffectiveArea = laborReq.RealHectares,
                        Rate = source.Rate,
                        RateUnit = source.RateUnit,
                        PlannedDose = source.PlannedDose,
                        RealizedDose = source.PlannedDose,
                        PlannedLaborId = source.Id,
                        CreatedAt = DateTime.UtcNow,
                        TenantId = sharedToken.TenantId
                    };

                    foreach (var s in source.Supplies)
                    {
                        var realS = laborReq.Supplies.FirstOrDefault(rs => rs.Id == s.Id);
                        var dose = realS?.RealDose ?? s.PlannedDose;

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
                }

                sharedToken.IsUsed = true;
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new { Message = "Labores registradas con éxito." });
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        });
    }

    private static string ComputeHash(string input)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(input);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexStringLower(hash);
    }
}
