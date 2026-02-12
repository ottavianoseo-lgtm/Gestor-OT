using GestorOT.Data;
using GestorOT.Shared.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestorOT.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CampaignsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public CampaignsController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<List<CampaignDto>>> GetCampaigns()
    {
        var raw = await _context.Campaigns
            .AsNoTracking()
            .Include(c => c.CampaignFields)
                .ThenInclude(cf => cf.Field)
            .OrderByDescending(c => c.StartDate)
            .ToListAsync();

        var campaigns = raw.Select(c => new CampaignDto(
            c.Id, c.Name, c.StartDate, c.EndDate, c.IsActive,
            ParseStatus(c.Status), c.BudgetTotalUSD, c.BusinessRulesJson, c.CreatedAt,
            c.CampaignFields.Select(cf => new CampaignFieldDto(
                cf.CampaignId, cf.FieldId, cf.Field?.Name, cf.TargetYieldTonHa, cf.AllocatedHectares
            )).ToList()
        )).ToList();

        return campaigns;
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CampaignDto>> GetCampaign(Guid id)
    {
        var c = await _context.Campaigns
            .AsNoTracking()
            .Include(x => x.CampaignFields)
                .ThenInclude(cf => cf.Field)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (c == null)
            return NotFound();

        return new CampaignDto(
            c.Id, c.Name, c.StartDate, c.EndDate, c.IsActive,
            ParseStatus(c.Status), c.BudgetTotalUSD, c.BusinessRulesJson, c.CreatedAt,
            c.CampaignFields.Select(cf => new CampaignFieldDto(
                cf.CampaignId, cf.FieldId, cf.Field?.Name, cf.TargetYieldTonHa, cf.AllocatedHectares
            )).ToList()
        );
    }

    [HttpGet("active")]
    public async Task<ActionResult<List<CampaignSummaryDto>>> GetActiveCampaigns()
    {
        var raw = await _context.Campaigns
            .AsNoTracking()
            .Where(c => c.IsActive && c.Status != "Locked")
            .OrderByDescending(c => c.StartDate)
            .ToListAsync();

        var campaigns = raw.Select(c => new CampaignSummaryDto(
            c.Id, c.Name, ParseStatus(c.Status), c.IsActive
        )).ToList();

        return campaigns;
    }

    private static CampaignStatus ParseStatus(string status) =>
        Enum.TryParse<CampaignStatus>(status, out var s) ? s : CampaignStatus.Planning;

    [HttpPost]
    public async Task<ActionResult<CampaignDto>> CreateCampaign(CampaignDto dto)
    {
        var campaign = new Campaign
        {
            Id = Guid.NewGuid(),
            Name = dto.Name,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            IsActive = dto.IsActive,
            Status = dto.Status.ToString(),
            BudgetTotalUSD = dto.BudgetTotalUSD,
            BusinessRulesJson = dto.BusinessRulesJson,
            CreatedAt = DateTime.UtcNow
        };

        _context.Campaigns.Add(campaign);

        if (dto.Fields != null)
        {
            foreach (var cf in dto.Fields)
            {
                _context.CampaignFields.Add(new CampaignField
                {
                    Id = Guid.NewGuid(),
                    CampaignId = campaign.Id,
                    FieldId = cf.FieldId,
                    TargetYieldTonHa = cf.TargetYieldTonHa,
                    AllocatedHectares = cf.AllocatedHectares
                });
            }
        }

        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetCampaign), new { id = campaign.Id },
            new CampaignDto(campaign.Id, campaign.Name, campaign.StartDate, campaign.EndDate,
                campaign.IsActive, ParseStatus(campaign.Status),
                campaign.BudgetTotalUSD, campaign.BusinessRulesJson, campaign.CreatedAt, new List<CampaignFieldDto>()));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateCampaign(Guid id, CampaignDto dto)
    {
        var campaign = await _context.Campaigns
            .Include(c => c.CampaignFields)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (campaign == null)
            return NotFound();

        if (campaign.Status == "Locked")
            return BadRequest("No se puede modificar una campaña cerrada.");

        campaign.Name = dto.Name;
        campaign.StartDate = dto.StartDate;
        campaign.EndDate = dto.EndDate;
        campaign.IsActive = dto.IsActive;
        campaign.Status = dto.Status.ToString();
        campaign.BudgetTotalUSD = dto.BudgetTotalUSD;
        campaign.BusinessRulesJson = dto.BusinessRulesJson;

        if (dto.Fields != null)
        {
            _context.CampaignFields.RemoveRange(campaign.CampaignFields);
            foreach (var cf in dto.Fields)
            {
                _context.CampaignFields.Add(new CampaignField
                {
                    Id = Guid.NewGuid(),
                    CampaignId = campaign.Id,
                    FieldId = cf.FieldId,
                    TargetYieldTonHa = cf.TargetYieldTonHa,
                    AllocatedHectares = cf.AllocatedHectares
                });
            }
        }

        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpPut("{id:guid}/status")]
    public async Task<IActionResult> UpdateCampaignStatus(Guid id, [FromBody] CampaignStatus newStatus)
    {
        var campaign = await _context.Campaigns.FindAsync(id);
        if (campaign == null)
            return NotFound();

        if (campaign.Status == "Locked" && newStatus != CampaignStatus.Locked)
            return BadRequest("Una campaña cerrada no puede reactivarse.");

        campaign.Status = newStatus.ToString();
        if (newStatus == CampaignStatus.Locked)
            campaign.IsActive = false;

        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteCampaign(Guid id)
    {
        var campaign = await _context.Campaigns.FindAsync(id);
        if (campaign == null)
            return NotFound();

        if (campaign.Status == "Locked")
            return BadRequest("No se puede eliminar una campaña cerrada.");

        var hasWorkOrders = await _context.WorkOrders
            .IgnoreQueryFilters()
            .AnyAsync(w => w.CampaignId == id);

        if (hasWorkOrders)
            return BadRequest("No se puede eliminar una campaña con órdenes de trabajo asociadas.");

        _context.Campaigns.Remove(campaign);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{id:guid}/fields")]
    public async Task<IActionResult> AddField(Guid id, CampaignFieldDto dto)
    {
        var campaign = await _context.Campaigns.FindAsync(id);
        if (campaign == null)
            return NotFound();

        if (campaign.Status == "Locked")
            return BadRequest("No se pueden modificar campos en una campaña cerrada.");

        var exists = await _context.CampaignFields
            .AnyAsync(cf => cf.CampaignId == id && cf.FieldId == dto.FieldId);

        if (exists)
            return BadRequest("El campo ya está asociado a esta campaña.");

        _context.CampaignFields.Add(new CampaignField
        {
            Id = Guid.NewGuid(),
            CampaignId = id,
            FieldId = dto.FieldId,
            TargetYieldTonHa = dto.TargetYieldTonHa,
            AllocatedHectares = dto.AllocatedHectares
        });

        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:guid}/fields/{fieldId:guid}")]
    public async Task<IActionResult> RemoveField(Guid id, Guid fieldId)
    {
        var cf = await _context.CampaignFields
            .FirstOrDefaultAsync(x => x.CampaignId == id && x.FieldId == fieldId);

        if (cf == null)
            return NotFound();

        var campaign = await _context.Campaigns.FindAsync(id);
        if (campaign?.Status == "Locked")
            return BadRequest("No se pueden modificar campos en una campaña cerrada.");

        _context.CampaignFields.Remove(cf);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}
