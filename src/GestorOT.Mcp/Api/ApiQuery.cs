namespace GestorOT.Mcp.Api;

/// <summary>
/// Query string para los GET .../search. Siempre lleva página y tamaño (la API los exige);
/// los filtros vacíos no se mandan.
/// </summary>
public sealed class ApiQuery
{
    public const int MaxPageSize = 100;

    private readonly List<string> _parts = new();

    public ApiQuery(int page, int pageSize, string? sortBy = null, string? sortDir = null)
    {
        // Se corrige acá en vez de dejar que la API devuelva 400: el modelo no gana nada con ese error.
        Add("page", Math.Max(1, page).ToString());
        Add("pageSize", Math.Clamp(pageSize, 1, MaxPageSize).ToString());
        Add("sortBy", sortBy);
        Add("sortDir", sortDir);
    }

    public ApiQuery Add(string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            _parts.Add($"{name}={Uri.EscapeDataString(value.Trim())}");
        return this;
    }

    public ApiQuery Add(string name, Guid? value) => Add(name, value?.ToString());

    public ApiQuery Add(string name, bool? value) => Add(name, value?.ToString().ToLowerInvariant());

    public ApiQuery Add(string name, DateTime? utc) => Add(name, utc?.ToString("O"));

    public ApiQuery AddEach(string name, IEnumerable<int> values)
    {
        foreach (var v in values) Add(name, v.ToString());
        return this;
    }

    public string For(string path) => path + "?" + string.Join('&', _parts);
}
