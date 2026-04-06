using GestorOT.Application.Services;
using GestorOT.Shared.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace GestorOT.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DashboardController : ControllerBase
{
    private readonly IDashboardQueryService _queryService;

    public DashboardController(IDashboardQueryService queryService)
    {
        _queryService = queryService;
    }

    [HttpGet("stats")]
    public async Task<ActionResult<DashboardStatsDto>> GetStats(CancellationToken ct)
    {
        return await _queryService.GetStatsAsync(ct);
    }

    [HttpGet("recent-orders")]
    public async Task<ActionResult<List<RecentWorkOrderDto>>> GetRecentOrders(CancellationToken ct)
    {
        return await _queryService.GetRecentOrdersAsync(10, ct);
    }
}
