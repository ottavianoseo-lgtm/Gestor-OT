namespace GestorOT.Application.Interfaces;

public interface IErpSyncService
{
    Task SyncLaborTypesAsync(CancellationToken ct = default);
    Task SyncEmployeesAsync(CancellationToken ct = default);
    Task SyncStockAsync(CancellationToken ct = default);
}
