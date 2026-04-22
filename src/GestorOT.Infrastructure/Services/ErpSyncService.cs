using GestorOT.Application.Interfaces;
using GestorOT.Application.Services;
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
        // Manual management as requested by user
        await Task.CompletedTask;
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

            var url = $"{BaseUrl}/v3/GestorG4/ListConceptos?databaseId={tenant.GestorMaxDatabaseId.Trim()}&soloFisicos=true";
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
                // 3. Sync stock to Inventories (Insumos) - Auto-create all INSUMOS
                else if (grupo == "INSUMOS")
                {
                    var inventory = await _context.Inventories
                        .IgnoreQueryFilters()
                        .Where(i => i.TenantId == tenantId && (i.ExternalErpId == externalId || i.ItemName == item.Descripcion))
                        .FirstOrDefaultAsync(ct);

                    if (inventory == null)
                    {
                        inventory = new Inventory
                        {
                            Id = Guid.NewGuid(),
                            TenantId = tenantId,
                            ExternalErpId = externalId,
                            ItemName = item.Descripcion,
                            CurrentStock = item.Cantidad,
                            Unit = item.UnidadA ?? "u",
                            UnitB = item.UnidadB ?? "u",
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
                        inventory.GrupoConcepto = grupo;
                        inventory.SubGrupoConcepto = subGrupo;
                    }
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
