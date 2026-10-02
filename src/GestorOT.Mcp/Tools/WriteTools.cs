using System.ComponentModel;
using GestorOT.Mcp.Api;
using Microsoft.Extensions.Options;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace GestorOT.Mcp.Tools;

/// <summary>
/// Tools que escriben. Todas las reglas (campaña bloqueada, OT bloqueada, persona de la OT,
/// rotación, etc.) las valida la API; si algo no cierra, el motivo vuelve como error de la tool.
/// </summary>
[McpServerToolType]
public sealed class WriteTools
{
    private readonly GestorOtApiClient _api;
    private readonly GestorOtOptions _options;

    public WriteTools(GestorOtApiClient api, IOptions<GestorOtOptions> options)
    {
        _api = api;
        _options = options.Value;
    }

    [McpServerTool(Name = "create_work_order", Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("Crea una orden de trabajo (OT) vacía en una campaña. Después se le cargan labores con create_labor (workOrderId) o assign_labors_to_work_order.")]
    public async Task<WorkOrderView> CreateWorkOrder(
        [Description("Id de campaña (ver get_active_campaigns). Obligatorio.")] Guid campaignId,
        [Description("Fecha de vencimiento/entrega, formato yyyy-MM-dd. Obligatoria.")] DateOnly dueDate,
        [Description("Nombre corto de la OT.")] string? name = null,
        [Description("Descripción o instrucciones.")] string? description = null,
        [Description("Fecha planificada de inicio (yyyy-MM-dd). Por defecto, dueDate.")] DateOnly? plannedDate = null,
        [Description("Responsable en texto libre.")] string? assignedTo = null,
        [Description("Persona que ejecuta (ver list_contacts). Las labores de la OT tienen que ser de esta persona.")] Guid? contactId = null,
        [Description("Nombre del estado (ver list_work_order_statuses). Vacío = el estado por defecto.")] string? status = null,
        [Description("Número de OT. Vacío = se genera uno como hace la UI (OT_XXXXX).")] string? otNumber = null,
        CancellationToken ct = default)
    {
        // El DTO de la API exige Status; sin uno pedido se manda el que el controller elegiría igual.
        if (string.IsNullOrWhiteSpace(status))
        {
            var statuses = await _api.GetWorkOrderStatusesAsync(ct);
            status = (statuses.FirstOrDefault(s => s.IsDefault) ?? statuses.OrderBy(s => s.SortOrder).FirstOrDefault())?.Name
                ?? throw new McpException("El tenant no tiene estados de OT configurados.");
        }

        var created = await _api.CreateWorkOrderAsync(new
        {
            campaignId,
            // Mismo formato que OrdenesTrabajos.razor; la API no lo genera.
            otNumber = string.IsNullOrWhiteSpace(otNumber) ? $"OT_{Guid.NewGuid().ToString()[..5].ToUpperInvariant()}" : otNumber,
            name,
            description = description ?? name,
            status,
            assignedTo = assignedTo ?? "",
            contactId,
            dueDate = _options.ToUtc(dueDate),
            plannedDate = _options.ToUtc(plannedDate ?? dueDate),
            expirationDate = _options.ToUtc(dueDate)
        }, ct);

        return WorkOrderView.From(created);
    }

    [McpServerTool(Name = "create_labor", Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("Crea una labor sobre un lote de una campaña, planificada o ya realizada, opcionalmente dentro de una OT y con insumos. Devuelve la labor creada y las advertencias de la API (ej. rotación).")]
    public async Task<CreateLaborResult> CreateLabor(
        [Description("Id de campaña. Obligatorio.")] Guid campaignId,
        [Description("Id del lote (ver list_lots con campaignId). Tiene que estar en la campaña.")] Guid lotId,
        [Description("Id del tipo de labor (ver list_labor_types). Obligatorio.")] Guid laborTypeId,
        [Description("Id de la actividad del ERP (ver list_activities). Obligatorio.")] Guid activityId,
        [Description("Fecha estimada o, si realized=true, fecha de ejecución (yyyy-MM-dd). Obligatoria.")] DateOnly date,
        [Description("Hectáreas. Por defecto, la superficie productiva del lote en la campaña.")] decimal? hectares = null,
        [Description("true = se carga como ya ejecutada (Realized); false = planificada.")] bool realized = false,
        [Description("OT donde cargarla (ver list_work_orders).")] Guid? workOrderId = null,
        [Description("Persona que la ejecuta (ver list_contacts).")] Guid? contactId = null,
        [Description("Baja, Regular, Alta o Urgente.")] string priority = "Regular",
        [Description("Notas.")] string? notes = null,
        [Description("Insumos con dosis por hectárea.")] List<LaborSupplyInput>? supplies = null,
        CancellationToken ct = default)
    {
        var campaignLot = (await _api.GetCampaignLotsAsync(campaignId, ct)).FirstOrDefault(l => l.LotId == lotId)
            ?? throw new McpException($"El lote {lotId} no está en la campaña {campaignId}. Usar list_lots con campaignId.");

        var ha = hectares ?? (campaignLot.ProductiveArea > 0 ? campaignLot.ProductiveArea : campaignLot.CadastralArea);
        var utcDate = _options.ToUtc(date);
        var mode = realized ? "Realized" : "Planned";

        var supplyBodies = new List<object>();
        foreach (var s in supplies ?? new())
        {
            var unit = s.Unit;
            if (string.IsNullOrWhiteSpace(unit))
            {
                // La unidad la elige la UI desde el inventario; si el modelo no la pasa, se hace lo mismo.
                var item = (await _api.GetInventoryAsync(null, ct)).FirstOrDefault(i => i.Id == s.SupplyId)
                    ?? throw new McpException($"El insumo {s.SupplyId} no existe. Usar list_supplies.");
                unit = item.UnitA;
            }

            supplyBodies.Add(new
            {
                supplyId = s.SupplyId,
                plannedDose = s.Dose,
                realDose = realized ? s.Dose : (decimal?)null,
                unitOfMeasure = unit
            });
        }

        var response = await _api.CreateLaborAsync(new
        {
            campaignId,
            campaignLotId = campaignLot.Id,
            lotId,
            laborTypeId,
            erpActivityId = activityId,
            workOrderId,
            contactId,
            status = mode,
            mode,
            estimatedDate = utcDate,
            executionDate = realized ? utcDate : (DateTime?)null,
            hectares = ha,
            rateUnit = "ha",
            priority = PriorityValue(priority),
            notes,
            supplies = supplyBodies
        }, ct);

        return new CreateLaborResult(LaborView.From(response.Labor), response.Warnings);
    }

    [McpServerTool(Name = "assign_labors_to_work_order", Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Mete labores existentes en una OT (o las saca, con workOrderId vacío). Procesa una por una y devuelve qué pasó con cada labor.")]
    public async Task<List<AssignResult>> AssignLaborsToWorkOrder(
        [Description("Ids de las labores (ver search_labors con assigned=false).")] List<Guid> laborIds,
        [Description("OT destino. Vacío = sacar las labores de su OT.")] Guid? workOrderId = null,
        CancellationToken ct = default)
    {
        var results = new List<AssignResult>();
        foreach (var id in laborIds.Distinct())
        {
            try
            {
                await _api.PatchLaborAsync(id, labor => labor["workOrderId"] = workOrderId?.ToString(), ct);
                results.Add(new AssignResult(id, true, null));
            }
            catch (McpException ex)
            {
                results.Add(new AssignResult(id, false, ex.Message));
            }
        }
        return results;
    }

    // Espejo de GestorOT.Domain.Enums.LaborPriority.
    private static int PriorityValue(string priority) => priority.Trim().ToLowerInvariant() switch
    {
        "baja" => 0,
        "regular" => 1,
        "alta" => 2,
        "urgente" => 3,
        _ => throw new McpException($"Prioridad '{priority}' inválida. Usar Baja, Regular, Alta o Urgente.")
    };
}

public sealed record LaborSupplyInput(
    [property: Description("Id del insumo (ver list_supplies).")] Guid SupplyId,
    [property: Description("Dosis por hectárea.")] decimal Dose,
    [property: Description("Unidad de la dosis (ej. 'l', 'kg'). Vacío = la unidad del inventario.")] string? Unit = null);

public sealed record CreateLaborResult(LaborView Labor, List<string> Warnings);

public sealed record AssignResult(Guid LaborId, bool Ok, string? Error);
