namespace GestorOT.Application.Interfaces;

public record TenantInfo(
    Guid Id, string Name, string Plan, string Currency, string MeasurementSystem,
    bool IsActive, DateTime CreatedAt, int FieldsCount, int UsersCount,
    string? GestorMaxApiKeyEncrypted = null, string? GestorMaxDatabaseId = null);

public interface ITenantService
{
    Task<List<TenantInfo>> GetTenantsAsync();
    Task<TenantInfo?> GetTenantByIdAsync(Guid id);
    Task CreateTenantAsync(string name, string? gestorMaxApiKey, string? gestorMaxDatabaseId);
    Task UpdateTenantAsync(Guid id, string name, string? gestorMaxApiKey, string? gestorMaxDatabaseId);
}
