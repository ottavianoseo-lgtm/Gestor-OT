using GestorOT.Shared.Dtos;

namespace GestorOT.Application.Services;

public interface ILotQueryService
{
    Task<List<LotDto>> GetAllAsync(CancellationToken ct = default);
    Task<LotDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<GeoJsonFeatureCollection> GetGeoJsonAsync(CancellationToken ct = default);
    Task<double> CalculateAreaFromWktAsync(string wkt, CancellationToken ct = default);
    Task<List<SurfaceHistoryDto>> GetSurfaceHistoryAsync(Guid lotId, CancellationToken ct = default);
    Task<List<CampaignLotDto>> GetCampaignsByLotAsync(Guid lotId, CancellationToken ct = default);
}
