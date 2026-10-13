using GestorOT.Shared.Dtos;

namespace GestorOT.Application.Interfaces;

public interface IPaseBuilderService
{
    Task<PaseLoteResult> GenerarLoteAsync(
        Guid tenantId,
        List<Guid>? workOrderIds,
        List<Guid>? laborIds,
        string? descripcion = null,
        CancellationToken ct = default);

    Task<IReadOnlyList<PaseLoteDto>> GetLotesAsync(Guid tenantId, CancellationToken ct = default);

    Task<PaseLoteDto?> GetLoteAsync(Guid tenantId, Guid loteId, CancellationToken ct = default);

    /// <summary>Borra el lote y sus pases. False si no existe en el tenant.</summary>
    Task<bool> DeleteLoteAsync(Guid tenantId, Guid loteId, CancellationToken ct = default);

    Task<IReadOnlyList<PendingImputacionItemDto>> GetPendingItemsAsync(Guid tenantId, CancellationToken ct = default);

    Task<IReadOnlyList<AccountConfigurationDto>> GetAccountConfigurationsAsync(Guid tenantId, CancellationToken ct = default);

    Task SaveAccountConfigurationAsync(Guid tenantId, AccountConfigurationDto dto, CancellationToken ct = default);
}
