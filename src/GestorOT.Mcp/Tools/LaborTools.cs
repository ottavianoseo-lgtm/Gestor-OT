using System.ComponentModel;
using GestorOT.Mcp.Api;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;

namespace GestorOT.Mcp.Tools;

[McpServerToolType]
public sealed class LaborTools
{
    private readonly GestorOtApiClient _api;
    private readonly GestorOtOptions _options;

    public LaborTools(GestorOtApiClient api, IOptions<GestorOtOptions> options)
    {
        _api = api;
        _options = options.Value;
    }

    [McpServerTool(Name = "search_labors", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Busca labores (tareas agronómicas sobre un lote), paginadas. Todos los filtros se combinan. Si hasMore=true, afinar filtros antes que recorrer muchas páginas.")]
    public async Task<PageResult<LaborView>> SearchLabors(
        [Description("Id de campaña. Casi siempre conviene pasarlo (ver get_active_campaigns).")] Guid? campaignId = null,
        [Description("Estado: Planned, AwaitingValidation, Validated, Realized o Pending.")] string? status = null,
        [Description("Modo: Planned o Realized.")] string? mode = null,
        [Description("true = solo las que ya están en una OT; false = solo las sin asignar.")] bool? assigned = null,
        [Description("Id de OT: las labores de esa orden.")] Guid? workOrderId = null,
        [Description("Id de tipo de labor (ver list_labor_types).")] Guid? laborTypeId = null,
        [Description("Id de la persona que la ejecuta (ver list_contacts).")] Guid? contactId = null,
        [Description("Id de lote: historial de labores de ese lote.")] Guid? lotId = null,
        [Description("Id de campo (ver list_fields).")] Guid? fieldId = null,
        [Description("Desde esta fecha inclusive (yyyy-MM-dd). La fecha es la de ejecución, o la estimada si no se ejecutó.")] DateOnly? dateFrom = null,
        [Description("Hasta esta fecha inclusive (yyyy-MM-dd).")] DateOnly? dateTo = null,
        [Description("Texto en lote, campo, tipo de labor o notas (contiene).")] string? search = null,
        [Description("Orden: created (default, más nuevas primero), date, estimatedDate, executionDate, priority, status, lot, field, laborType o hectares.")] string? sortBy = null,
        [Description(Paging.SortDir)] string? sortDir = null,
        [Description(Paging.Page)] int page = 1,
        [Description(Paging.PageSize)] int pageSize = Paging.DefaultPageSize,
        CancellationToken ct = default)
    {
        var query = new ApiQuery(page, pageSize, sortBy, sortDir)
            .Add("campaignId", campaignId)
            .Add("status", status)
            .Add("mode", mode)
            .Add("assigned", assigned)
            .Add("workOrderId", workOrderId)
            .Add("laborTypeId", laborTypeId)
            .Add("contactId", contactId)
            .Add("lotId", lotId)
            .Add("fieldId", fieldId)
            .Add("dateFrom", dateFrom.HasValue ? _options.ToUtc(dateFrom.Value) : null)
            // La API toma dateBefore exclusivo; el modelo piensa en días inclusivos.
            .Add("dateBefore", dateTo.HasValue ? _options.ToUtc(dateTo.Value.AddDays(1)) : null)
            .Add("search", search);

        return PageResult<LaborView>.From(await _api.SearchLaborsAsync(query, ct), LaborView.From);
    }
}

/// <summary>
/// Trae los ids (tipo, actividad, persona, OT) además de los nombres para que el modelo pueda
/// replicar o reasignar una labor sin volver a buscarlos.
/// </summary>
public sealed record LaborView(
    Guid Id, string Status, string Mode, string Priority,
    Guid LaborTypeId, string? LaborType, Guid? ActivityId, string? Activity, Guid? ContactId,
    Guid LotId, string? Lot, string? Field, Guid? CampaignId,
    Guid? WorkOrderId, string? OtNumber,
    DateTime? EstimatedDate, DateTime? ExecutionDate,
    decimal Hectares, decimal EffectiveArea, string? AssignedTo, string? Notes,
    List<LaborSupplyView> Supplies)
{
    public static LaborView From(ApiLabor l) => new(
        l.Id, l.Status, l.Mode, PriorityName(l.Priority),
        l.LaborTypeId, l.LaborTypeName, l.ErpActivityId, l.ErpActivityName, l.ContactId,
        l.LotId, l.LotName, l.FieldName, l.CampaignId,
        l.WorkOrderId, l.OTNumber,
        l.EstimatedDate, l.ExecutionDate,
        l.Hectares, l.EffectiveArea, l.AssignedTo, l.Notes,
        l.Supplies.Select(s => new LaborSupplyView(s.SupplyName, s.PlannedDose, s.RealDose, s.PlannedTotal, s.RealTotal, s.UnitOfMeasure)).ToList());

    // Espejo de GestorOT.Domain.Enums.LaborPriority.
    private static string PriorityName(int p) => p switch
    {
        0 => "Baja",
        2 => "Alta",
        3 => "Urgente",
        _ => "Regular"
    };
}

public sealed record LaborSupplyView(string? Supply, decimal PlannedDose, decimal? RealDose, decimal PlannedTotal, decimal? RealTotal, string Unit);
