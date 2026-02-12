namespace GestorOT.Shared.Services;

public record TenantInfo(
    Guid Id,
    string Name,
    string Plan,
    string Currency,
    string MeasurementSystem,
    bool IsActive,
    DateTime CreatedAt,
    int UserCount,
    int FieldCount
);

public interface ITenantService
{
    Task<List<TenantInfo>> GetTenantsAsync();
    Task<TenantInfo?> GetTenantByIdAsync(Guid id);
}
