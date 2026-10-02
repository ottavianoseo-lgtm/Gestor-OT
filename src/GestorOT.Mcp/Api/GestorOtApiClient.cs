using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol;

namespace GestorOT.Mcp.Api;

/// <summary>
/// Cliente tipado de la API de GestorOT. Toda la lógica de negocio, validaciones y filtro
/// por tenant quedan del lado de la API; acá solo se arma el request.
/// </summary>
public sealed class GestorOtApiClient
{
    private readonly HttpClient _http;

    public GestorOtApiClient(HttpClient http)
    {
        _http = http;
    }

    public Task<ApiUser> GetCurrentUserAsync(CancellationToken ct) =>
        GetAsync<ApiUser>("api/auth/me", ct);

    public Task<List<ApiCampaignSummary>> GetActiveCampaignsAsync(CancellationToken ct) =>
        GetAsync<List<ApiCampaignSummary>>("api/campaigns/active", ct);

    public Task<List<ApiCampaignSummary>> GetCampaignsAsync(CancellationToken ct) =>
        GetAsync<List<ApiCampaignSummary>>("api/campaigns/selector", ct);

    public Task<List<ApiField>> GetFieldsAsync(CancellationToken ct) =>
        GetAsync<List<ApiField>>("api/fields", ct);

    public Task<List<ApiLot>> GetLotsAsync(CancellationToken ct) =>
        GetAsync<List<ApiLot>>("api/lots", ct);

    public Task<List<ApiCampaignLot>> GetCampaignLotsAsync(Guid campaignId, CancellationToken ct) =>
        GetAsync<List<ApiCampaignLot>>($"api/campaigns/{campaignId}/lots", ct);

    public Task<List<ApiLabor>> GetLaborsAsync(
        Guid? campaignId, string? status, bool? assigned, Guid? laborTypeId, string? sortBy, CancellationToken ct)
    {
        var query = new List<string>();
        if (campaignId.HasValue) query.Add($"campaignId={campaignId}");
        if (!string.IsNullOrWhiteSpace(status)) query.Add($"status={Uri.EscapeDataString(status)}");
        if (assigned.HasValue) query.Add($"assigned={assigned.Value.ToString().ToLowerInvariant()}");
        if (laborTypeId.HasValue) query.Add($"laborTypeId={laborTypeId}");
        if (!string.IsNullOrWhiteSpace(sortBy)) query.Add($"sortBy={Uri.EscapeDataString(sortBy)}");

        var path = query.Count == 0 ? "api/labors" : "api/labors?" + string.Join('&', query);
        return GetAsync<List<ApiLabor>>(path, ct);
    }

    public Task<List<ApiLabor>> GetLaborsByLotAsync(Guid lotId, CancellationToken ct) =>
        GetAsync<List<ApiLabor>>($"api/labors/by-lot/{lotId}", ct);

    public Task<ApiPaged<ApiWorkOrder>> GetWorkOrdersPagedAsync(int page, int pageSize, CancellationToken ct) =>
        GetAsync<ApiPaged<ApiWorkOrder>>($"api/workorders/paged?page={page}&pageSize={pageSize}", ct);

    public async Task<ApiWorkOrder?> GetWorkOrderAsync(Guid id, CancellationToken ct)
    {
        using var response = await SendAsync(HttpMethod.Get, $"api/workorders/{id}", null, ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;
        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<ApiWorkOrder>(ct);
    }

    // --- Catálogos ---

    public Task<List<ApiLaborType>> GetLaborTypesAsync(CancellationToken ct) =>
        GetAsync<List<ApiLaborType>>("api/catalogs/labor-types", ct);

    public Task<List<ApiActivity>> GetActivitiesAsync(CancellationToken ct) =>
        GetAsync<List<ApiActivity>>("api/catalogs/activities", ct);

    public Task<List<ApiContact>> GetContactsAsync(CancellationToken ct) =>
        GetAsync<List<ApiContact>>("api/catalogs/contacts", ct);

    public Task<List<ApiInventoryItem>> GetInventoryAsync(string? search, CancellationToken ct) =>
        GetAsync<List<ApiInventoryItem>>(
            string.IsNullOrWhiteSpace(search) ? "api/inventory" : $"api/inventory?search={Uri.EscapeDataString(search)}", ct);

    public Task<List<ApiWorkOrderStatus>> GetWorkOrderStatusesAsync(CancellationToken ct) =>
        GetAsync<List<ApiWorkOrderStatus>>("api/workorderstatuses", ct);

    // --- Escritura ---

    public Task<ApiWorkOrder> CreateWorkOrderAsync(object body, CancellationToken ct) =>
        SendJsonAsync<ApiWorkOrder>(HttpMethod.Post, "api/workorders", body, ct);

    public Task<ApiLaborSaveResponse> CreateLaborAsync(object body, CancellationToken ct) =>
        SendJsonAsync<ApiLaborSaveResponse>(HttpMethod.Post, "api/labors", body, ct);

    /// <summary>
    /// PUT api/labors/{id} reemplaza la labor entera, así que se lee tal cual viene, se cambia solo
    /// lo pedido y se manda de vuelta. Con JsonNode no se pierde ningún campo que el MCP no conozca.
    /// </summary>
    public async Task<ApiLaborSaveResponse> PatchLaborAsync(Guid id, Action<JsonObject> change, CancellationToken ct)
    {
        var labor = await GetAsync<JsonObject>($"api/labors/{id}", ct);
        change(labor);
        return await SendJsonAsync<ApiLaborSaveResponse>(HttpMethod.Put, $"api/labors/{id}", labor, ct);
    }

    // --- Plomería ---

    private async Task<T> GetAsync<T>(string path, CancellationToken ct)
    {
        using var response = await SendAsync(HttpMethod.Get, path, null, ct);
        await EnsureSuccessAsync(response, ct);
        return (await response.Content.ReadFromJsonAsync<T>(ct))!;
    }

    private async Task<T> SendJsonAsync<T>(HttpMethod method, string path, object body, CancellationToken ct)
    {
        using var response = await SendAsync(method, path, JsonContent.Create(body), ct);
        await EnsureSuccessAsync(response, ct);
        return (await response.Content.ReadFromJsonAsync<T>(ct))!;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, HttpContent? content, CancellationToken ct)
    {
        try
        {
            return await _http.SendAsync(new HttpRequestMessage(method, path) { Content = content }, ct);
        }
        catch (HttpRequestException ex)
        {
            throw new McpException($"No se pudo conectar a GestorOT en {_http.BaseAddress}: {ex.Message}");
        }
    }

    /// <summary>
    /// McpException llega al modelo con el mensaje tal cual; cualquier otra excepción el SDK la
    /// reemplaza por un error genérico y el modelo no sabe qué corregir. Los 400/409 de la API
    /// traen el motivo en el cuerpo, por eso se reenvía.
    /// </summary>
    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
            return;

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            throw new McpException(
                $"GestorOT rechazó la sesión ({(int)response.StatusCode}). En claude.ai, volver a conectar el conector; en local, revisar GestorOt:Token o GestorOt:Email/Password.");

        var body = await response.Content.ReadAsStringAsync(ct);
        if (body.Length > 500) body = body[..500] + "…";
        throw new McpException($"GestorOT respondió {(int)response.StatusCode} en {response.RequestMessage?.Method} {response.RequestMessage?.RequestUri?.PathAndQuery}: {body}");
    }
}
