using System.Security.Cryptography;
using System.Text.Json;
using GestorOT.Application.Interfaces;
using GestorOT.Domain.Entities;
using GestorOT.Domain.Enums;
using GestorOT.Shared.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.EntityFrameworkCore;

namespace GestorOT.Api.Controllers;

[ApiController]
[Route("api/share")]
[IgnoreAntiforgeryToken]
public class ShareController : ControllerBase
{
    private readonly IApplicationDbContext _context;

    public ShareController(IApplicationDbContext context)
    {
        _context = context;
    }

    [HttpPost("generate/{workOrderId:guid}")]
    public async Task<ActionResult<ShareLinkDto>> GenerateLink(Guid workOrderId, [FromBody] GenerateShareLinkRequest request)
    {
        var expiryDays = request.ExpiryDays > 0 ? request.ExpiryDays : 7;
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
            CreatedAt = DateTime.UtcNow,
            Metadata = request.LaborIds != null && request.LaborIds.Any() 
                ? JsonSerializer.Serialize(new { laborIds = request.LaborIds }) 
                : null
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

        // Filter labors if metadata has specific IDs
        List<Labor> filteredLabors = wo.Labors.ToList();
        if (!string.IsNullOrEmpty(sharedToken.Metadata))
        {
            try
            {
                var meta = JsonSerializer.Deserialize<JsonElement>(sharedToken.Metadata);
                var allowedIds = new HashSet<Guid>();

                if (meta.TryGetProperty("laborIds", out var idsProp))
                {
                    foreach (var id in idsProp.EnumerateArray()) allowedIds.Add(id.GetGuid());
                }
                else if (meta.TryGetProperty("laborId", out var idProp))
                {
                    allowedIds.Add(idProp.GetGuid());
                }

                if (allowedIds.Any())
                {
                    filteredLabors = wo.Labors.Where(l => allowedIds.Contains(l.Id)).ToList();
                }
            }
            catch { /* Ignore malformed metadata */ }
        }

        var labors = filteredLabors.OrderBy(l => l.CreatedAt).Select(l => new PublicLaborDto(
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
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> RealizeLaborPublic(string token, Guid laborId, [FromBody] PublicLaborExecutionRequest request)
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
        labor.EffectiveArea = request.RealHectares;
        labor.RealizedDose = request.Supplies.FirstOrDefault()?.RealDose ?? labor.PlannedDose;

        foreach (var realSupply in request.Supplies)
        {
            var existing = labor.Supplies.FirstOrDefault(s => s.Id == realSupply.Id);
            if (existing != null)
            {
                existing.RealDose = realSupply.RealDose ?? realSupply.PlannedDose;
                existing.RealTotal = (realSupply.RealDose ?? realSupply.PlannedDose) * labor.Hectares;
                existing.RealHectares = labor.Hectares;
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
    [IgnoreAntiforgeryToken]
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

                    if (source == null || source.Status == LaborStatus.Realized)
                        continue;

                    // Update existing labor instead of creating a new one
                    source.Status = LaborStatus.Realized;
                    source.ExecutionDate = DateTime.UtcNow;
                    source.EffectiveArea = laborReq.RealHectares; // This is the REAL area
                    
                    source.RealizedDose = laborReq.Supplies.FirstOrDefault()?.RealDose ?? source.PlannedDose;

                    foreach (var s in source.Supplies)
                    {
                        var realS = laborReq.Supplies.FirstOrDefault(rs => rs.Id == s.Id);
                        var dose = realS?.RealDose ?? s.PlannedDose;

                        s.RealDose = dose;
                        s.RealTotal = dose * source.Hectares;
                        s.RealHectares = source.Hectares;
                    }
                }

                sharedToken.IsUsed = true;
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new { Message = "Labores actualizadas con éxito." });
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
