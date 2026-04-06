using GestorOT.Application.DTOs;

namespace GestorOT.Application.Services;

public interface IStockValidatorService
{
    Task<StockValidationResult> ValidateStockForWorkOrderAsync(Guid workOrderId, CancellationToken ct = default);
    Task<bool> ReserveStockAsync(Guid workOrderId, CancellationToken ct = default);
    Task<bool> ReleaseStockAsync(Guid workOrderId, CancellationToken ct = default);
}
