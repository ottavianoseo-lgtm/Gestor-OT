namespace GestorOT.Application.Interfaces;

public interface IErpSyncService
{
    Task SyncLaborTypesAsync(Guid? tenantId = null, CancellationToken ct = default);
    Task SyncContactsAsync(Guid? tenantId = null, CancellationToken ct = default);
    Task SyncStockAsync(Guid? tenantId = null, CancellationToken ct = default);
}
