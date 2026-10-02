using System.ComponentModel;
using GestorOT.Mcp.Api;
using Microsoft.Extensions.Options;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace GestorOT.Mcp.Tools;

[McpServerToolType]
public sealed class WorkOrderTools
{
    private readonly GestorOtApiClient _api;
    private readonly GestorOtOptions _options;

    public WorkOrderTools(GestorOtApiClient api, IOptions<GestorOtOptions> options)
    {
        _api = api;
        _options = options.Value;
    }

    [McpServerTool(Name = "list_work_orders", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Órdenes de trabajo (OT) paginadas, sin sus labores. Para ver las labores usar get_work_order o search_labors con workOrderId.")]
    public async Task<PageResult<WorkOrderView>> ListWorkOrders(
        [Description("Id de campaña (ver get_active_campaigns).")] Guid? campaignId = null,
        [Description("Nombre del estado (ver list_work_order_statuses).")] string? status = null,
        [Description("Id de campo (ver list_fields).")] Guid? fieldId = null,
        [Description("Id de la persona responsable (ver list_contacts).")] Guid? contactId = null,
        [Description("true = solo las bloqueadas por su estado; false = solo las editables.")] bool? locked = null,
        [Description("Vencimiento desde esta fecha inclusive (yyyy-MM-dd).")] DateOnly? dueFrom = null,
        [Description("Vencimiento hasta esta fecha inclusive (yyyy-MM-dd).")] DateOnly? dueTo = null,
        [Description("Texto en número de OT, nombre, descripción o responsable (contiene).")] string? search = null,
        [Description("Orden: dueDate (default, más nueva primero), plannedDate, otNumber, name o status (orden del flujo).")] string? sortBy = null,
        [Description(Paging.SortDir)] string? sortDir = null,
        [Description(Paging.Page)] int page = 1,
        [Description(Paging.PageSize)] int pageSize = Paging.DefaultPageSize,
        CancellationToken ct = default)
    {
        var query = new ApiQuery(page, pageSize, sortBy, sortDir)
            .Add("campaignId", campaignId)
            .Add("status", status)
            .Add("fieldId", fieldId)
            .Add("contactId", contactId)
            .Add("locked", locked)
            .Add("dueFrom", dueFrom.HasValue ? _options.ToUtc(dueFrom.Value) : null)
            .Add("dueBefore", dueTo.HasValue ? _options.ToUtc(dueTo.Value.AddDays(1)) : null)
            .Add("search", search);

        return PageResult<WorkOrderView>.From(await _api.SearchWorkOrdersAsync(query, ct), WorkOrderView.From);
    }

    [McpServerTool(Name = "get_work_order", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Detalle de una orden de trabajo con una página de sus labores e insumos. Si labors.hasMore=true, pedir laborPage siguiente.")]
    public async Task<WorkOrderDetailView> GetWorkOrder(
        [Description("Id de la OT (ver list_work_orders).")] Guid id,
        [Description("Página de labores, desde 1.")] int laborPage = 1,
        [Description("Labores por página (1-100, default 25).")] int laborPageSize = Paging.DefaultPageSize,
        CancellationToken ct = default)
    {
        var wo = await _api.GetWorkOrderAsync(id, ct)
            ?? throw new McpException($"No existe la OT {id} (o no es de este tenant).");

        var labors = await _api.SearchLaborsAsync(
            new ApiQuery(laborPage, laborPageSize, "date").Add("workOrderId", id), ct);

        return new WorkOrderDetailView(WorkOrderView.From(wo), PageResult<LaborView>.From(labors, LaborView.From));
    }
}

public sealed record WorkOrderView(
    Guid Id, string? OtNumber, string? Name, string? Description, string Status, string? AssignedTo,
    string? Field, Guid? CampaignId, DateTime DueDate, DateTime? PlannedDate, DateTime? ExpirationDate,
    bool StockReserved, bool IsLocked)
{
    public static WorkOrderView From(ApiWorkOrder w) => new(
        w.Id, w.OTNumber, w.Name, w.Description, w.Status, w.AssignedTo,
        w.FieldName, w.CampaignId, w.DueDate, w.PlannedDate, w.ExpirationDate,
        w.StockReserved, w.IsLocked);
}

public sealed record WorkOrderDetailView(WorkOrderView WorkOrder, PageResult<LaborView> Labors);
