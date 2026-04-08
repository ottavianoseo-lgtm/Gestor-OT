using GestorOT.Application.Interfaces;
using GestorOT.Domain.Entities;
using GestorOT.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Caching.Hybrid;
using System.Security.Cryptography;
using System.Text.Json.Serialization;

namespace GestorOT.Infrastructure.Services;

public class ErpSyncService : IErpSyncService
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<ErpSyncService> _logger;
    private readonly IEncryptionService _encryptionService;
    private readonly ICurrentTenantService _currentTenantService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly HybridCache _cache;
    private const string BaseUrl = "https://api.gestormax.com";

    public ErpSyncService(
        IApplicationDbContext context, 
        ILogger<ErpSyncService> logger,
        IEncryptionService encryptionService,
        ICurrentTenantService currentTenantService,
        IHttpClientFactory httpClientFactory,
        HybridCache cache)
    {
        _context = context;
        _logger = logger;
        _encryptionService = encryptionService;
        _currentTenantService = currentTenantService;
        _httpClientFactory = httpClientFactory;
        _cache = cache;
    }

    public async Task SyncLaborTypesAsync(Guid? overrideTenantId = null, CancellationToken ct = default)
    {
        var tenantId = overrideTenantId ?? _currentTenantService.TenantId;
        if (tenantId == Guid.Empty) return;

        var tenant = await _context.Tenants.FindAsync(new object[] { tenantId }, ct);
        if (tenant == null || string.IsNullOrEmpty(tenant.GestorMaxApiKeyEncrypted)) return;

        try 
        {
            string apiKey = _encryptionService.Decrypt(tenant.GestorMaxApiKeyEncrypted);
            
            if (apiKey == "ERROR_DECRYPTING")
            {
                _logger.LogError("Could not decrypt Gestor Max API Key for tenant {TenantId}. Please re-enter the API key in the company settings.", tenantId);
                return;
            }

            if (string.IsNullOrWhiteSpace(tenant.GestorMaxDatabaseId))
            {
                _logger.LogWarning("Tenant {TenantId} identifies no Gestor Max Database ID.", tenantId);
                return;
            }

            var url = $"{BaseUrl}/v3/GestorG4/ListActividades?databaseId={tenant.GestorMaxDatabaseId.Trim()}&soloHabilitados=true";
            string finalKey = apiKey.Trim();
            // Logging masked key details for debugging padding issues
            _logger.LogInformation("ERP Request -> DB: {DbId}, Key Length: {KeyLength}, Key Starts: {Start}..., Ends: ...{End}", 
                tenant.GestorMaxDatabaseId.Trim(), finalKey.Length, finalKey[..5], finalKey[^1..]);

            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("X-Api-Key", finalKey);
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");

            var response = await client.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(ct);
                _logger.LogError("Gestor Max API Error ({StatusCode}): {Body}", response.StatusCode, errorBody);
                response.EnsureSuccessStatusCode();
            }

            var erpActivities = await response.Content.ReadFromJsonAsync<List<ErpActivityResponse>>(ct);

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
                        Name = act.Nombre,
                        ExternalErpId = act.Codigo.ToString()
                    };
                    _context.LaborTypes.Add(laborType);
                }
                else 
                {
                    laborType.Name = act.Nombre;
                }
            }

            await _context.SaveChangesAsync(ct);
            _logger.LogInformation("Successfully synced {Count} labor types for tenant {TenantId}.", erpActivities.Count, tenantId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing labor types from Gestor Max.");
            throw; // Re-throw to propagate error
        }
    }

    public async Task SyncContactsAsync(Guid? overrideTenantId = null, CancellationToken ct = default)
    {
        var tenantId = overrideTenantId ?? _currentTenantService.TenantId;
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
            
            if (apiKey == "ERROR_DECRYPTING")
            {
                _logger.LogError("Could not decrypt Gestor Max API Key for tenant {TenantId}.", tenantId);
                return;
            }

            if (string.IsNullOrWhiteSpace(tenant.GestorMaxDatabaseId)) return;

            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("X-Api-Key", apiKey.Trim());
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");

            var url = $"{BaseUrl}/v3/GestorG4/ListPersonas?databaseId={tenant.GestorMaxDatabaseId.Trim()}";
            var response = await client.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(ct);
                _logger.LogError("Gestor Max API Error ({StatusCode}): {Body}", response.StatusCode, errorBody);
                response.EnsureSuccessStatusCode();
            }

             var erpPeople = await response.Content.ReadFromJsonAsync<List<ErpPersonResponse>>(ct);
 
             if (erpPeople == null) return;
 
             foreach (var p in erpPeople)
             {
                 var externalId = p.Id?.ToString();
                 if (string.IsNullOrEmpty(externalId)) continue;
                 
                 var erpPerson = await _context.ErpPeople
                     .IgnoreQueryFilters()
                     .Where(e => e.TenantId == tenantId && e.ExternalErpId == externalId)
                     .FirstOrDefaultAsync(ct);
 
                 if (erpPerson == null)
                 {
                     erpPerson = new ErpPerson
                     {
                         Id = Guid.NewGuid(),
                         TenantId = tenantId,
                         ExternalErpId = externalId,
                         FullName = p.Nombre ?? "Sin Nombre",
                         Alias = p.Alias,
                         VatNumber = p.VatNumber,
                         PersonType = p.PersonType,
                         DocumentType = p.DocumentType,
                         Country = p.Country,
                         ResponsibleTax = p.ResponsibleTax,
                         Group = p.Group,
                         Enabled = p.Enabled,
                         LastSyncDate = DateTime.UtcNow
                     };
                     _context.ErpPeople.Add(erpPerson);
                 }
                 else
                 {
                     erpPerson.FullName = p.Nombre ?? erpPerson.FullName;
                     erpPerson.Alias = p.Alias ?? erpPerson.Alias;
                     erpPerson.VatNumber = p.VatNumber ?? erpPerson.VatNumber;
                     erpPerson.Enabled = p.Enabled;
                     erpPerson.LastSyncDate = DateTime.UtcNow;
                 }
             }
 
             await _context.SaveChangesAsync(ct);
             _logger.LogInformation("Successfully synced {Count} ERP individuals for tenant {TenantId}.", erpPeople.Count, tenantId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing contacts from Gestor Max.");
            throw; // Re-throw
        }
    }

    public async Task SyncStockAsync(Guid? overrideTenantId = null, CancellationToken ct = default)
    {
        var tenantId = overrideTenantId ?? _currentTenantService.TenantId;
        if (tenantId == Guid.Empty) return;

        var tenant = await _context.Tenants.FindAsync(new object[] { tenantId }, ct);
        if (tenant == null || string.IsNullOrEmpty(tenant.GestorMaxApiKeyEncrypted)) return;

        try
        {
            string apiKey = _encryptionService.Decrypt(tenant.GestorMaxApiKeyEncrypted);
            
            if (apiKey == "ERROR_DECRYPTING")
            {
                _logger.LogError("Could not decrypt Gestor Max API Key for tenant {TenantId}.", tenantId);
                return;
            }

            if (string.IsNullOrWhiteSpace(tenant.GestorMaxDatabaseId)) return;

            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("X-Api-Key", apiKey.Trim());
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");

            // Intentamos traer de Cache primero si existe una sincronizacion reciente
            var cacheKey = $"erp_stock_{tenantId}";
            
            var erpStock = await _cache.GetOrCreateAsync(
                cacheKey,
                async cancel => 
                {
                    var url = $"{BaseUrl}/v3/GestorG4/ListConceptos?databaseId={tenant.GestorMaxDatabaseId.Trim()}&soloFisicos=true";
                    _logger.LogInformation("Requesting Stock from: {Url}", url);
                    var response = await client.GetAsync(url, cancel);
                    if (!response.IsSuccessStatusCode)
                    {
                        var errorBody = await response.Content.ReadAsStringAsync(cancel);
                        _logger.LogWarning("Gestor Max API Error ({StatusCode}): {Body}", response.StatusCode, errorBody);
                        return null; 
                    }
                    return await response.Content.ReadFromJsonAsync<List<ErpConceptResponse>>(cancel);
                },
                new HybridCacheEntryOptions { Expiration = TimeSpan.FromMinutes(15) },
                cancellationToken: ct);

            if (erpStock == null) return;

            foreach (var item in erpStock)
            {
                if (string.IsNullOrEmpty(item.Descripcion)) continue;
                
                var inventory = await _context.Inventories
                    .IgnoreQueryFilters()
                    .Where(i => i.TenantId == tenantId && i.ItemName == item.Descripcion)
                    .FirstOrDefaultAsync(ct);

                if (inventory == null)
                {
                    inventory = new Inventory
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenantId,
                        ItemName = item.Descripcion,
                        CurrentStock = item.Cantidad,
                        Unit = item.UnidadA ?? "u",
                        UnitB = item.UnidadB ?? "u",
                        ExternalErpId = item.Descripcion
                    };
                    _context.Inventories.Add(inventory);
                }
                else
                {
                    inventory.ItemName = item.Descripcion;
                    inventory.CurrentStock = item.Cantidad;
                    inventory.Unit = item.UnidadA ?? inventory.Unit;
                    inventory.UnitB = item.UnidadB ?? inventory.UnitB;
                }
            }

            await _context.SaveChangesAsync(ct);
            _logger.LogInformation("Successfully synced stock for {Count} items.", erpStock.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing stock from ERP. Message: {Message}", ex.Message);
            throw; // Re-throw
        }
    }

    // Helper records para mapear la respuesta de Gestor Max
    private record ErpPersonResponse(
        [property: JsonPropertyName("codPersona")] object Id, 
        [property: JsonPropertyName("persona")] string Nombre,
        [property: JsonPropertyName("alias")] string? Alias,
        [property: JsonPropertyName("nroDocumento")] string? VatNumber,
        [property: JsonPropertyName("tipoPersonaAFIP")] string? PersonType,
        [property: JsonPropertyName("tipoDocumentoAFIP")] string? DocumentType,
        [property: JsonPropertyName("paisAFIP")] string? Country,
        [property: JsonPropertyName("tipoResponsableIVA")] string? ResponsibleTax,
        [property: JsonPropertyName("grupoPersonas")] string? Group,
        [property: JsonPropertyName("habilitado")] bool Enabled);

    private record ErpActivityResponse(
        [property: JsonPropertyName("codActividadPerfilImputacion")] object Codigo, 
        [property: JsonPropertyName("actividadPerfilImputacion")] string Nombre);

    private record ErpConceptResponse(
        [property: JsonPropertyName("descripcion")] string Descripcion, 
        [property: JsonPropertyName("cantidad")] double Cantidad, 
        [property: JsonPropertyName("unidadAuxiliar")] string? UnidadA,
        [property: JsonPropertyName("UnidadPrecio")] string? UnidadB,
        [property: JsonPropertyName("precio")] double Precio, 
        [property: JsonPropertyName("totalConcepto")] double totalConcepto);
}
