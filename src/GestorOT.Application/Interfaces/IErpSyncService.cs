namespace GestorOT.Application.Interfaces;

public interface IErpSyncService
{
    Task SyncActivitiesAsync(Guid? tenantId = null, CancellationToken ct = default);
    Task SyncCatalogAsync(Guid? tenantId = null, CancellationToken ct = default);
    Task SyncContactsAsync(Guid? tenantId = null, CancellationToken ct = default);
    Task TotalSyncAsync(Guid tenantId, CancellationToken ct = default);
    
    // Obsolete but kept for compatibility
    Task SyncLaborTypesAsync(Guid? tenantId = null, CancellationToken ct = default);
    Task SyncStockAsync(Guid? tenantId = null, CancellationToken ct = default);
}
