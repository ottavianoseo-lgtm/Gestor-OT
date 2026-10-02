using System.ComponentModel;
using GestorOT.Mcp.Api;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace GestorOT.Mcp.Tools;

[McpServerToolType]
public sealed class WorkOrderTools
{
    private readonly GestorOtApiClient _api;

    public WorkOrderTools(GestorOtApiClient api)
    {
        _api = api;
    }

    [McpServerTool(Name = "list_work_orders", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Órdenes de trabajo (OT) paginadas, de la más nueva a la más vieja. Para ver sus labores usar get_work_order.")]
    public async Task<WorkOrderPage> ListWorkOrders(
        [Description("Página, desde 1.")] int page = 1,
        [Description("Tamaño de página (1-100).")] int pageSize = 25,
        CancellationToken ct = default)
    {
        var result = await _api.GetWorkOrdersPagedAsync(Math.Max(1, page), Math.Clamp(pageSize, 1, 100), ct);
        return new WorkOrderPage(result.Total, result.Page, result.PageSize, result.Items.Select(WorkOrderView.From).ToList());
    }

    [McpServerTool(Name = "get_work_order", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Detalle de una orden de trabajo con sus labores e insumos.")]
    public async Task<WorkOrderDetailView> GetWorkOrder(
        [Description("Id de la OT (ver list_work_orders).")] Guid id,
        CancellationToken ct = default)
    {
        var wo = await _api.GetWorkOrderAsync(id, ct)
            ?? throw new McpException($"No existe la OT {id} (o no es de este tenant).");

        return new WorkOrderDetailView(WorkOrderView.From(wo), (wo.Labors ?? new()).Select(LaborView.From).ToList());
    }
}

public sealed record WorkOrderPage(int Total, int Page, int PageSize, List<WorkOrderView> Items);

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

public sealed record WorkOrderDetailView(WorkOrderView WorkOrder, List<LaborView> Labors);
