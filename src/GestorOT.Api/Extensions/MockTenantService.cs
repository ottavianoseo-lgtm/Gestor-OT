using GestorOT.Application.Interfaces;

namespace GestorOT.Api.Extensions;

public class MockTenantService : ITenantService
{
    private static readonly List<TenantInfo> _tenants = new()
    {
        new TenantInfo(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            "AgroPro S.A.",
            "Enterprise",
            "ARS",
            "Métrico",
            true,
            new DateTime(2024, 1, 15),
            12,
            8,
            null,
            null
        ),
        new TenantInfo(
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            "Campo del Sur SRL",
            "Professional",
            "USD",
            "Métrico",
            true,
            new DateTime(2024, 3, 22),
            6,
            3,
            null,
            null
        ),
        new TenantInfo(
            Guid.Parse("33333333-3333-3333-3333-333333333333"),
            "Los Alamos Agro",
            "Starter",
            "ARS",
            "Métrico",
            true,
            new DateTime(2024, 6, 10),
            3,
            2,
            null,
            null
        ),
        new TenantInfo(
            Guid.Parse("44444444-4444-4444-4444-444444444444"),
            "Estancia La Esperanza",
            "Enterprise",
            "ARS",
            "Métrico",
            false,
            new DateTime(2023, 11, 5),
            8,
            5,
            null,
            null
        ),
        new TenantInfo(
            Guid.Parse("55555555-5555-5555-5555-555555555555"),
            "Grupo Pampeano SA",
            "Professional",
            "USD",
            "Imperial",
            true,
            new DateTime(2025, 1, 8),
            15,
            12,
            null,
            null
        )
    };

    public Task<List<TenantInfo>> GetTenantsAsync()
    {
        return Task.FromResult(_tenants);
    }

    public Task<TenantInfo?> GetTenantByIdAsync(Guid id)
    {
        return Task.FromResult(_tenants.FirstOrDefault(t => t.Id == id));
    }

    public Task CreateTenantAsync(string name, string? gestorMaxApiKey, string? gestorMaxDatabaseId) => Task.CompletedTask;
    public Task UpdateTenantAsync(Guid id, string name, string? gestorMaxApiKey, string? gestorMaxDatabaseId) => Task.CompletedTask;
}
