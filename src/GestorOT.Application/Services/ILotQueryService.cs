using GestorOT.Shared.Dtos;

namespace GestorOT.Application.Services;

public interface ILotQueryService
{
    Task<List<LotDto>> GetAllAsync(CancellationToken ct = default);
    Task<LotDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<GeoJsonFeatureCollection> GetGeoJsonAsync(CancellationToken ct = default);
}
