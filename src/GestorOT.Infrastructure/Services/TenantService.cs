using GestorOT.Application.Interfaces;
using GestorOT.Domain.Entities;
using GestorOT.Shared.Dtos;
using Microsoft.EntityFrameworkCore;

namespace GestorOT.Infrastructure.Services;

public class TenantService : ITenantService
{
    private readonly IApplicationDbContext _context;
    private readonly IEncryptionService _encryptionService;

    public TenantService(IApplicationDbContext context, IEncryptionService encryptionService)
    {
        _context = context;
        _encryptionService = encryptionService;
    }

    public async Task<List<TenantInfo>> GetTenantsAsync()
    {
        return await _context.Tenants
            .Select(t => new TenantInfo(
                t.Id, t.Name, "Standard", "ARS", "Métrico", true, t.CreatedAt, 0, 0,
                t.GestorMaxApiKeyEncrypted, t.GestorMaxDatabaseId))
            .ToListAsync();
    }

    public async Task<TenantInfo?> GetTenantByIdAsync(Guid id)
    {
        var t = await _context.Tenants.FindAsync(id);
        if (t == null) return null;

        return new TenantInfo(
            t.Id, t.Name, "Standard", "ARS", "Métrico", true, t.CreatedAt, 0, 0,
            t.GestorMaxApiKeyEncrypted, t.GestorMaxDatabaseId);
    }

    public async Task CreateTenantAsync(string name, string? gestorMaxApiKey, string? gestorMaxDatabaseId)
    {
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = name,
            GestorMaxApiKeyEncrypted = !string.IsNullOrWhiteSpace(gestorMaxApiKey) ? _encryptionService.Encrypt(gestorMaxApiKey.Trim()) : null,
            GestorMaxDatabaseId = gestorMaxDatabaseId?.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        _context.Tenants.Add(tenant);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateTenantAsync(Guid id, string name, string? gestorMaxApiKey, string? gestorMaxDatabaseId)
    {
        var tenant = await _context.Tenants.FindAsync(id);
        if (tenant == null) return;

        tenant.Name = name;
        if (!string.IsNullOrWhiteSpace(gestorMaxApiKey))
            tenant.GestorMaxApiKeyEncrypted = _encryptionService.Encrypt(gestorMaxApiKey.Trim());
            
        tenant.GestorMaxDatabaseId = gestorMaxDatabaseId?.Trim();

        await _context.SaveChangesAsync();
    }
}
