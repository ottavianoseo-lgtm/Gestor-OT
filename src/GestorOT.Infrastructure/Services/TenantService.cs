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
        
        // Automatically create an initial Admin user for the new tenant
        var cleanTenantName = new string(name.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
        if (string.IsNullOrEmpty(cleanTenantName)) cleanTenantName = "empresa";
        var adminEmail = $"admin@{cleanTenantName}.com";

        var adminUser = new UserProfile
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            DisplayName = $"Admin {name}",
            Email = adminEmail,
            Role = "Admin",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.UserProfiles.Add(adminUser);
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

    public async Task<int> EnsureAdminsExistAsync()
    {
        var tenants = await _context.Tenants.IgnoreQueryFilters().ToListAsync();
        var countCreated = 0;

        foreach (var tenant in tenants)
        {
            var hasAdmin = await _context.UserProfiles
                .IgnoreQueryFilters()
                .AnyAsync(u => u.TenantId == tenant.Id && (u.Role == "Admin" || u.Role == "TenantAdmin"));

            if (!hasAdmin)
            {
                var cleanTenantName = new string(tenant.Name.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
                if (string.IsNullOrEmpty(cleanTenantName)) cleanTenantName = "empresa";
                var adminEmail = $"admin@{cleanTenantName}.com";

                var adminUser = new UserProfile
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenant.Id,
                    DisplayName = $"Admin {tenant.Name}",
                    Email = adminEmail,
                    Role = "Admin",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                _context.UserProfiles.Add(adminUser);
                countCreated++;
            }
        }

        if (countCreated > 0)
        {
            await _context.SaveChangesAsync();
        }

        return countCreated;
    }
}
