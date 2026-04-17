using GestorOT.Shared.Dtos;

namespace GestorOT.Application.Services;

public interface IRotationService
{
    Task<List<RotationDto>> GetRotationsByCampaignLotAsync(Guid campaignLotId, CancellationToken ct = default);
    Task<RotationDto?> GetActiveRotationAsync(Guid campaignLotId, DateOnly date, CancellationToken ct = default);
    Task<RotationResponse> CreateRotationAsync(RotationDto dto, CancellationToken ct = default);
    Task UpdateRotationAsync(Guid id, RotationDto dto, CancellationToken ct = default);
    Task DeleteRotationAsync(Guid id, CancellationToken ct = default);
    Task<List<RotationWarning>> ValidateRotationEndDatesAsync(Guid campaignId, CancellationToken ct = default);
}
