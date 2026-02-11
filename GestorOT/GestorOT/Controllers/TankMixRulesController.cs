using GestorOT.Data;
using GestorOT.Services;
using GestorOT.Shared.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestorOT.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TankMixRulesController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly AgronomicValidationService _validationService;

    public TankMixRulesController(ApplicationDbContext context, AgronomicValidationService validationService)
    {
        _context = context;
        _validationService = validationService;
    }

    [HttpGet]
    public async Task<ActionResult<List<TankMixRuleDto>>> GetAll()
    {
        var rules = await _context.TankMixRules
            .AsNoTracking()
            .Include(r => r.ProductA)
            .Include(r => r.ProductB)
            .OrderBy(r => r.CreatedAt)
            .Select(r => new TankMixRuleDto(
                r.Id, r.ProductAId, r.ProductBId, r.Severity, r.WarningMessage,
                r.ProductA != null ? r.ProductA.ItemName : null,
                r.ProductB != null ? r.ProductB.ItemName : null
            ))
            .ToListAsync();

        return rules;
    }

    [HttpPost]
    public async Task<ActionResult<TankMixRuleDto>> Create(TankMixRuleDto dto)
    {
        var rule = new TankMixRule
        {
            Id = Guid.NewGuid(),
            ProductAId = dto.ProductAId,
            ProductBId = dto.ProductBId,
            Severity = dto.Severity,
            WarningMessage = dto.WarningMessage,
            CreatedAt = DateTime.UtcNow
        };

        _context.TankMixRules.Add(rule);
        await _context.SaveChangesAsync();

        var created = await _context.TankMixRules
            .AsNoTracking()
            .Include(r => r.ProductA)
            .Include(r => r.ProductB)
            .FirstAsync(r => r.Id == rule.Id);

        return CreatedAtAction(nameof(GetAll), new TankMixRuleDto(
            created.Id, created.ProductAId, created.ProductBId, created.Severity, created.WarningMessage,
            created.ProductA?.ItemName, created.ProductB?.ItemName
        ));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var rule = await _context.TankMixRules.FindAsync(id);
        if (rule == null) return NotFound();

        _context.TankMixRules.Remove(rule);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("validate")]
    public async Task<ActionResult<List<TankMixAlertDto>>> ValidateMix(TankMixValidationRequest request)
    {
        var alerts = await _validationService.ValidateMix(request.SupplyIds);
        return alerts;
    }
}
