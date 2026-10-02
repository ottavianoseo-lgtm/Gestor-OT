using System.ComponentModel;
using GestorOT.Mcp.Api;
using ModelContextProtocol.Server;

namespace GestorOT.Mcp.Tools;

[McpServerToolType]
public sealed class CampaignTools
{
    private const string Sorts = "Orden: startDate (default, más reciente primero), endDate o name.";
    private readonly GestorOtApiClient _api;

    public CampaignTools(GestorOtApiClient api)
    {
        _api = api;
    }

    [McpServerTool(Name = "get_current_user", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Usuario con el que el MCP está conectado a GestorOT (sirve para confirmar la conexión y el rol).")]
    public Task<ApiUser> GetCurrentUser(CancellationToken ct) => _api.GetCurrentUserAsync(ct);

    [McpServerTool(Name = "get_active_campaigns", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Campañas activas y no cerradas, paginadas, de la más reciente a la más vieja. La primera suele ser 'la campaña actual'. Usar su id para filtrar lotes y labores.")]
    public async Task<PageResult<CampaignView>> GetActiveCampaigns(
        [Description(Paging.Page)] int page = 1,
        [Description(Paging.PageSize)] int pageSize = Paging.DefaultPageSize,
        CancellationToken ct = default) =>
        PageResult<CampaignView>.From(
            await _api.SearchCampaignsAsync(new ApiQuery(page, pageSize).Add("active", true), ct),
            CampaignView.From);

    [McpServerTool(Name = "list_campaigns", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Campañas, incluidas las cerradas (Locked), paginadas.")]
    public async Task<PageResult<CampaignView>> ListCampaigns(
        [Description("Filtra por nombre (contiene).")] string? search = null,
        [Description("true = solo activas y no cerradas; false = solo inactivas o cerradas; vacío = todas.")] bool? active = null,
        [Description(Sorts)] string? sortBy = null,
        [Description(Paging.SortDir)] string? sortDir = null,
        [Description(Paging.Page)] int page = 1,
        [Description(Paging.PageSize)] int pageSize = Paging.DefaultPageSize,
        CancellationToken ct = default) =>
        PageResult<CampaignView>.From(
            await _api.SearchCampaignsAsync(new ApiQuery(page, pageSize, sortBy, sortDir)
                .Add("search", search)
                .Add("active", active), ct),
            CampaignView.From);
}

public sealed record CampaignView(Guid Id, string Name, string Status, bool IsActive, DateOnly StartDate, DateOnly EndDate)
{
    public static CampaignView From(ApiCampaignSummary c) =>
        new(c.Id, c.Name, c.Status == 1 ? "Locked" : "Active", c.IsActive, c.StartDate, c.EndDate);
}
