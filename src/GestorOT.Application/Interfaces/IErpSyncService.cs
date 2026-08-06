using GestorOT.Shared.Dtos;

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

    // ERP Metadata lists for Accounting Configurations
    Task<List<ErpCompanyDto>> GetEmpresasAsync(Guid? tenantId = null, CancellationToken ct = default);
    Task<List<ErpVoucherTypeDto>> GetComprobantesAsync(Guid? tenantId = null, CancellationToken ct = default);
    Task<List<ErpCurrencyDto>> GetMonedasAsync(Guid? tenantId = null, CancellationToken ct = default);
    Task<List<ErpProfileDto>> GetPerfilesAsync(Guid? tenantId = null, CancellationToken ct = default);
    Task<List<ErpAccountDto>> GetCuentasAsync(Guid? tenantId = null, CancellationToken ct = default);
    Task<List<ErpAccountDto>> GetCuentasGestionAsync(Guid? tenantId = null, CancellationToken ct = default);
    Task<List<ErpAccountDto>> GetCuentasCentroAsync(Guid? tenantId = null, CancellationToken ct = default);
    Task<List<ErpAccountDto>> GetCuentasContabilidadAsync(Guid? tenantId = null, CancellationToken ct = default);
    Task<List<ErpPersonItemDto>> GetPersonasAsync(Guid? tenantId = null, CancellationToken ct = default);
}

