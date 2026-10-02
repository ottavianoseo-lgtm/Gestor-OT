using System.ComponentModel;
using GestorOT.Mcp.Api;
using ModelContextProtocol.Server;

namespace GestorOT.Mcp.Tools;

[McpServerToolType]
public sealed class CampaignTools
{
    private readonly GestorOtApiClient _api;

    public CampaignTools(GestorOtApiClient api)
    {
        _api = api;
    }

    [McpServerTool(Name = "get_current_user", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Usuario con el que el MCP está conectado a GestorOT (sirve para confirmar la conexión y el rol).")]
    public Task<ApiUser> GetCurrentUser(CancellationToken ct) => _api.GetCurrentUserAsync(ct);

    [McpServerTool(Name = "get_active_campaigns", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Campañas activas y no cerradas, de la más reciente a la más vieja. La primera suele ser 'la campaña actual'. Usar su id para filtrar lotes y labores.")]
    public async Task<List<CampaignView>> GetActiveCampaigns(CancellationToken ct) =>
        (await _api.GetActiveCampaignsAsync(ct)).Select(CampaignView.From).ToList();

    [McpServerTool(Name = "list_campaigns", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Todas las campañas, incluidas las cerradas (Locked), de la más reciente a la más vieja.")]
    public async Task<List<CampaignView>> ListCampaigns(CancellationToken ct) =>
        (await _api.GetCampaignsAsync(ct)).Select(CampaignView.From).ToList();
}

public sealed record CampaignView(Guid Id, string Name, string Status, bool IsActive, DateOnly StartDate, DateOnly EndDate)
{
    public static CampaignView From(ApiCampaignSummary c) =>
        new(c.Id, c.Name, c.Status == 1 ? "Locked" : "Active", c.IsActive, c.StartDate, c.EndDate);
}
