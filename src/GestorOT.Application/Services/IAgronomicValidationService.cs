using GestorOT.Shared.Dtos;

namespace GestorOT.Application.Services;

public interface IAgronomicValidationService
{
    Task<List<TankMixAlertDto>> ValidateMixAsync(List<Guid> supplyIds, CancellationToken ct = default);
    Task<bool> ValidateLaborSurfaceAsync(Guid campaignLotId, decimal hectares, CancellationToken ct = default);
    Task<string?> ValidateLaborActivityMatchesRotationAsync(Guid campaignLotId, DateOnly date, Guid activityId, CancellationToken ct = default);
    Task<string?> ValidateLaborDatesInRotationAsync(Guid campaignLotId, DateTime? estimatedDate, DateTime? executionDate, CancellationToken ct = default);
}
