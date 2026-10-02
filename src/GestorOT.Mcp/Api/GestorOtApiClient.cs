using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol;

namespace GestorOT.Mcp.Api;

/// <summary>
/// Cliente tipado de la API de GestorOT. Toda la lógica de negocio, validaciones y filtro
/// por tenant quedan del lado de la API; acá solo se arma el request.
/// </summary>
public sealed class GestorOtApiClient
{
    private static readonly JsonSerializerOptions ReadOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new NormalizedDecimalConverter() }
    };

    private readonly HttpClient _http;

    public GestorOtApiClient(HttpClient http)
    {
        _http = http;
    }

    public Task<ApiUser> GetCurrentUserAsync(CancellationToken ct) =>
        GetAsync<ApiUser>("api/auth/me", ct);

    // --- Consultas: todas van a los GET .../search de la API, que exigen página y la cortan en SQL ---

    public Task<ApiPaged<ApiCampaignSummary>> SearchCampaignsAsync(ApiQuery query, CancellationToken ct) =>
        GetAsync<ApiPaged<ApiCampaignSummary>>(query.For("api/campaigns/search"), ct);

    public Task<ApiPaged<ApiFieldListItem>> SearchFieldsAsync(ApiQuery query, CancellationToken ct) =>
        GetAsync<ApiPaged<ApiFieldListItem>>(query.For("api/fields/search"), ct);

    public Task<ApiPaged<ApiLot>> SearchLotsAsync(ApiQuery query, CancellationToken ct) =>
        GetAsync<ApiPaged<ApiLot>>(query.For("api/lots/search"), ct);

    public Task<ApiPaged<ApiCampaignLot>> SearchCampaignLotsAsync(Guid campaignId, ApiQuery query, CancellationToken ct) =>
        GetAsync<ApiPaged<ApiCampaignLot>>(query.For($"api/campaigns/{campaignId}/lots/search"), ct);

    public Task<ApiPaged<ApiLabor>> SearchLaborsAsync(ApiQuery query, CancellationToken ct) =>
        GetAsync<ApiPaged<ApiLabor>>(query.For("api/labors/search"), ct);

    public Task<ApiPaged<ApiWorkOrder>> SearchWorkOrdersAsync(ApiQuery query, CancellationToken ct) =>
        GetAsync<ApiPaged<ApiWorkOrder>>(query.For("api/workorders/search"), ct);

    public async Task<ApiWorkOrder?> GetWorkOrderAsync(Guid id, CancellationToken ct)
    {
        using var response = await SendAsync(HttpMethod.Get, $"api/workorders/{id}", null, ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;
        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<ApiWorkOrder>(ReadOptions, ct);
    }

    public Task<ApiPaged<ApiLaborType>> SearchLaborTypesAsync(ApiQuery query, CancellationToken ct) =>
        GetAsync<ApiPaged<ApiLaborType>>(query.For("api/catalogs/labor-types/search"), ct);

    public Task<ApiPaged<ApiActivity>> SearchActivitiesAsync(ApiQuery query, CancellationToken ct) =>
        GetAsync<ApiPaged<ApiActivity>>(query.For("api/catalogs/activities/search"), ct);

    public Task<ApiPaged<ApiContact>> SearchContactsAsync(ApiQuery query, CancellationToken ct) =>
        GetAsync<ApiPaged<ApiContact>>(query.For("api/catalogs/contacts/search"), ct);

    public Task<ApiPaged<ApiInventoryItem>> SearchInventoryAsync(ApiQuery query, CancellationToken ct) =>
        GetAsync<ApiPaged<ApiInventoryItem>>(query.For("api/inventory/search"), ct);

    public async Task<ApiLabor?> GetLaborAsync(Guid id, CancellationToken ct)
    {
        using var response = await SendAsync(HttpMethod.Get, $"api/labors/{id}", null, ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;
        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<ApiLabor>(ReadOptions, ct);
    }

    public async Task<ApiInventoryItem?> GetInventoryItemAsync(Guid id, CancellationToken ct)
    {
        using var response = await SendAsync(HttpMethod.Get, $"api/inventory/{id}", null, ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;
        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<ApiInventoryItem>(ReadOptions, ct);
    }

    public Task<ApiPaged<ApiWorkOrderStatus>> SearchWorkOrderStatusesAsync(ApiQuery query, CancellationToken ct) =>
        GetAsync<ApiPaged<ApiWorkOrderStatus>>(query.For("api/workorderstatuses/search"), ct);

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
        return (await response.Content.ReadFromJsonAsync<T>(ReadOptions, ct))!;
    }

    private async Task<T> SendJsonAsync<T>(HttpMethod method, string path, object body, CancellationToken ct)
    {
        using var response = await SendAsync(method, path, JsonContent.Create(body), ct);
        await EnsureSuccessAsync(response, ct);
        return (await response.Content.ReadFromJsonAsync<T>(ReadOptions, ct))!;
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
