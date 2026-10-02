using System.ComponentModel;
using GestorOT.Mcp.Api;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace GestorOT.Mcp.Tools;

/// <summary>
/// Catálogos que hacen falta para crear labores y OTs (tipos, actividades, personas, insumos,
/// estados). Algunos tienen miles de filas: todos filtran y paginan del lado de la API.
/// </summary>
[McpServerToolType]
public sealed class CatalogTools
{
    private readonly GestorOtApiClient _api;

    public CatalogTools(GestorOtApiClient api)
    {
        _api = api;
    }

    [McpServerTool(Name = "list_labor_types", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Tipos de labor (siembra, pulverización, cosecha...), paginados por nombre. executionMode: Propia, Contratista o null (sirve para los dos).")]
    public async Task<PageResult<LaborTypeView>> ListLaborTypes(
        [Description("Filtra por nombre o descripción (contiene).")] string? search = null,
        [Description("Propia o Contratista. Vacío = todos.")] string? executionMode = null,
        [Description(Paging.SortDir)] string? sortDir = null,
        [Description(Paging.Page)] int page = 1,
        [Description(Paging.PageSize)] int pageSize = Paging.DefaultPageSize,
        CancellationToken ct = default) =>
        PageResult<LaborTypeView>.From(
            await _api.SearchLaborTypesAsync(new ApiQuery(page, pageSize, "name", sortDir)
                .Add("search", search)
                .Add("executionMode", ExecutionModeValue(executionMode)), ct),
            t => new LaborTypeView(t.Id, t.Name, t.Description, ExecutionModeName(t.ExecutionMode)));

    [McpServerTool(Name = "list_activities", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Actividades del ERP (cultivo/actividad contable), paginadas por nombre. create_labor exige una.")]
    public async Task<PageResult<ApiActivity>> ListActivities(
        [Description("Filtra por nombre (contiene).")] string? search = null,
        [Description("true para incluir las desactivadas.")] bool includeInactive = false,
        [Description(Paging.SortDir)] string? sortDir = null,
        [Description(Paging.Page)] int page = 1,
        [Description(Paging.PageSize)] int pageSize = Paging.DefaultPageSize,
        CancellationToken ct = default) =>
        PageResult<ApiActivity>.From(
            await _api.SearchActivitiesAsync(new ApiQuery(page, pageSize, "name", sortDir)
                .Add("search", search)
                .Add("includeInactive", includeInactive), ct),
            a => a);

    [McpServerTool(Name = "list_contacts", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Personas, paginadas: personal propio, contratistas, proveedores. Es quien ejecuta la labor (contactId). Hay miles: filtrar siempre que se pueda.")]
    public async Task<PageResult<ContactView>> ListContacts(
        [Description("Filtra por nombre, razón social o email (contiene).")] string? search = null,
        [Description("true = solo contratistas, proveedores y sin clasificar; false = solo personal propio y sin clasificar; vacío = todos.")] bool? contractors = null,
        [Description("Orden: name (default), legalName o role.")] string? sortBy = null,
        [Description(Paging.SortDir)] string? sortDir = null,
        [Description(Paging.Page)] int page = 1,
        [Description(Paging.PageSize)] int pageSize = Paging.DefaultPageSize,
        CancellationToken ct = default)
    {
        // Espejo de GestorOT.Domain.Enums.ContactRole. Sin clasificar (5) entra en los dos lados.
        int[] roles = contractors switch
        {
            true => new[] { 1, 4, 5 },
            false => new[] { 0, 2, 3, 5, 99 },
            null => Array.Empty<int>()
        };

        return PageResult<ContactView>.From(
            await _api.SearchContactsAsync(new ApiQuery(page, pageSize, sortBy, sortDir)
                .Add("search", search)
                .AddEach("roles", roles), ct),
            c => new ContactView(c.Id, c.FullName, c.LegalName, c.Position, RoleName(c.Role)));
    }

    [McpServerTool(Name = "list_supplies", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Insumos del inventario (semillas, fitosanitarios, fertilizantes) con stock y unidad, paginados. El id es el supplyId de create_labor.")]
    public async Task<PageResult<SupplyView>> ListSupplies(
        [Description("Filtra por nombre del insumo, grupo o subgrupo (contiene).")] string? search = null,
        [Description("Filtra por categoría (contiene).")] string? category = null,
        [Description("true = solo con stock > 0; false = solo sin stock.")] bool? inStock = null,
        [Description("Orden: name (default), category o stock (mayor primero).")] string? sortBy = null,
        [Description(Paging.SortDir)] string? sortDir = null,
        [Description(Paging.Page)] int page = 1,
        [Description(Paging.PageSize)] int pageSize = Paging.DefaultPageSize,
        CancellationToken ct = default) =>
        PageResult<SupplyView>.From(
            await _api.SearchInventoryAsync(new ApiQuery(page, pageSize, sortBy, sortDir)
                .Add("search", search)
                .Add("category", category)
                .Add("inStock", inStock), ct),
            i => new SupplyView(i.Id, i.ItemName, i.Category, i.CurrentStock, i.UnitA, i.GrupoConcepto, i.SubGrupoConcepto));

    [McpServerTool(Name = "list_work_order_statuses", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Estados de OT configurados en el tenant, paginados. isDefault marca el que se usa si no se indica uno.")]
    public async Task<PageResult<ApiWorkOrderStatus>> ListWorkOrderStatuses(
        [Description("Filtra por nombre (contiene).")] string? search = null,
        [Description("Orden: order (default, el del flujo) o name.")] string? sortBy = null,
        [Description(Paging.SortDir)] string? sortDir = null,
        [Description(Paging.Page)] int page = 1,
        [Description(Paging.PageSize)] int pageSize = Paging.DefaultPageSize,
        CancellationToken ct = default) =>
        PageResult<ApiWorkOrderStatus>.From(
            await _api.SearchWorkOrderStatusesAsync(new ApiQuery(page, pageSize, sortBy, sortDir).Add("search", search), ct),
            s => s);

    // Espejo de GestorOT.Domain.Enums.LaborExecutionMode.
    private static string? ExecutionModeName(int? mode) => mode switch
    {
        0 => "Propia",
        1 => "Contratista",
        _ => null
    };

    private static string? ExecutionModeValue(string? mode) => mode?.Trim().ToLowerInvariant() switch
    {
        null or "" => null,
        "propia" => "0",
        "contratista" => "1",
        _ => throw new McpException($"executionMode '{mode}' inválido. Usar Propia o Contratista.")
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

public sealed record LaborTypeView(Guid Id, string Name, string? Description, string? ExecutionMode);

public sealed record ContactView(Guid Id, string FullName, string? LegalName, string? Position, string Role);

public sealed record SupplyView(Guid Id, string Name, string Category, double CurrentStock, string Unit, string? Group, string? SubGroup);
