using GestorOT.Application.Services;
using GestorOT.Shared.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace GestorOT.Api.Controllers;

[ApiController]
[Route("api/campaigns/{campaignId:guid}/lots/{lotId:guid}/rotations")]
public class RotationsController : ControllerBase
{
    private readonly IRotationService _rotationService;

    public RotationsController(IRotationService rotationService)
    {
        _rotationService = rotationService;
    }

    [HttpGet]
    public async Task<ActionResult<List<RotationDto>>> GetRotations(Guid campaignId, Guid lotId)
    {
        var rotations = await _rotationService.GetRotationsByCampaignLotAsync(lotId);
        return Ok(rotations);
    }

    [HttpGet("active")]
    public async Task<ActionResult<RotationDto>> GetActiveRotation(Guid campaignId, Guid lotId, [FromQuery] DateOnly date)
    {
        var rotation = await _rotationService.GetActiveRotationAsync(lotId, date);
        if (rotation == null) return NotFound();
        return Ok(rotation);
    }

    [HttpPost]
    public async Task<ActionResult<RotationResponse>> CreateRotation(Guid campaignId, Guid lotId, RotationDto dto)
    {
        if (lotId != dto.CampaignLotId) return BadRequest("Lot ID mismatch");
        
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
    public async Task<IActionResult> UpdateRotation(Guid id, RotationDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch");
        
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
    public async Task<IActionResult> DeleteRotation(Guid id)
    {
        await _rotationService.DeleteRotationAsync(id);
        return NoContent();
    }

    [HttpGet("validate")]
    public async Task<ActionResult<List<RotationWarning>>> ValidateRotations(Guid campaignId)
    {
        var warnings = await _rotationService.ValidateRotationEndDatesAsync(campaignId);
        return Ok(warnings);
    }
}
