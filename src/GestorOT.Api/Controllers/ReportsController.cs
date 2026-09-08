using GestorOT.Application.Interfaces;
using GestorOT.Shared.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace GestorOT.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReportsController : ControllerBase
{
    private readonly ITraceabilityReportService _traceabilityService;

    public ReportsController(ITraceabilityReportService traceabilityService)
    {
        _traceabilityService = traceabilityService;
    }

    [HttpGet("campaign-traceability/{campaignId:guid}")]
    public async Task<ActionResult<CampaignTraceabilityReportDto>> GetCampaignTraceability(
        Guid campaignId,
        CancellationToken ct)
    {
        var result = await _traceabilityService.GetCampaignTraceabilityAsync(campaignId, ct);
        return Ok(result);
    }

    [HttpGet("lot-traceability/{campaignId:guid}/{lotId:guid}")]
    public async Task<ActionResult<LotTraceabilityReportDto>> GetLotTraceability(
        Guid campaignId,
        Guid lotId,
        CancellationToken ct)
    {
        var result = await _traceabilityService.GetLotTraceabilityAsync(campaignId, lotId, ct);
        if (result == null) return NotFound("Lote no encontrado en la campaña especificada.");
        return Ok(result);
    }

    [HttpGet("field-traceability/{campaignId:guid}/{fieldId:guid}")]
    public async Task<ActionResult<FieldTraceabilityReportDto>> GetFieldTraceability(
        Guid campaignId,
        Guid fieldId,
        CancellationToken ct)
    {
        var result = await _traceabilityService.GetFieldTraceabilityAsync(campaignId, fieldId, ct);
        if (result == null) return NotFound("Campo no encontrado en la campaña especificada.");
        return Ok(result);
    }
}
