using System.ComponentModel;
using System.Text.Json.Nodes;
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
            var statuses = (await _api.SearchWorkOrderStatusesAsync(new ApiQuery(1, ApiQuery.MaxPageSize, "order"), ct)).Items;
            status = (statuses.FirstOrDefault(s => s.IsDefault) ?? statuses.FirstOrDefault())?.Name
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
    public async Task<LaborSaveResult> CreateLabor(
        [Description("Id de campaña. Obligatorio.")] Guid campaignId,
        [Description("Id del lote (ver list_lots con campaignId). Tiene que estar en la campaña.")] Guid lotId,
        [Description("Id del tipo de labor (ver list_labor_types). Obligatorio. Ojo con los nombres repetidos: elegir por executionMode (Propia/Contratista) según quién la ejecuta.")] Guid laborTypeId,
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
        var campaignLot = (await _api.SearchCampaignLotsAsync(campaignId, new ApiQuery(1, 1).Add("lotId", lotId), ct)).Items.FirstOrDefault()
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
                var item = await _api.GetInventoryItemAsync(s.SupplyId, ct)
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

        return new LaborSaveResult(LaborView.From(response.Labor), response.Warnings);
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

    [McpServerTool(Name = "update_labor", Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Edita una labor existente: solo cambia lo que se pasa. Con realized=true la marca como realizada (el caso típico: 'ya se hizo la pulverización del lote X'), igual que el botón Ejecutar de la UI: fecha de ejecución y dosis reales, que por defecto son las planificadas. Las del Planeamiento Original (isOriginalPlan) no se pueden realizar.")]
    public async Task<LaborSaveResult> UpdateLabor(
        [Description("Id de la labor (ver search_labors o get_labor).")] Guid laborId,
        [Description("true = marcarla realizada; false = volverla a planificada. Vacío = no cambia.")] bool? realized = null,
        [Description("Fecha (yyyy-MM-dd): la de ejecución si la labor queda realizada, la estimada si queda planificada. Al realizar sin fecha, hoy.")] DateOnly? date = null,
        [Description("Hectáreas. Si queda realizada, son las hectáreas reales de los insumos.")] decimal? hectares = null,
        [Description("Persona que la ejecuta (ver list_contacts).")] Guid? contactId = null,
        [Description("Tipo de labor (ver list_labor_types).")] Guid? laborTypeId = null,
        [Description("Actividad del ERP (ver list_activities).")] Guid? activityId = null,
        [Description("Baja, Regular, Alta o Urgente.")] string? priority = null,
        [Description("Notas. Reemplaza las anteriores.")] string? notes = null,
        [Description("Dosis por hectárea a cambiar, por supplyId (ver get_labor). Si la labor queda realizada se toman como dosis reales; si no, planificadas. Un supplyId que la labor no tiene se agrega. Los insumos no nombrados no se tocan.")] List<LaborSupplyInput>? supplies = null,
        CancellationToken ct = default)
    {
        var current = await _api.GetLaborAsync(laborId, ct)
            ?? throw new McpException($"No existe la labor {laborId} (o no es de este tenant).");

        var willBeRealized = realized ?? current.Status == "Realized";
        if (willBeRealized && current.IsOriginalPlan)
            throw new McpException("La labor es del Planeamiento Original: no se puede realizar. Cargar la realizada con create_labor (realized=true).");

        // Los insumos nuevos necesitan la unidad del inventario; se resuelve antes de tocar nada.
        var newSupplyUnits = new Dictionary<Guid, string>();
        foreach (var s in supplies ?? new())
        {
            if (current.Supplies.Any(x => x.SupplyId == s.SupplyId)) continue;
            newSupplyUnits[s.SupplyId] = !string.IsNullOrWhiteSpace(s.Unit)
                ? s.Unit
                : (await _api.GetInventoryItemAsync(s.SupplyId, ct)
                    ?? throw new McpException($"El insumo {s.SupplyId} no existe. Usar list_supplies.")).UnitA;
        }

        var response = await _api.PatchLaborAsync(laborId, labor =>
        {
            if (realized == true)
            {
                labor["status"] = "Realized";
                labor["mode"] = "Realized";
                labor["executionDate"] = Iso(_options.ToUtc(date ?? _options.Today()));
            }
            else if (realized == false)
            {
                labor["status"] = "Planned";
                labor["mode"] = "Planned";
                labor["executionDate"] = null;
                if (date.HasValue) labor["estimatedDate"] = Iso(_options.ToUtc(date.Value));
            }
            else if (date.HasValue)
            {
                labor[willBeRealized ? "executionDate" : "estimatedDate"] = Iso(_options.ToUtc(date.Value));
            }

            // Sin fecha estimada la API rechaza volverla a planificada.
            if (!willBeRealized && labor["estimatedDate"] is null)
                labor["estimatedDate"] = labor["executionDate"]?.DeepClone() ?? Iso(_options.ToUtc(_options.Today()));

            if (hectares.HasValue) labor["hectares"] = hectares.Value;
            if (contactId.HasValue) labor["contactId"] = contactId.Value.ToString();
            if (laborTypeId.HasValue) labor["laborTypeId"] = laborTypeId.Value.ToString();
            if (activityId.HasValue) labor["erpActivityId"] = activityId.Value.ToString();
            if (priority is not null) labor["priority"] = PriorityValue(priority);
            if (notes is not null) labor["notes"] = notes;

            var laborHa = labor["hectares"]!.GetValue<decimal>();
            var supplyNodes = labor["supplies"] as JsonArray ?? new JsonArray();
            labor["supplies"] = supplyNodes;

            foreach (var node in supplyNodes.OfType<JsonObject>())
            {
                var edit = supplies?.FirstOrDefault(s => s.SupplyId.ToString() == node["supplyId"]?.GetValue<string>());
                if (edit is not null)
                    node[willBeRealized ? "realDose" : "plannedDose"] = edit.Dose;

                if (hectares.HasValue)
                    node[willBeRealized ? "realHectares" : "plannedHectares"] = laborHa;

                // Lo mismo que hace la UI al pasar a Realizada: lo real arranca igual a lo planificado.
                if (willBeRealized)
                {
                    node["realDose"] ??= node["plannedDose"]?.DeepClone();
                    node["realHectares"] ??= node["plannedHectares"]?.DeepClone() ?? laborHa;
                }
            }

            foreach (var (supplyId, unit) in newSupplyUnits)
            {
                var dose = supplies!.First(s => s.SupplyId == supplyId).Dose;
                supplyNodes.Add(new JsonObject
                {
                    ["id"] = Guid.Empty.ToString(),
                    ["supplyId"] = supplyId.ToString(),
                    ["plannedDose"] = dose,
                    ["plannedHectares"] = laborHa,
                    ["realDose"] = willBeRealized ? dose : null,
                    ["realHectares"] = willBeRealized ? laborHa : null,
                    ["unitOfMeasure"] = unit
                });
            }
        }, ct);

        // La respuesta del PUT no trae tipo ni actividad por nombre; se relee para devolverla completa.
        var saved = await _api.GetLaborAsync(laborId, ct) ?? response.Labor;
        return new LaborSaveResult(LaborView.From(saved), response.Warnings);
    }

    private static string Iso(DateTime utc) => utc.ToString("O");

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

public sealed record LaborSaveResult(LaborView Labor, List<string> Warnings);

public sealed record AssignResult(Guid LaborId, bool Ok, string? Error);
