namespace GestorOT.Client.Components.Tables;

/// <summary>
/// Estado de una <see cref="RemoteTable{TItem}"/>: página, orden, búsqueda y filtros. Se traduce
/// a la query string de los GET .../search de la API. Con StateKey se guarda en FilterState, así
/// que al volver a la pantalla los filtros siguen como se dejaron.
/// </summary>
public sealed class TableQuery
{
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? SortBy { get; set; }
    public string? SortDir { get; set; }
    public string? Search { get; set; }

    /// <summary>La tabla ya aplicó sus valores iniciales (tamaño de página) a este estado.</summary>
    public bool Seeded { get; set; }

    /// <summary>Parámetro de la API → valor. Vacío o null = sin filtro.</summary>
    public Dictionary<string, string?> Filters { get; } = new();

    /// <summary>
    /// Lo que un filtro necesita para volver a dibujarse igual (ej. el rango de fechas tal como
    /// lo eligió el usuario, o la etiqueta de la opción elegida). No viaja a la API.
    /// </summary>
    public Dictionary<string, object?> UiState { get; } = new();

    public string? Get(string param) => Filters.GetValueOrDefault(param);

    public void Set(string param, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) Filters.Remove(param);
        else Filters[param] = value;
    }

    public bool HasFilters => !string.IsNullOrWhiteSpace(Search) || Filters.Count > 0;

    public void ClearFilters()
    {
        Search = null;
        Filters.Clear();
        UiState.Clear();
        PageIndex = 1;
    }

    /// <summary>
    /// <paramref name="fixedFilters"/> son los que pone la pantalla y el usuario no ve (ej. la
    /// campaña actual); pisan a los del usuario si coinciden.
    /// </summary>
    public string ToQueryString(IReadOnlyDictionary<string, string?>? fixedFilters = null)
    {
        var parts = new List<string> { $"page={PageIndex}", $"pageSize={PageSize}" };
        Add(parts, "sortBy", SortBy);
        Add(parts, "sortDir", SortDir);
        Add(parts, "search", Search);

        var merged = new Dictionary<string, string?>(Filters);
        foreach (var (k, v) in fixedFilters ?? new Dictionary<string, string?>())
            merged[k] = v;
        foreach (var (k, v) in merged)
            Add(parts, k, v);

        return string.Join('&', parts);
    }

    private static void Add(List<string> parts, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            parts.Add($"{name}={Uri.EscapeDataString(value.Trim())}");
    }
}

/// <summary>Lo que ven los filtros de la barra que los contiene (<see cref="FilterToolbar"/>).</summary>
public interface ITableFilterHost
{
    TableQuery Query { get; }

    /// <summary>Un filtro cambió: vuelve a la página 1 y avisa a quien muestra los datos.</summary>
    Task FiltersChangedAsync();

    /// <summary>Se dispara cuando la tabla cambia filtros desde afuera (ej. "Limpiar filtros").</summary>
    event Action? QueryReset;
}

/// <summary>Opción de un <see cref="SelectFilter"/>. Value es lo que se manda a la API.</summary>
public sealed record FilterOption(string Value, string Label);
