using GestorOT.Shared.Dtos;
using System.Net.Http.Json;

namespace GestorOT.Client.Services;

public class CatalogCache
{
    private readonly HttpClient _http;
    private Dictionary<string, (object Data, DateTime CachedAt)> _cache = new();
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);

    public CatalogCache(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<LaborTypeDto>> GetLaborTypesAsync()
    {
        return (await GetOrFetchAsync("labor-types",
            () => _http.GetFromJsonAsync<List<LaborTypeDto>>("api/catalogs/labor-types")))!;
    }

    public async Task<List<ErpActivityDto>> GetActivitiesAsync()
    {
        return (await GetOrFetchAsync("activities",
            () => _http.GetFromJsonAsync<List<ErpActivityDto>>("api/catalogs/activities")))!;
    }

    public async Task<List<ContactDto>> GetContactsAsync()
    {
        return (await GetOrFetchAsync("contacts",
            () => _http.GetFromJsonAsync<List<ContactDto>>("api/catalogs/contacts")))!;
    }

    public async Task<List<InventoryDto>> GetInventoryAsync()
    {
        return (await GetOrFetchAsync("inventory",
            () => _http.GetFromJsonAsync<List<InventoryDto>>("api/inventory")))!;
    }

    public async Task<List<FieldDto>> GetFieldsAsync()
    {
        return (await GetOrFetchAsync("fields",
            () => _http.GetFromJsonAsync<List<FieldDto>>("api/fields")))!;
    }

    public async Task<List<WorkOrderStatusDto>> GetWorkOrderStatusesAsync()
    {
        return (await GetOrFetchAsync("workorder-statuses",
            () => _http.GetFromJsonAsync<List<WorkOrderStatusDto>>("api/workorderstatuses")))!;
    }

    public async Task<List<CropStrategyDto>> GetStrategiesAsync()
    {
        return (await GetOrFetchAsync("strategies",
            () => _http.GetFromJsonAsync<List<CropStrategyDto>>("api/strategies")))!;
    }

    public async Task<List<CampaignLotDto>> GetCampaignLotsAsync(Guid campaignId)
    {
        var key = $"campaign-lots-{campaignId}";
        return (await GetOrFetchAsync(key,
            () => _http.GetFromJsonAsync<List<CampaignLotDto>>($"api/campaigns/{campaignId}/lots")))!;
    }

    public void Invalidate(string key)
    {
        _cache.Remove(key);
    }

    public void InvalidateAll()
    {
        _cache.Clear();
    }

    private async Task<T?> GetOrFetchAsync<T>(string key, Func<Task<T?>> fetch) where T : class
    {
        if (_cache.TryGetValue(key, out var cached) && DateTime.UtcNow - cached.CachedAt < Ttl)
            return (T)cached.Data;

        var data = await fetch();
        if (data != null)
            _cache[key] = (data, DateTime.UtcNow);

        return data;
    }
}
