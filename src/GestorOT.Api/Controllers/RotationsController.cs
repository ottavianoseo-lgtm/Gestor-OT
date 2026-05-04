using GestorOT.Application.Interfaces;
using GestorOT.Application.Services;
using GestorOT.Shared.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestorOT.Api.Controllers;

[ApiController]
[Route("api/campaigns/{campaignId:guid}/lots/{lotId:guid}/rotations")]
public class RotationsController : ControllerBase
{
    private readonly IRotationService _rotationService;
    private readonly IApplicationDbContext _context;

    public RotationsController(IRotationService rotationService, IApplicationDbContext context)
    {
        _rotationService = rotationService;
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<List<RotationDto>>> GetRotations(Guid campaignId, [FromRoute(Name = "lotId")] Guid campaignLotId)
    {
        var rotations = await _rotationService.GetRotationsByCampaignLotAsync(campaignLotId);
        return Ok(rotations);
    }

    [HttpGet("active")]
    public async Task<ActionResult<RotationDto>> GetActiveRotation(Guid campaignId, [FromRoute(Name = "lotId")] Guid campaignLotId, [FromQuery] DateOnly date)
    {
        var rotation = await _rotationService.GetActiveRotationAsync(campaignLotId, date);
        if (rotation == null) return NotFound();
        return Ok(rotation);
    }

    [HttpPost]
    public async Task<ActionResult<RotationResponse>> CreateRotation(Guid campaignId, [FromRoute(Name = "lotId")] Guid campaignLotId, RotationDto dto)
    {
        if (campaignLotId != dto.CampaignLotId) return BadRequest("CampaignLotId mismatch");

        if (await IsCampaignLocked(campaignId))
            return BadRequest("No se pueden crear rotaciones en una campaña bloqueada.");

        try
        {
            var response = await _rotationService.CreateRotationAsync(dto);
            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateRotation(Guid campaignId, Guid id, RotationDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch");

        if (await IsCampaignLocked(campaignId))
            return BadRequest("No se pueden modificar rotaciones en una campaña bloqueada.");

        try
        {
            await _rotationService.UpdateRotationAsync(id, dto);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteRotation(Guid campaignId, Guid id)
    {
        if (await IsCampaignLocked(campaignId))
            return BadRequest("No se pueden eliminar rotaciones en una campaña bloqueada.");

        await _rotationService.DeleteRotationAsync(id);
        return NoContent();
    }

    [HttpGet("validate")]
    public async Task<ActionResult<List<RotationWarning>>> ValidateRotations(Guid campaignId)
    {
        var warnings = await _rotationService.ValidateRotationEndDatesAsync(campaignId);
        return Ok(warnings);
    }

    private async Task<bool> IsCampaignLocked(Guid campaignId)
    {
        var campaign = await _context.Campaigns.AsNoTracking().FirstOrDefaultAsync(c => c.Id == campaignId);
        return campaign?.Status == "Locked";
    }
}
