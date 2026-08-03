using GestorOT.Application.Interfaces;
using GestorOT.Application.Services;
using GestorOT.Domain.Entities;
using GestorOT.Shared.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

namespace GestorOT.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LotsController : ControllerBase
{
    private readonly IApplicationDbContext _context;
    private readonly ILotQueryService _queryService;

    public LotsController(IApplicationDbContext context, ILotQueryService queryService)
    {
        _context = context;
        _queryService = queryService;
    }

    [HttpGet]
    public async Task<ActionResult<List<LotDto>>> GetLots(CancellationToken ct)
    {
        return await _queryService.GetAllAsync(ct);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<LotDto>> GetLot(Guid id, CancellationToken ct)
    {
        var result = await _queryService.GetByIdAsync(id, ct);
        if (result == null) return NotFound();
        return result;
    }

    [HttpGet("geojson")]
    public async Task<ActionResult<GeoJsonFeatureCollection>> GetLotsGeoJson(CancellationToken ct)
    {
        return await _queryService.GetGeoJsonAsync(ct);
    }

    [HttpGet("{id:guid}/surface-history")]
    public async Task<ActionResult<List<SurfaceHistoryDto>>> GetSurfaceHistory(Guid id, CancellationToken ct)
    {
        return await _queryService.GetSurfaceHistoryAsync(id, ct);
    }

    [HttpGet("{id:guid}/campaigns")]
    public async Task<ActionResult<List<CampaignLotDto>>> GetLotCampaigns(Guid id, CancellationToken ct)
    {
        return await _queryService.GetCampaignsByLotAsync(id, ct);
    }

    [HttpPost("check-overlap")]
    public async Task<ActionResult<LotOverlapCheckResultDto>> CheckOverlap([FromBody] CheckLotOverlapRequestDto req, CancellationToken ct)
    {
        var result = await _queryService.CheckLotOverlapAsync(req.WktGeometry, req.FieldId, req.ExcludeLotId, ct);
        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<LotDto>> CreateLot(LotDto dto, [FromQuery] Guid? campaignId, [FromQuery] bool overrideOverlap = false, CancellationToken ct = default)
    {
        if (!overrideOverlap && !string.IsNullOrEmpty(dto.WktGeometry) && dto.FieldId != Guid.Empty)
        {
            var overlapResult = await _queryService.CheckLotOverlapAsync(dto.WktGeometry, dto.FieldId, null, ct);
            if (overlapResult.HasOverlap)
            {
                return Conflict(overlapResult);
            }
        }

        Geometry? geometry = null;
        double areaHa = 0;
        var cadastralArea = dto.CadastralArea;

        if (!string.IsNullOrEmpty(dto.WktGeometry))
        {
            var reader = new WKTReader();
            var g = reader.Read(dto.WktGeometry);
            g.SRID = 4326;
            geometry = g;

            areaHa = await _queryService.CalculateAreaFromWktAsync(dto.WktGeometry);
            if (cadastralArea == 0)
            {
                var existingLotIds = await _context.Lots
                    .Where(l => l.FieldId == dto.FieldId)
                    .Select(l => l.Id)
                    .ToListAsync(ct);

                var netArea = await _queryService.CalculateNetNewAreaAsync(dto.WktGeometry, existingLotIds, ct);
                cadastralArea = netArea > 0 ? (decimal)netArea : (decimal)areaHa;
            }
        }

        var lot = new Lot
        {
            Id = Guid.NewGuid(),
            FieldId = dto.FieldId,
            Name = dto.Name,
            Status = dto.Status,
            Geometry = geometry,
            CadastralArea = cadastralArea
        };

        _context.Lots.Add(lot);

        if (campaignId.HasValue && campaignId.Value != Guid.Empty)
        {
            var campaignExists = await _context.Campaigns.AnyAsync(c => c.Id == campaignId.Value);
            if (campaignExists)
            {
                var productiveArea = cadastralArea > 0 ? cadastralArea : (decimal)areaHa;
                _context.CampaignLots.Add(new CampaignLot
                {
                    Id = Guid.NewGuid(),
                    CampaignId = campaignId.Value,
                    LotId = lot.Id,
                    ProductiveArea = productiveArea
                });
            }
        }

        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetLot), new { id = lot.Id },
            new LotDto(lot.Id, lot.FieldId, lot.Name, lot.Status, dto.WktGeometry, null, areaHa, lot.CadastralArea));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateLot(Guid id, LotDto dto)
    {
        // Use FirstOrDefaultAsync (not FindAsync) so tenant query filter is applied
        var lot = await _context.Lots.FirstOrDefaultAsync(l => l.Id == id);
        if (lot == null) return NotFound("El lote no existe.");

        lot.Name = dto.Name;
        lot.Status = dto.Status;
        lot.FieldId = dto.FieldId;

        if (!string.IsNullOrEmpty(dto.WktGeometry))
        {
            var reader = new WKTReader();
            var g = reader.Read(dto.WktGeometry);
            g.SRID = 4326;
            lot.Geometry = g;

            var areaHa = await _queryService.CalculateAreaFromWktAsync(dto.WktGeometry);
            var calculatedArea = (decimal)areaHa;

            if (dto.CadastralArea > 0)
            {
                lot.CadastralArea = dto.CadastralArea;
            }
            else
            {
                lot.CadastralArea = calculatedArea;
            }

            // Automatically assign imported polygon surface as productive area for campaign lots
            var campaignLots = await _context.CampaignLots
                .Where(cl => cl.LotId == lot.Id)
                .ToListAsync();

            foreach (var cl in campaignLots)
            {
                cl.ProductiveArea = lot.CadastralArea;
            }

            // Recalculate field allocated hectares for affected campaigns
            var campaignIds = campaignLots.Select(cl => cl.CampaignId).Distinct().ToList();
            foreach (var campId in campaignIds)
            {
                var campaignField = await _context.CampaignFields
                    .FirstOrDefaultAsync(cf => cf.CampaignId == campId && cf.FieldId == lot.FieldId);

                if (campaignField != null)
                {
                    var lotIdsInCamp = await _context.CampaignLots
                        .Where(cl => cl.CampaignId == campId && cl.Lot!.FieldId == lot.FieldId)
                        .Select(cl => cl.LotId)
                        .ToListAsync();

                    var nonOverlapArea = await _queryService.CalculateNonOverlappingAreaAsync(lotIdsInCamp);
                    campaignField.AllocatedHectares = nonOverlapArea > 0
                        ? (decimal)nonOverlapArea
                        : await _context.CampaignLots
                            .Where(cl => cl.CampaignId == campId && cl.Lot!.FieldId == lot.FieldId)
                            .SumAsync(cl => cl.ProductiveArea);
                }
            }
        }
        else if (dto.CadastralArea > 0)
        {
            // Geometry not updated, but user explicitly edited the cadastral area
            lot.CadastralArea = dto.CadastralArea;
        }

        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteLot(Guid id)
    {
        var lot = await _context.Lots.FirstOrDefaultAsync(l => l.Id == id);
        if (lot == null) return NotFound("El lote no existe.");

        var hasWorkOrders = await _context.Labors.AnyAsync(l => l.LotId == id);
        if (hasWorkOrders)
            return BadRequest("No se puede eliminar un lote que tiene labores u órdenes de trabajo asociadas.");

        _context.Lots.Remove(lot);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}
