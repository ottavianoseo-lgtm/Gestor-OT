using GestorOT.Data;
using GestorOT.Shared.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestorOT.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StatsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public StatsController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet("lots/{id:guid}")]
    public async Task<ActionResult<LoteResumenDto>> GetLoteResumen(Guid id)
    {
        var lot = await _context.Lots
            .Include(l => l.Field)
            .FirstOrDefaultAsync(l => l.Id == id);

        if (lot == null) return NotFound();

        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(DateTime.Today);

        var campaignPlot = await _context.CampaignPlots
            .Include(cp => cp.Campaign)
            .Include(cp => cp.Crop)
            .Where(cp => cp.PlotId == id)
            .Where(cp => cp.Campaign != null && cp.Campaign.IsActive)
            .OrderByDescending(cp => cp.Campaign!.StartDate)
            .FirstOrDefaultAsync();

        string? cultivoActual = null;
        int diasDesdeSiembra = 0;
        if (campaignPlot != null)
        {
            var cropName = campaignPlot.Crop?.Name ?? "Sin cultivo";
            var campaignName = campaignPlot.Campaign?.Name ?? "";
            cultivoActual = $"{cropName} - {campaignName}";

            if (campaignPlot.EstimatedStartDate.HasValue)
            {
                diasDesdeSiembra = (today.DayNumber - campaignPlot.EstimatedStartDate.Value.DayNumber);
                if (diasDesdeSiembra < 0) diasDesdeSiembra = 0;
            }
        }

        var ultimaLabor = await _context.Labors
            .Where(l => l.LotId == id && (l.Status == "Completed" || l.Status == "Cerrada"))
            .OrderByDescending(l => l.ExecutionDate ?? l.PlannedDate ?? l.CreatedAt)
            .Select(l => new LaborResumenDto(
                l.Id,
                l.LaborType,
                l.Status,
                l.ExecutionDate ?? l.PlannedDate,
                null
            ))
            .FirstOrDefaultAsync();

        var proximaLabor = await _context.Labors
            .Where(l => l.LotId == id && (l.Status == "Planned" || l.Status == "Pendiente"))
            .OrderBy(l => l.PlannedDate ?? l.CreatedAt)
            .Select(l => new LaborResumenDto(
                l.Id,
                l.LaborType,
                l.Status,
                l.PlannedDate,
                null
            ))
            .FirstOrDefaultAsync();

        var responsable = await _context.WorkOrders
            .Where(w => w.LotId == id && (w.Status == "InProgress" || w.Status == "Pending"))
            .OrderByDescending(w => w.DueDate)
            .Select(w => w.AssignedTo)
            .FirstOrDefaultAsync();

        var superficieHa = campaignPlot?.ProductiveSurfaceHa ?? (lot.Geometry != null ? (decimal)(lot.Geometry.Area / 10000.0) : 0m);

        var resumen = new LoteResumenDto(
            lot.Id,
            lot.Name,
            lot.Field?.Name,
            (decimal)superficieHa,
            cultivoActual,
            diasDesdeSiembra,
            ultimaLabor,
            proximaLabor,
            responsable,
            lot.Status
        );

        return Ok(resumen);
    }

    [HttpGet("labors/{id:guid}")]
    public async Task<ActionResult<LaborDetalleDto>> GetLaborDetalle(Guid id)
    {
        try
        {
            var labor = await _context.Labors
                .AsNoTracking()
                .Include(l => l.Lot)
                .Include(l => l.Supplies)
                    .ThenInclude(s => s.Supply)
                .Include(l => l.WorkOrder)
                .FirstOrDefaultAsync(l => l.Id == id);

            if (labor == null) return NotFound();

            var insumos = labor.Supplies?
                .Where(s => s.Supply != null)
                .Select(s => s.Supply!.ItemName)
                .ToList() ?? new List<string>();

            var responsable = labor.WorkOrder?.AssignedTo
                ?? "Sin Asignar";

            var detalle = new LaborDetalleDto(
                labor.Id,
                labor.LaborType ?? "—",
                labor.Status ?? "—",
                labor.PlannedDate ?? labor.ExecutionDate,
                labor.Lot?.Name ?? "Sin Lote Asignado",
                labor.LotId,
                responsable,
                labor.MachineryUsedId ?? "N/A",
                insumos,
                labor.Hectares,
                labor.Notes
            );

            return Ok(detalle);
        }
        catch (Exception)
        {
            return StatusCode(500, new { error = "Error al obtener detalle de labor" });
        }
    }
}
