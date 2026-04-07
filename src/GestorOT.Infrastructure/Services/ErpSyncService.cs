using GestorOT.Application.Interfaces;
using GestorOT.Domain.Entities;
using GestorOT.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Security.Cryptography;

namespace GestorOT.Infrastructure.Services;

public class ErpSyncService : IErpSyncService
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<ErpSyncService> _logger;
    private readonly IEncryptionService _encryptionService;
    private readonly ICurrentTenantService _currentTenantService;
    private readonly IHttpClientFactory _httpClientFactory;
    private const string BaseUrl = "https://api.gestormax.com";

    public ErpSyncService(
        IApplicationDbContext context, 
        ILogger<ErpSyncService> logger,
        IEncryptionService encryptionService,
        ICurrentTenantService currentTenantService,
        IHttpClientFactory httpClientFactory)
    {
        _context = context;
        _logger = logger;
        _encryptionService = encryptionService;
        _currentTenantService = currentTenantService;
        _httpClientFactory = httpClientFactory;
    }

    public async Task SyncLaborTypesAsync(CancellationToken ct = default)
    {
        var tenantId = _currentTenantService.TenantId;
        if (tenantId == Guid.Empty) return;

        var tenant = await _context.Tenants.FindAsync(new object[] { tenantId }, ct);
        if (tenant == null || string.IsNullOrEmpty(tenant.GestorMaxApiKeyEncrypted)) return;

        try 
        {
            string apiKey = _encryptionService.Decrypt(tenant.GestorMaxApiKeyEncrypted);
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);

            var url = $"{BaseUrl}/v3/GestorG4/ListActividades?databaseId={tenant.GestorMaxDatabaseId}&soloHabilitados=true";
            var erpActivities = await client.GetFromJsonAsync<List<ErpActivityResponse>>(url, ct);

            if (erpActivities == null) return;

            foreach (var act in erpActivities)
            {
                var laborType = await _context.LaborTypes
                    .IgnoreQueryFilters()
                    .Where(l => l.TenantId == tenantId && l.ExternalErpId == act.Codigo.ToString())
                    .FirstOrDefaultAsync(ct);

                if (laborType == null)
                {
                    laborType = new LaborType
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenantId,
                        Name = act.Descripcion,
                        ExternalErpId = act.Codigo.ToString()
                    };
                    _context.LaborTypes.Add(laborType);
                }
                else 
                {
                    laborType.Name = act.Descripcion;
                }
            }

            await _context.SaveChangesAsync(ct);
            _logger.LogInformation("Successfully synced {Count} labor types for tenant {TenantId}.", erpActivities.Count, tenantId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing labor types from Gestor Max.");
        }
    }

    public async Task SyncEmployeesAsync(CancellationToken ct = default)
    {
        var tenantId = _currentTenantService.TenantId;
        if (tenantId == Guid.Empty) 
        {
            _logger.LogWarning("No tenant context for ERP Sync.");
            return;
        }

        var tenant = await _context.Tenants.FindAsync(new object[] { tenantId }, ct);
        if (tenant == null || string.IsNullOrEmpty(tenant.GestorMaxApiKeyEncrypted))
        {
            _logger.LogWarning("Tenant {TenantId} has no ERP configuration.", tenantId);
            return;
        }

        try 
        {
            string apiKey = _encryptionService.Decrypt(tenant.GestorMaxApiKeyEncrypted);
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("X-Api-Key", apiKey); // Ajustar segun Auth real de Gestor Max

            var url = $"{BaseUrl}/v3/Gestor/Personas?databaseId={tenant.GestorMaxDatabaseId}";
            var erpPeople = await client.GetFromJsonAsync<List<ErpPersonResponse>>(url, ct);

            if (erpPeople == null) return;

            foreach (var p in erpPeople)
            {
                var employee = await _context.Employees
                    .IgnoreQueryFilters() // Necesario para buscar por ExternalId en todo el universo? 
                    .Where(e => e.TenantId == tenantId && e.ExternalErpId == p.Id.ToString())
                    .FirstOrDefaultAsync(ct);

                if (employee == null)
                {
                    employee = new Employee
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenantId,
                        ExternalErpId = p.Id.ToString(),
                        FullName = p.Nombre,
                        Email = p.Email,
                        Role = EmployeeRole.Agronomo // Por defecto al sincronizar
                    };
                    _context.Employees.Add(employee);
                }
                else 
                {
                    employee.FullName = p.Nombre;
                    employee.Email = p.Email;
                }
            }

            await _context.SaveChangesAsync(ct);
            _logger.LogInformation("Successfully synced {Count} employees for tenant {TenantId}.", erpPeople.Count, tenantId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing employees from Gestor Max.");
            throw;
        }
    }

    public async Task SyncStockAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Syncing Stock/Inventory from Gestor Max...");
        await Task.CompletedTask;
    }

    // Helper records para mapear la respuesta de Gestor Max
    private record ErpPersonResponse(int Id, string Nombre, string? Email);
    private record ErpActivityResponse(int Codigo, string Descripcion);
}
