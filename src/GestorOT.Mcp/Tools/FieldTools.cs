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
    [Description("Campos (establecimientos), paginados, con cantidad de lotes y superficie catastral total. Para ver los lotes de un campo usar list_lots con fieldId.")]
    public async Task<PageResult<FieldView>> ListFields(
        [Description("Filtra por nombre del campo (contiene, sin distinguir mayúsculas).")] string? search = null,
        [Description("Orden: name (default), lotCount o cadastralArea.")] string? sortBy = null,
        [Description(Paging.SortDir)] string? sortDir = null,
        [Description(Paging.Page)] int page = 1,
        [Description(Paging.PageSize)] int pageSize = Paging.DefaultPageSize,
        CancellationToken ct = default) =>
        PageResult<FieldView>.From(
            await _api.SearchFieldsAsync(new ApiQuery(page, pageSize, sortBy, sortDir).Add("search", search), ct),
            f => new FieldView(f.Id, f.Name, f.CodCentro, f.LotCount, f.CadastralArea));

    [McpServerTool(Name = "list_lots", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Lotes paginados, sin geometría. Con campaignId devuelve los lotes de esa campaña con superficie productiva y cultivo; sin campaignId, los lotes del tenant con su estado.")]
    public async Task<PageResult<LotView>> ListLots(
        [Description("Id de campaña (ver get_active_campaigns).")] Guid? campaignId = null,
        [Description("Id de campo (ver list_fields).")] Guid? fieldId = null,
        [Description("Filtra por nombre del lote (contiene, sin distinguir mayúsculas).")] string? search = null,
        [Description("Estado del lote (ej. Active). Solo sin campaignId.")] string? status = null,
        [Description("Orden: name (default), field, cadastralArea; con campaignId también productiveArea.")] string? sortBy = null,
        [Description(Paging.SortDir)] string? sortDir = null,
        [Description(Paging.Page)] int page = 1,
        [Description(Paging.PageSize)] int pageSize = Paging.DefaultPageSize,
        CancellationToken ct = default)
    {
        var query = new ApiQuery(page, pageSize, sortBy, sortDir)
            .Add("fieldId", fieldId)
            .Add("search", search);

        if (campaignId.HasValue)
            return PageResult<LotView>.From(
                await _api.SearchCampaignLotsAsync(campaignId.Value, query, ct),
                l => new LotView(l.LotId, l.LotName ?? "", l.FieldId, l.FieldName, l.CadastralArea, l.ProductiveArea, l.CropId, null));

        return PageResult<LotView>.From(
            await _api.SearchLotsAsync(query.Add("status", status), ct),
            l => new LotView(l.Id, l.Name, l.FieldId, l.FieldName, l.CadastralArea, null, null, l.Status));
    }
}

public sealed record FieldView(Guid Id, string Name, long? CodCentro, int LotCount, decimal CadastralArea);

/// <summary>ProductiveArea y CropId solo vienen cuando se consulta por campaña.</summary>
public sealed record LotView(Guid Id, string Name, Guid? FieldId, string? FieldName, decimal CadastralArea, decimal? ProductiveArea, Guid? CropId, string? Status);
