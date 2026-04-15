using GestorOT.Application.Interfaces;
using GestorOT.Domain.Entities;
using GestorOT.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Caching.Hybrid;
using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
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
    private readonly IConfiguration _configuration;

    public ErpSyncService(
        IApplicationDbContext context, 
        ILogger<ErpSyncService> logger,
        IEncryptionService encryptionService,
        ICurrentTenantService currentTenantService,
        IHttpClientFactory httpClientFactory,
        HybridCache cache,
        IConfiguration configuration)
    {
        _context = context;
        _logger = logger;
        _encryptionService = encryptionService;
        _currentTenantService = currentTenantService;
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _configuration = configuration;
    }

    private string BaseUrl => _configuration["GestorMaxIntegrator:Url"] ?? "https://integrator.gestormax.com";

    public async Task SyncLaborTypesAsync(Guid? overrideTenantId = null, CancellationToken ct = default)
    {
        var tenantId = overrideTenantId ?? _currentTenantService.TenantId;
        if (tenantId == Guid.Empty) return;

        try 
        {
            // Now calling Intermediary instead of ERP
            var url = $"{BaseUrl}/api/Actividades";
            
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var response = await client.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(ct);
                _logger.LogError("Intermediary API Error ({StatusCode}): {Body}", response.StatusCode, errorBody);
                return;
            }

            var erpActivities = await response.Content.ReadFromJsonAsync<List<ErpActivityResponse>>(ct);

            if (erpActivities == null) return;

            foreach (var act in erpActivities)
            {
                var externalId = act.Codigo?.ToString();
                if (string.IsNullOrEmpty(externalId)) continue;

                var laborType = await _context.LaborTypes
                    .IgnoreQueryFilters()
                    .Where(l => l.TenantId == tenantId && l.ExternalErpId == externalId)
                    .FirstOrDefaultAsync(ct);

                if (laborType == null)
                {
                    laborType = new LaborType
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenantId,
                        Name = act.Nombre,
                        ExternalErpId = externalId
                    };
                    _context.LaborTypes.Add(laborType);
                }
                else 
                {
                    laborType.Name = act.Nombre;
                }
            }

            await _context.SaveChangesAsync(ct);
            _logger.LogInformation("Successfully synced {Count} labor types from Intermediary for tenant {TenantId}.", erpActivities.Count, tenantId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing labor types from Intermediary.");
        }
    }

    public async Task SyncContactsAsync(Guid? overrideTenantId = null, CancellationToken ct = default)
    {
        var tenantId = overrideTenantId ?? _currentTenantService.TenantId;
        if (tenantId == Guid.Empty) return;

        try 
        {
            var url = $"{BaseUrl}/api/Personas";
            
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var response = await client.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(ct);
                _logger.LogError("Intermediary API Error ({StatusCode}): {Body}", response.StatusCode, errorBody);
                return;
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
             _logger.LogInformation("Successfully synced {Count} individuals from Intermediary for tenant {TenantId}.", erpPeople.Count, tenantId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing contacts from Intermediary.");
        }
    }


    public async Task SyncStockAsync(Guid? overrideTenantId = null, CancellationToken ct = default)
    {
        var tenantId = overrideTenantId ?? _currentTenantService.TenantId;
        if (tenantId == Guid.Empty) return;

        try
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            // Cache Key still useful to avoid frequent Intermediary calls
            var cacheKey = $"intermediary_stock_{tenantId}";
            
            var stockItems = await _cache.GetOrCreateAsync(
                cacheKey,
                async cancel => 
                {
                    var url = $"{BaseUrl}/api/Stock";
                    _logger.LogInformation("Requesting Stock from Intermediary: {Url}", url);
                    var response = await client.GetAsync(url, cancel);
                    if (!response.IsSuccessStatusCode)
                    {
                        var errorBody = await response.Content.ReadAsStringAsync(cancel);
                        _logger.LogWarning("Intermediary API Error ({StatusCode}): {Body}", response.StatusCode, errorBody);
                        return null; 
                    }
                    return await response.Content.ReadFromJsonAsync<List<IntermediaryStockResponse>>(cancel);
                },
                new HybridCacheEntryOptions { Expiration = TimeSpan.FromMinutes(15) },
                cancellationToken: ct);

            if (stockItems == null) return;

            foreach (var item in stockItems)
            {
                var itemName = item.Concepto?.Descripcion;
                if (string.IsNullOrEmpty(itemName)) continue;
                
                var inventory = await _context.Inventories
                    .IgnoreQueryFilters()
                    .Where(i => i.TenantId == tenantId && i.ItemName == itemName)
                    .FirstOrDefaultAsync(ct);

                if (inventory == null)
                {
                    inventory = new Inventory
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenantId,
                        ItemName = itemName,
                        CurrentStock = item.Cantidad,
                        Unit = item.Unidad ?? "u",
                        UnitB = item.Unidad ?? "u",
                        ExternalErpId = itemName,
                        GrupoConcepto = item.Concepto?.GrupoConcepto,
                        SubGrupoConcepto = item.Concepto?.SubGrupoConcepto
                    };
                    _context.Inventories.Add(inventory);
                }
                else
                {
                    inventory.ItemName = itemName;
                    inventory.CurrentStock = item.Cantidad;
                    inventory.Unit = item.Unidad ?? inventory.Unit;
                    inventory.UnitB = item.Unidad ?? inventory.UnitB;
                    inventory.GrupoConcepto = item.Concepto?.GrupoConcepto;
                    inventory.SubGrupoConcepto = item.Concepto?.SubGrupoConcepto;
                }
            }

            await _context.SaveChangesAsync(ct);
            _logger.LogInformation("Successfully synced stock from Intermediary for {Count} items.", stockItems.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing stock from Intermediary. Message: {Message}", ex.Message);
        }
    }

    // Helper records para mapear la respuesta del Intermediario
    private record ErpPersonResponse(
        [property: JsonPropertyName("codPersona")] object Id, 
        [property: JsonPropertyName("personaNombre")] string Nombre,
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

    private record IntermediaryStockResponse(
        [property: JsonPropertyName("codConcepto")] int CodConcepto,
        [property: JsonPropertyName("cantidad")] double Cantidad,
        [property: JsonPropertyName("unidad")] string? Unidad,
        [property: JsonPropertyName("concepto")] IntermediaryConceptoResponse? Concepto);

    private record IntermediaryConceptoResponse(
        [property: JsonPropertyName("descripcion")] string Descripcion,
        [property: JsonPropertyName("grupoConcepto")] string? GrupoConcepto,
        [property: JsonPropertyName("subGrupoConcepto")] string? SubGrupoConcepto);
}
