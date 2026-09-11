using GestorOT.Shared.Dtos;

namespace GestorOT.Application.Services;

public interface ILotQueryService
{
    Task<List<LotDto>> GetAllAsync(CancellationToken ct = default);
    Task<LotDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    /// <summary>
    /// Los lotes con geometría, en GeoJSON. Con <paramref name="campaignId"/> cada feature
    /// suma el cultivo de esa campaña (cropId, cropName, cropColor); sin el parámetro el
    /// payload es el mismo de siempre.
    /// </summary>
    Task<GeoJsonFeatureCollection> GetGeoJsonAsync(Guid? campaignId = null, CancellationToken ct = default);
    Task<GeoJsonFeatureCollection> GetFieldsGeoJsonAsync(CancellationToken ct = default);
    Task<double> CalculateAreaFromWktAsync(string wkt, CancellationToken ct = default);
    Task<double> CalculateNonOverlappingAreaAsync(List<Guid> lotIds, CancellationToken ct = default);
    Task<double> CalculateNetNewAreaAsync(string newWkt, List<Guid> existingLotIds, CancellationToken ct = default);
    Task<List<SurfaceHistoryDto>> GetSurfaceHistoryAsync(Guid lotId, CancellationToken ct = default);
    Task<List<CampaignLotDto>> GetCampaignsByLotAsync(Guid lotId, CancellationToken ct = default);
    Task<LotOverlapCheckResultDto> CheckLotOverlapAsync(string wkt, Guid fieldId, Guid? excludeLotId = null, CancellationToken ct = default);
}
