using System.ComponentModel;
using GestorOT.Mcp.Api;
using ModelContextProtocol.Server;

namespace GestorOT.Mcp.Tools;

[McpServerToolType]
public sealed class LaborTools
{
    private const int MaxLimit = 200;
    private readonly GestorOtApiClient _api;

    public LaborTools(GestorOtApiClient api)
    {
        _api = api;
    }

    [McpServerTool(Name = "search_labors", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Busca labores (tareas agronómicas sobre un lote). Devuelve como mucho 'limit' resultados y el total encontrado; si total > devueltas, afinar filtros.")]
    public async Task<LaborSearchResult> SearchLabors(
        [Description("Id de campaña. Casi siempre conviene pasarlo (ver get_active_campaigns).")] Guid? campaignId = null,
        [Description("Estado: Planned, AwaitingValidation, Validated, Realized o Pending.")] string? status = null,
        [Description("true = solo las que ya están en una OT; false = solo las sin asignar.")] bool? assigned = null,
        [Description("Id de tipo de labor.")] Guid? laborTypeId = null,
        [Description("Id de lote: devuelve el historial de labores de ese lote e ignora los otros filtros.")] Guid? lotId = null,
        [Description("Orden: 'date' (fecha estimada), 'priority' o vacío (más nuevas primero).")] string? sortBy = null,
        [Description("Máximo de labores a devolver (1-200).")] int limit = 50,
        CancellationToken ct = default)
    {
        var labors = lotId.HasValue
            ? await _api.GetLaborsByLotAsync(lotId.Value, ct)
            : await _api.GetLaborsAsync(campaignId, status, assigned, laborTypeId, sortBy, ct);

        var take = Math.Clamp(limit, 1, MaxLimit);
        return new LaborSearchResult(labors.Count, labors.Take(take).Select(LaborView.From).ToList());
    }
}

public sealed record LaborSearchResult(int Total, List<LaborView> Labors);

public sealed record LaborView(
    Guid Id, string Status, string Mode, string Priority, string? LaborType,
    Guid LotId, string? Lot, string? Field, Guid? CampaignId,
    Guid? WorkOrderId, string? OtNumber,
    DateTime? EstimatedDate, DateTime? ExecutionDate,
    decimal Hectares, decimal EffectiveArea, string? AssignedTo, string? Notes,
    List<LaborSupplyView> Supplies)
{
    public static LaborView From(ApiLabor l) => new(
        l.Id, l.Status, l.Mode, PriorityName(l.Priority), l.LaborTypeName,
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
