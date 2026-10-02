using System.ComponentModel;
using GestorOT.Mcp.Api;
using ModelContextProtocol.Server;

namespace GestorOT.Mcp.Tools;

/// <summary>
/// Catálogos que hacen falta para crear labores y OTs (tipos, actividades, personas, insumos,
/// estados). Algunos tienen miles de filas, así que todos filtran por texto y cortan en 'limit'.
/// </summary>
[McpServerToolType]
public sealed class CatalogTools
{
    private const int MaxLimit = 200;
    private readonly GestorOtApiClient _api;

    public CatalogTools(GestorOtApiClient api)
    {
        _api = api;
    }

    [McpServerTool(Name = "list_labor_types", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Tipos de labor (siembra, pulverización, cosecha...). executionMode: Propia, Contratista o null (sirve para los dos).")]
    public async Task<CatalogResult<LaborTypeView>> ListLaborTypes(
        [Description("Filtra por nombre (contiene).")] string? search = null,
        int limit = 50,
        CancellationToken ct = default)
    {
        var items = (await _api.GetLaborTypesAsync(ct))
            .Where(t => Matches(t.Name, search) || Matches(t.Description ?? "", search))
            .Select(t => new LaborTypeView(t.Id, t.Name, t.Description, ExecutionModeName(t.ExecutionMode)));
        return Page(items, limit);
    }

    [McpServerTool(Name = "list_activities", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Actividades del ERP (cultivo/actividad contable). create_labor exige una.")]
    public async Task<CatalogResult<ApiActivity>> ListActivities(
        [Description("Filtra por nombre (contiene).")] string? search = null,
        int limit = 50,
        CancellationToken ct = default) =>
        Page((await _api.GetActivitiesAsync(ct)).Where(a => Matches(a.Name, search)), limit);

    [McpServerTool(Name = "list_contacts", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Personas: personal propio, contratistas, proveedores. Es quien ejecuta la labor (contactId).")]
    public async Task<CatalogResult<ContactView>> ListContacts(
        [Description("Filtra por nombre o razón social (contiene).")] string? search = null,
        [Description("true = solo contratistas y proveedores; false = solo personal propio; vacío = todos.")] bool? contractors = null,
        int limit = 50,
        CancellationToken ct = default)
    {
        var items = (await _api.GetContactsAsync(ct))
            .Where(c => Matches(c.FullName, search) || Matches(c.LegalName ?? "", search))
            .Select(c => new ContactView(c.Id, c.FullName, c.LegalName, c.Position, RoleName(c.Role)))
            .Where(c => contractors switch
            {
                true => c.Role is "Contratista" or "Proveedor" or "Sin clasificar",
                false => c.Role is not ("Contratista" or "Proveedor"),
                null => true
            })
            .OrderBy(c => c.FullName);
        return Page(items, limit);
    }

    [McpServerTool(Name = "list_supplies", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Insumos del inventario (semillas, fitosanitarios, fertilizantes) con stock y unidad. El id es el supplyId de create_labor.")]
    public async Task<CatalogResult<SupplyView>> ListSupplies(
        [Description("Filtra por nombre del insumo.")] string? search = null,
        int limit = 50,
        CancellationToken ct = default)
    {
        var items = (await _api.GetInventoryAsync(search, ct))
            .Select(i => new SupplyView(i.Id, i.ItemName, i.Category, i.CurrentStock, i.UnitA, i.GrupoConcepto, i.SubGrupoConcepto));
        return Page(items, limit);
    }

    [McpServerTool(Name = "list_work_order_statuses", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Estados de OT configurados en el tenant, en orden. isDefault marca el que se usa si no se indica uno.")]
    public async Task<List<ApiWorkOrderStatus>> ListWorkOrderStatuses(CancellationToken ct) =>
        (await _api.GetWorkOrderStatusesAsync(ct)).OrderBy(s => s.SortOrder).ToList();

    private static CatalogResult<T> Page<T>(IEnumerable<T> items, int limit)
    {
        var all = items.ToList();
        return new CatalogResult<T>(all.Count, all.Take(Math.Clamp(limit, 1, MaxLimit)).ToList());
    }

    private static bool Matches(string value, string? filter) =>
        string.IsNullOrWhiteSpace(filter) || value.Contains(filter.Trim(), StringComparison.OrdinalIgnoreCase);

    // Espejo de GestorOT.Domain.Enums.LaborExecutionMode.
    private static string? ExecutionModeName(int? mode) => mode switch
    {
        0 => "Propia",
        1 => "Contratista",
        _ => null
    };

    // Espejo de GestorOT.Domain.Enums.ContactRole.
    private static string RoleName(int role) => role switch
    {
        0 => "Staff Interno",
        1 => "Contratista",
        2 => "Agrónomo",
        3 => "Administrador",
        4 => "Proveedor",
        5 => "Sin clasificar",
        99 => "Super Administrador",
        _ => role.ToString()
    };
}

public sealed record CatalogResult<T>(int Total, List<T> Items);

public sealed record LaborTypeView(Guid Id, string Name, string? Description, string? ExecutionMode);

public sealed record ContactView(Guid Id, string FullName, string? LegalName, string? Position, string Role);

public sealed record SupplyView(Guid Id, string Name, string Category, double CurrentStock, string Unit, string? Group, string? SubGroup);
