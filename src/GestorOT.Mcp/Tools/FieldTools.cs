using System.ComponentModel;
using GestorOT.Mcp.Api;
using ModelContextProtocol.Server;

namespace GestorOT.Mcp.Tools;

[McpServerToolType]
public sealed class FieldTools
{
    private readonly GestorOtApiClient _api;

    public FieldTools(GestorOtApiClient api)
    {
        _api = api;
    }

    [McpServerTool(Name = "list_fields", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Campos (establecimientos) del tenant con cantidad de lotes y superficie catastral total.")]
    public async Task<List<FieldView>> ListFields(
        [Description("Filtra por nombre del campo (contiene, sin distinguir mayúsculas).")] string? nameContains = null,
        [Description("true para incluir los lotes de cada campo (id, nombre, estado, superficie).")] bool includeLots = false,
        CancellationToken ct = default)
    {
        var fields = await _api.GetFieldsAsync(ct);
        return fields
            .Where(f => Matches(f.Name, nameContains))
            .Select(f => new FieldView(
                f.Id, f.Name, f.CodCentro, f.Lots.Count, f.Lots.Sum(l => l.CadastralArea),
                includeLots ? f.Lots : null))
            .ToList();
    }

    [McpServerTool(Name = "list_lots", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Lotes, sin geometría. Con campaignId devuelve los lotes de esa campaña con superficie productiva y cultivo; sin campaignId, todos los lotes del tenant.")]
    public async Task<List<LotView>> ListLots(
        [Description("Id de campaña (ver get_active_campaigns).")] Guid? campaignId = null,
        [Description("Id de campo (ver list_fields).")] Guid? fieldId = null,
        [Description("Filtra por nombre del lote (contiene, sin distinguir mayúsculas).")] string? nameContains = null,
        CancellationToken ct = default)
    {
        IEnumerable<LotView> lots = campaignId.HasValue
            ? (await _api.GetCampaignLotsAsync(campaignId.Value, ct)).Select(l => new LotView(
                l.LotId, l.LotName ?? "", l.FieldId, l.FieldName, l.CadastralArea, l.ProductiveArea, l.CropId, null))
            : (await _api.GetLotsAsync(ct)).Select(l => new LotView(
                l.Id, l.Name, l.FieldId, l.FieldName, l.CadastralArea, null, null, l.Status));

        return lots
            .Where(l => !fieldId.HasValue || l.FieldId == fieldId)
            .Where(l => Matches(l.Name, nameContains))
            .ToList();
    }

    private static bool Matches(string value, string? filter) =>
        string.IsNullOrWhiteSpace(filter) || value.Contains(filter.Trim(), StringComparison.OrdinalIgnoreCase);
}

public sealed record FieldView(Guid Id, string Name, long? CodCentro, int LotCount, decimal CadastralArea, List<ApiLotSummary>? Lots);

/// <summary>ProductiveArea y CropId solo vienen cuando se consulta por campaña.</summary>
public sealed record LotView(Guid Id, string Name, Guid? FieldId, string? FieldName, decimal CadastralArea, decimal? ProductiveArea, Guid? CropId, string? Status);
