using GestorOT.Application.Interfaces;
using GestorOT.Application.Services;
using GestorOT.Domain.Entities;
using GestorOT.Domain.Enums;
using GestorOT.Shared.Dtos;
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
        ILogger<ErpSyncService> _logger,
        IEncryptionService encryptionService,
        ICurrentTenantService currentTenantService,
        IHttpClientFactory httpClientFactory,
        HybridCache cache)
    {
        _context = context;
        this._logger = _logger;
        _encryptionService = encryptionService;
        _currentTenantService = currentTenantService;
        _httpClientFactory = httpClientFactory;
        _cache = cache;
    }

    public async Task SyncActivitiesAsync(Guid? overrideTenantId = null, CancellationToken ct = default)
    {
        var tenantId = overrideTenantId ?? _currentTenantService.TenantId;
        if (tenantId == Guid.Empty) return;

        try
        {
            var (client, databaseId) = await GetErpClientAndDatabaseIdAsync(tenantId, ct);
            if (client == null || string.IsNullOrEmpty(databaseId)) return;

            var url = $"{BaseUrl}/v3/GestorG4/ListActividades?databaseId={databaseId}&soloHabilitados=true";
            var response = await client.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("GestorMax ListActividades returned {StatusCode} for Tenant {TenantId}", response.StatusCode, tenantId);
                return;
            }

            var items = await response.Content.ReadFromJsonAsync<List<GenericErpItemResponse>>(ct);
            if (items == null || !items.Any()) return;

            foreach (var item in items)
            {
                var name = item.GetDescription()?.Trim();
                if (string.IsNullOrWhiteSpace(name) || name == "Sin Descripción") continue;

                var externalId = item.GetCode().ToString();

                var existing = await _context.ErpActivities
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(a => a.TenantId == tenantId && a.ExternalErpId == externalId, ct);

                if (existing == null)
                {
                    // Si no se encuentra por ExternalErpId, buscamos por nombre dentro del mismo tenant
                    existing = await _context.ErpActivities
                        .IgnoreQueryFilters()
                        .FirstOrDefaultAsync(a => a.TenantId == tenantId && a.Name.ToLower() == name.ToLower(), ct);

                    if (existing != null)
                    {
                        existing.ExternalErpId = externalId;
                        existing.Name = name;
                    }
                    else
                    {
                        _context.ErpActivities.Add(new ErpActivity
                        {
                            Id = Guid.NewGuid(),
                            TenantId = tenantId,
                            ExternalErpId = externalId,
                            Name = name,
                            IsActive = true
                        });
                    }
                }
                else
                {
                    existing.Name = name;
                }
            }

            await _context.SaveChangesAsync(ct);
            _logger.LogInformation("Sincronización de Actividades finalizada para el Tenant {TenantId}.", tenantId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing Activities for Tenant {TenantId}.", tenantId);
        }
    }

    public async Task SyncCatalogAsync(Guid? overrideTenantId = null, CancellationToken ct = default)
    {
        var tenantId = overrideTenantId ?? _currentTenantService.TenantId;
        if (tenantId == Guid.Empty) return;

        var tenant = await _context.Tenants.FindAsync(new object[] { tenantId }, ct);
        if (tenant == null || string.IsNullOrEmpty(tenant.GestorMaxApiKeyEncrypted)) return;

        try
        {
            string apiKey = _encryptionService.Decrypt(tenant.GestorMaxApiKeyEncrypted);
            if (apiKey == "ERROR_DECRYPTING" || string.IsNullOrWhiteSpace(tenant.GestorMaxDatabaseId)) return;

            var url = $"{BaseUrl}/v3/GestorG4/ListConceptos?databaseId={tenant.GestorMaxDatabaseId.Trim()}";
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("X-Api-Key", apiKey.Trim());
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0");

            var response = await client.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode) return;

            var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var erpStock = await response.Content.ReadFromJsonAsync<List<ErpConceptResponse>>(options, ct);
            if (erpStock == null) return;

            foreach (var item in erpStock)
            {
                if (string.IsNullOrEmpty(item.Descripcion)) continue;

                var externalId = item.CodConcepto?.ToString() ?? item.Descripcion;
                var grupo = (item.GrupoConceptos ?? item.GrupoConcepto ?? "").ToUpper().Trim();
                var subGrupo = (item.SubgrupoConceptos ?? item.SubgrupoConcepto ?? "").ToUpper().Trim();

                // 1. Update ErpConcepts (The full catalog)
                var concept = await _context.ErpConcepts
                    .IgnoreQueryFilters()
                    .Where(c => c.TenantId == tenantId && c.ExternalErpId == externalId)
                    .FirstOrDefaultAsync(ct);

                if (concept == null)
                {
                    concept = new ErpConcept
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenantId,
                        ExternalErpId = externalId,
                        Description = item.Descripcion,
                        Stock = item.Cantidad,
                        UnitA = item.UnidadA,
                        UnitB = item.UnidadB,
                        GrupoConcepto = grupo,
                        SubGrupoConcepto = subGrupo,
                        LastSyncDate = DateTime.UtcNow
                    };
                    _context.ErpConcepts.Add(concept);
                }
                else
                {
                    concept.Description = item.Descripcion;
                    concept.Stock = item.Cantidad;
                    concept.UnitA = item.UnidadA;
                    concept.UnitB = item.UnidadB;
                    concept.GrupoConcepto = grupo;
                    concept.SubGrupoConcepto = subGrupo;
                    concept.LastSyncDate = DateTime.UtcNow;
                }

                // 2. Sync stock to activated LaborTypes (if exists)
                if (grupo == "LABOR" || grupo == "LABORES")
                {
                    var laborType = await _context.LaborTypes
                        .IgnoreQueryFilters()
                        .Where(l => l.TenantId == tenantId && (l.ExternalErpId == externalId || l.Name == item.Descripcion))
                        .FirstOrDefaultAsync(ct);

                    if (laborType != null)
                    {
                        laborType.Name = item.Descripcion;
                        laborType.ExternalErpId = externalId;
                    }
                }

                // 3. Sync to Inventories - Auto-create/update all concepts from ListConceptos without filtering by INSUMOS
                var inventory = await _context.Inventories
                    .IgnoreQueryFilters()
                    .Where(i => i.TenantId == tenantId && (i.ExternalErpId == externalId || i.ItemName == item.Descripcion))
                    .FirstOrDefaultAsync(ct);

                var category = !string.IsNullOrWhiteSpace(subGrupo) ? subGrupo : (!string.IsNullOrWhiteSpace(grupo) ? grupo : "General");

                if (inventory == null)
                {
                    inventory = new Inventory
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenantId,
                        ExternalErpId = externalId,
                        Category = category,
                        ItemName = item.Descripcion,
                        CurrentStock = item.Cantidad,
                        UnitA = item.UnidadA ?? "u",
                        UnitB = item.UnidadB ?? "u",
                        Unit = item.UnidadA ?? "u",
                        GrupoConcepto = grupo,
                        SubGrupoConcepto = subGrupo
                    };
                    _context.Inventories.Add(inventory);
                }
                else
                {
                    inventory.CurrentStock = item.Cantidad;
                    inventory.ItemName = item.Descripcion;
                    inventory.ExternalErpId = externalId;
                    if (string.IsNullOrWhiteSpace(inventory.Category) || inventory.Category == "General")
                    {
                        inventory.Category = category;
                    }
                    inventory.UnitA = item.UnidadA ?? inventory.UnitA;
                    inventory.UnitB = item.UnidadB ?? inventory.UnitB;
                    inventory.Unit = item.UnidadA ?? inventory.Unit;
                    inventory.GrupoConcepto = grupo;
                    inventory.SubGrupoConcepto = subGrupo;
                }
            }
            await _context.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing Catalog.");
        }
    }

    public async Task SyncLaborTypesAsync(Guid? overrideTenantId = null, CancellationToken ct = default)
    {
        await SyncCatalogAsync(overrideTenantId, ct);
    }

    public async Task SyncStockAsync(Guid? overrideTenantId = null, CancellationToken ct = default)
    {
        await SyncCatalogAsync(overrideTenantId, ct);
    }

    public async Task TotalSyncAsync(Guid tenantId, CancellationToken ct = default)
    {
        _logger.LogInformation($"Iniciando Sincronización Total para el Tenant {tenantId}...");
        
        // 1. Sincronizar Catálogo (Labores e Insumos)
        await SyncCatalogAsync(tenantId, ct);
        
        // 2. Sincronizar Contactos
        await SyncContactsAsync(tenantId, ct);
        
        // 3. Actividades (si aplica en el futuro)
        await SyncActivitiesAsync(tenantId, ct);

        _logger.LogInformation($"Sincronización Total finalizada para el Tenant {tenantId}.");
    }

    public async Task SyncContactsAsync(Guid? overrideTenantId = null, CancellationToken ct = default)
    {
        var tenantId = overrideTenantId ?? _currentTenantService.TenantId;
        if (tenantId == Guid.Empty) return;

        var tenant = await _context.Tenants.FindAsync(new object[] { tenantId }, ct);
        if (tenant == null || string.IsNullOrEmpty(tenant.GestorMaxApiKeyEncrypted)) return;

        try
        {
            string apiKey = _encryptionService.Decrypt(tenant.GestorMaxApiKeyEncrypted);
            if (apiKey == "ERROR_DECRYPTING" || string.IsNullOrWhiteSpace(tenant.GestorMaxDatabaseId)) return;

            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("X-Api-Key", apiKey.Trim());
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0");

            var url = $"{BaseUrl}/v3/GestorG4/ListPersonas?databaseId={tenant.GestorMaxDatabaseId.Trim()}";
            var response = await client.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode) return;

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
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing Contacts.");
        }
    }

    public async Task<List<ErpCompanyDto>> GetEmpresasAsync(Guid? overrideTenantId = null, CancellationToken ct = default)
    {
        var tenantId = overrideTenantId ?? _currentTenantService.TenantId;
        var result = new List<ErpCompanyDto>();
        try
        {
            var (client, databaseId) = await GetErpClientAndDatabaseIdAsync(tenantId, ct);
            if (client != null && !string.IsNullOrEmpty(databaseId))
            {
                var url = $"{BaseUrl}/v3/GestorG4/ListEmpresas?databaseId={databaseId}";
                var response = await client.GetAsync(url, ct);
                if (response.IsSuccessStatusCode)
                {
                    var items = await response.Content.ReadFromJsonAsync<List<GenericErpItemResponse>>(ct);
                    if (items != null && items.Any())
                    {
                        result = items.Select(i => new ErpCompanyDto(i.GetCode(), i.GetDescription())).ToList();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not fetch ListEmpresas from GestorMax.");
        }

        if (!result.Any())
        {
            result.Add(new ErpCompanyDto(1, "Empresa 1 (Default)"));
        }
        return result;
    }

    public async Task<List<ErpVoucherTypeDto>> GetComprobantesAsync(Guid? overrideTenantId = null, CancellationToken ct = default)
    {
        var tenantId = overrideTenantId ?? _currentTenantService.TenantId;
        var result = new List<ErpVoucherTypeDto>();
        try
        {
            var (client, databaseId) = await GetErpClientAndDatabaseIdAsync(tenantId, ct);
            if (client != null && !string.IsNullOrEmpty(databaseId))
            {
                var url = $"{BaseUrl}/v3/GestorG4/ListComprobantes?databaseId={databaseId}";
                var response = await client.GetAsync(url, ct);
                if (response.IsSuccessStatusCode)
                {
                    var items = await response.Content.ReadFromJsonAsync<List<GenericErpItemResponse>>(ct);
                    if (items != null && items.Any())
                    {
                        result = items.Select(i => new ErpVoucherTypeDto(i.GetCode(), i.GetDescription())).ToList();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not fetch ListComprobantes from GestorMax.");
        }

        if (!result.Any())
        {
            result.Add(new ErpVoucherTypeDto(1, "Comprobante 1 - Imputación G4"));
        }
        return result;
    }

    public async Task<List<ErpCurrencyDto>> GetMonedasAsync(Guid? overrideTenantId = null, CancellationToken ct = default)
    {
        var tenantId = overrideTenantId ?? _currentTenantService.TenantId;
        var result = new List<ErpCurrencyDto>();
        try
        {
            var (client, databaseId) = await GetErpClientAndDatabaseIdAsync(tenantId, ct);
            if (client != null && !string.IsNullOrEmpty(databaseId))
            {
                var url = $"{BaseUrl}/v3/GestorG4/ListMonedas?databaseId={databaseId}";
                var response = await client.GetAsync(url, ct);
                if (response.IsSuccessStatusCode)
                {
                    var items = await response.Content.ReadFromJsonAsync<List<GenericErpItemResponse>>(ct);
                    if (items != null && items.Any())
                    {
                        result = items.Select(i => new ErpCurrencyDto(i.GetCode(), i.GetDescription(), "$")).ToList();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not fetch ListMonedas from GestorMax.");
        }

        if (!result.Any())
        {
            result.Add(new ErpCurrencyDto(1, "Pesos (ARS)", "$"));
            result.Add(new ErpCurrencyDto(2, "Dólares estadounidenses (USD)", "US$"));
        }
        return result;
    }

    private static readonly string[] AgricultureKeywords = [
        "agri", "agricola", "agricultura", "cultivo", "siembra", "cosecha", "fumigacion", "fumigación",
        "fertilizacion", "fertilización", "pulverizacion", "pulverización", "trigo", "maiz", "maíz",
        "soja", "girasol", "cebada", "sorgo", "labor", "labores", "insumo", "insumos", "campo", "lote",
        "grano", "granero", "agro"
    ];

    private static bool MatchesAgriculture(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        string normalized = text.ToLowerInvariant()
            .Replace('á', 'a').Replace('é', 'e').Replace('í', 'i')
            .Replace('ó', 'o').Replace('ú', 'u').Replace('ñ', 'n');
        return AgricultureKeywords.Any(k => normalized.Contains(k));
    }

    public async Task<List<ErpProfileDto>> GetPerfilesAsync(Guid? overrideTenantId = null, CancellationToken ct = default)
    {
        var tenantId = overrideTenantId ?? _currentTenantService.TenantId;
        var result = new List<ErpProfileDto>();
        try
        {
            var (client, databaseId) = await GetErpClientAndDatabaseIdAsync(tenantId, ct);
            if (client != null && !string.IsNullOrEmpty(databaseId))
            {
                var url = $"{BaseUrl}/v3/GestorG4/ListPerfilesImputacion?databaseId={databaseId}&soloHabilitados=true";
                var response = await client.GetAsync(url, ct);
                if (response.IsSuccessStatusCode)
                {
                    var items = await response.Content.ReadFromJsonAsync<List<GenericErpItemResponse>>(ct);
                    if (items != null && items.Any())
                    {
                        var allProfiles = items.Select(i => new ErpProfileDto(i.GetCode(), i.GetDescription())).ToList();
                        var filtered = allProfiles.Where(p => MatchesAgriculture(p.Nombre)).ToList();
                        result = filtered.Any() ? filtered : allProfiles;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not fetch ListPerfilesImputacion from GestorMax.");
        }

        return result;
    }

    public async Task<List<ErpAccountDto>> GetCuentasAsync(Guid? overrideTenantId = null, CancellationToken ct = default)
    {
        var tenantId = overrideTenantId ?? _currentTenantService.TenantId;
        var result = new List<ErpAccountDto>();
        try
        {
            var (client, databaseId) = await GetErpClientAndDatabaseIdAsync(tenantId, ct);
            if (client != null && !string.IsNullOrEmpty(databaseId))
            {
                // 1. Intento directo por ListPlanDeCuentasCuentas (solo imputables/habilitadas)
                var url = $"{BaseUrl}/v3/GestorG4/ListPlanDeCuentasCuentas?databaseId={databaseId}&ocultarSumarias=true&soloHabilitados=true";
                var response = await client.GetAsync(url, ct);
                if (response.IsSuccessStatusCode)
                {
                    var items = await response.Content.ReadFromJsonAsync<List<GenericErpItemResponse>>(ct);
                    if (items != null && items.Any())
                    {
                        result = items
                            .Where(i => !string.IsNullOrWhiteSpace(i.GetDescription()) && i.GetDescription() != "Sin Descripción")
                            .Select(i => new ErpAccountDto(i.GetCode(), i.CodigoCuenta ?? i.GetCode().ToString(), i.GetDescription()))
                            .ToList();
                    }
                }

                // 2. Si requiere codPlan explícito, listar planes y recuperar el plan principal
                if (!result.Any())
                {
                    var plansUrl = $"{BaseUrl}/v3/GestorG4/ListPlanesDeCuentas?databaseId={databaseId}";
                    var plansResp = await client.GetAsync(plansUrl, ct);
                    if (plansResp.IsSuccessStatusCode)
                    {
                        var plans = await plansResp.Content.ReadFromJsonAsync<List<GenericErpItemResponse>>(ct);
                        var mainPlan = plans?.FirstOrDefault();
                        if (mainPlan != null)
                        {
                            var planCode = mainPlan.GetCode();
                            var planCtaUrl = $"{BaseUrl}/v3/GestorG4/ListPlanDeCuentasCuentas?databaseId={databaseId}&codPlan={planCode}&ocultarSumarias=true&soloHabilitados=true";
                            var planCtaResp = await client.GetAsync(planCtaUrl, ct);
                            if (planCtaResp.IsSuccessStatusCode)
                            {
                                var planItems = await planCtaResp.Content.ReadFromJsonAsync<List<GenericErpItemResponse>>(ct);
                                if (planItems != null && planItems.Any())
                                {
                                    result = planItems
                                        .Where(i => !string.IsNullOrWhiteSpace(i.GetDescription()) && i.GetDescription() != "Sin Descripción")
                                        .Select(i => new ErpAccountDto(i.GetCode(), i.CodigoCuenta ?? i.GetCode().ToString(), i.GetDescription()))
                                        .ToList();
                                }
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not fetch ListPlanDeCuentasCuentas from GestorMax.");
        }

        return result.DistinctBy(c => c.CodCuenta).Take(500).ToList();
    }

    public async Task<List<ErpAccountDto>> GetCuentasGestionAsync(Guid? overrideTenantId = null, CancellationToken ct = default)
    {
        var tenantId = overrideTenantId ?? _currentTenantService.TenantId;
        var result = new List<ErpAccountDto>();
        try
        {
            var (client, databaseId) = await GetErpClientAndDatabaseIdAsync(tenantId, ct);
            if (client != null && !string.IsNullOrEmpty(databaseId))
            {
                var url = $"{BaseUrl}/v3/GestorG4/ListCuentasPlanGestionLucius?databaseId={databaseId}&ocultarSumarias=true&soloHabilitados=true";
                var response = await client.GetAsync(url, ct);
                if (response.IsSuccessStatusCode)
                {
                    var items = await response.Content.ReadFromJsonAsync<List<GenericErpItemResponse>>(ct);
                    if (items != null && items.Any())
                    {
                        result = items
                            .Where(i => !string.IsNullOrWhiteSpace(i.GetDescription()) && i.GetDescription() != "Sin Descripción")
                            .Select(i => new ErpAccountDto(i.GetCode(), i.CodigoCuenta ?? i.GetCode().ToString(), i.GetDescription()))
                            .ToList();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not fetch ListCuentasPlanGestionLucius from GestorMax.");
        }

        if (!result.Any())
        {
            return await GetCuentasAsync(overrideTenantId, ct);
        }

        return result.DistinctBy(c => c.CodCuenta).Take(500).ToList();
    }

    public async Task<List<ErpAccountDto>> GetCuentasCentroAsync(Guid? overrideTenantId = null, CancellationToken ct = default)
    {
        var tenantId = overrideTenantId ?? _currentTenantService.TenantId;
        var result = new List<ErpAccountDto>();
        try
        {
            var (client, databaseId) = await GetErpClientAndDatabaseIdAsync(tenantId, ct);
            if (client != null && !string.IsNullOrEmpty(databaseId))
            {
                var url = $"{BaseUrl}/v3/GestorG4/ListCuentasPlanCentrosLucius?databaseId={databaseId}&ocultarSumarias=true&soloHabilitados=true";
                var response = await client.GetAsync(url, ct);
                if (response.IsSuccessStatusCode)
                {
                    var items = await response.Content.ReadFromJsonAsync<List<GenericErpItemResponse>>(ct);
                    if (items != null && items.Any())
                    {
                        result = items
                            .Where(i => !string.IsNullOrWhiteSpace(i.GetDescription()) && i.GetDescription() != "Sin Descripción")
                            .Select(i => new ErpAccountDto(i.GetCode(), i.CodigoCuenta ?? i.GetCode().ToString(), i.GetDescription()))
                            .ToList();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not fetch ListCuentasPlanCentrosLucius from GestorMax.");
        }

        if (!result.Any())
        {
            return await GetCuentasAsync(overrideTenantId, ct);
        }

        return result.DistinctBy(c => c.CodCuenta).Take(500).ToList();
    }

    public Task<List<ErpAccountDto>> GetCuentasContabilidadAsync(Guid? overrideTenantId = null, CancellationToken ct = default)
    {
        return GetCuentasAsync(overrideTenantId, ct);
    }

    public async Task<List<ErpPersonItemDto>> GetPersonasAsync(Guid? overrideTenantId = null, CancellationToken ct = default)
    {
        var tenantId = overrideTenantId ?? _currentTenantService.TenantId;
        var result = new List<ErpPersonItemDto>();
        try
        {
            var (client, databaseId) = await GetErpClientAndDatabaseIdAsync(tenantId, ct);
            if (client != null && !string.IsNullOrEmpty(databaseId))
            {
                var url = $"{BaseUrl}/v3/GestorG4/ListPersonas?databaseId={databaseId}";
                var response = await client.GetAsync(url, ct);
                if (response.IsSuccessStatusCode)
                {
                    var items = await response.Content.ReadFromJsonAsync<List<GenericErpItemResponse>>(ct);
                    if (items != null && items.Any())
                    {
                        result = items.Select(i => new ErpPersonItemDto(i.GetCode(), i.GetDescription())).ToList();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not fetch ListPersonas from GestorMax.");
        }

        return result;
    }

    private async Task<(HttpClient? Client, string? DatabaseId)> GetErpClientAndDatabaseIdAsync(Guid tenantId, CancellationToken ct)
    {
        if (tenantId == Guid.Empty) return (null, null);

        var tenant = await _context.Tenants.FindAsync(new object[] { tenantId }, ct);
        if (tenant == null || string.IsNullOrEmpty(tenant.GestorMaxApiKeyEncrypted) || string.IsNullOrWhiteSpace(tenant.GestorMaxDatabaseId))
            return (null, null);

        string apiKey = _encryptionService.Decrypt(tenant.GestorMaxApiKeyEncrypted);
        if (apiKey == "ERROR_DECRYPTING") return (null, null);

        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", apiKey.Trim());
        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0");

        return (client, tenant.GestorMaxDatabaseId.Trim());
    }

    private record GenericErpItemResponse(
        [property: JsonPropertyName("codEmpresa")] object? CodEmpresa,
        [property: JsonPropertyName("codComprobante")] object? CodComprobante,
        [property: JsonPropertyName("codMoneda")] object? CodMoneda,
        [property: JsonPropertyName("codPerfilImputacion")] object? CodPerfilImputacion,
        [property: JsonPropertyName("codPerfil")] object? CodPerfil,
        [property: JsonPropertyName("codActividadPerfilImputacion")] object? CodPerfilActividad,
        [property: JsonPropertyName("codActividad")] object? CodActividad,
        [property: JsonPropertyName("codPlan")] object? CodPlan,
        [property: JsonPropertyName("codCuenta")] object? CodCuenta,
        [property: JsonPropertyName("codPersona")] object? CodPersona,
        [property: JsonPropertyName("codigo")] object? Codigo,
        [property: JsonPropertyName("codigoCuenta")] string? CodigoCuenta,
        [property: JsonPropertyName("empresa")] string? Empresa,
        [property: JsonPropertyName("persona")] string? Persona,
        [property: JsonPropertyName("comprobante")] string? Comprobante,
        [property: JsonPropertyName("moneda")] string? Moneda,
        [property: JsonPropertyName("perfilImputacion")] string? PerfilImputacion,
        [property: JsonPropertyName("actividadPerfilImputacion")] string? ActividadPerfil,
        [property: JsonPropertyName("actividad")] string? Actividad,
        [property: JsonPropertyName("plan")] string? Plan,
        [property: JsonPropertyName("cuenta")] string? Cuenta,
        [property: JsonPropertyName("descripcion")] string? Descripcion,
        [property: JsonPropertyName("nombre")] string? Nombre)
    {
        public long GetCode()
        {
            var raw = CodEmpresa ?? CodComprobante ?? CodMoneda ?? CodPerfilImputacion ?? CodPerfil ?? CodPerfilActividad ?? CodActividad ?? CodCuenta ?? CodPlan ?? CodPersona ?? Codigo;
            if (raw != null && long.TryParse(raw.ToString(), out var val)) return val;
            return 1;
        }

        public string GetDescription()
        {
            return Persona ?? Empresa ?? Comprobante ?? Moneda ?? PerfilImputacion ?? ActividadPerfil ?? Actividad ?? Cuenta ?? Plan ?? Descripcion ?? Nombre ?? "Sin Descripción";
        }
    }

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
        [property: JsonPropertyName("codConcepto")] object? CodConcepto,
        [property: JsonPropertyName("descripcion")] string Descripcion, 
        [property: JsonPropertyName("cantidad")] double Cantidad, 
        [property: JsonPropertyName("unidadAuxiliar")] string? UnidadA,
        [property: JsonPropertyName("UnidadPrecio")] string? UnidadB,
        [property: JsonPropertyName("grupoConceptos")] string? GrupoConceptos,
        [property: JsonPropertyName("subgrupoConceptos")] string? SubgrupoConceptos,
        [property: JsonPropertyName("grupoConcepto")] string? GrupoConcepto,
        [property: JsonPropertyName("subgrupoConcepto")] string? SubgrupoConcepto);
}

/*
EXAMPLE RESPONSE FROM GESTORMAX (ListConceptos):
{
    "codigo": "50030",
    "codGrupoConceptos": "10002",
    "codSubgrupoConceptos": "20009",
    "codSubClasificacionConceptos": 0,
    "codConcepto": "50030",
    "codigoConcepto": "2010101000",
    "descripcion": "ACEITE AGRICOLA",
    "grupoConceptos": "INSUMOS",
    "subgrupoConceptos": "ADITIVO",
    "tipo": "Concepto",
    "unidadAuxiliar": "HA",
    "UnidadPrecio": "LT"
}
*/
