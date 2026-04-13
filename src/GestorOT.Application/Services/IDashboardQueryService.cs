using GestorOT.Shared.Dtos;

namespace GestorOT.Application.Services;

public interface IDashboardQueryService
{
    Task<DashboardStatsDto> GetStatsAsync(CancellationToken ct = default);
    Task<List<RecentWorkOrderDto>> GetRecentOrdersAsync(int count = 10, CancellationToken ct = default);
}
