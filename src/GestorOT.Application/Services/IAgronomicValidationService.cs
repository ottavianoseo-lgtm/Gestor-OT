using GestorOT.Shared.Dtos;

namespace GestorOT.Application.Services;

public interface IAgronomicValidationService
{
    Task<List<TankMixAlertDto>> ValidateMixAsync(List<Guid> supplyIds, CancellationToken ct = default);
}
