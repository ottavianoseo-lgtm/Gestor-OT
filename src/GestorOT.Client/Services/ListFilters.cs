using GestorOT.Shared.Dtos;

namespace GestorOT.Client.Services;

/// <summary>
/// Guarda los filtros de cada pantalla mientras dura la sesión: al volver a la página
/// se encuentran como se dejaron. Se pierde al recargar el navegador, a propósito.
/// </summary>
public sealed class FilterState
{
    private readonly Dictionary<string, object> _byPage = new();

    public T For<T>(string page) where T : new()
    {
        if (_byPage.TryGetValue(page, out var existing) && existing is T typed)
            return typed;
        var created = new T();
        _byPage[page] = created;
        return created;
    }
}

/// <summary>
/// Filtros de lista que se resuelven en el cliente sobre lo que ya trajo la API.
/// </summary>
public sealed class LaborFilter
{
    public string? Search { get; set; }
    public string? FieldName { get; set; }
    public Guid? LotId { get; set; }
    public DateTime?[]? DateRange { get; set; }
    public string SortBy { get; set; } = LaborSort.CreatedDesc;

    public bool IsEmpty =>
        string.IsNullOrWhiteSpace(Search) && FieldName == null && LotId == null
        && DateRange?.Any(d => d.HasValue) != true;

    public void Clear()
    {
        Search = null;
        FieldName = null;
        LotId = null;
        DateRange = null;
    }

    public List<LaborDto> Apply(IEnumerable<LaborDto> labors)
    {
        var result = labors;

        if (!string.IsNullOrWhiteSpace(Search))
        {
            var term = Search.Trim();
            result = result.Where(l =>
                Contains(l.LotName, term) || Contains(l.FieldName, term) || Contains(l.LaborTypeName, term)
                || Contains(l.AssignedTo, term) || Contains(l.Notes, term) || Contains(l.OTNumber, term)
                || Contains(l.ErpActivityName, term));
        }

        if (FieldName != null)
            result = result.Where(l => string.Equals(l.FieldName, FieldName, StringComparison.OrdinalIgnoreCase));

        if (LotId != null)
            result = result.Where(l => l.LotId == LotId);

        // La fecha de la labor se guarda como medianoche local en UTC (03:00Z) y la tabla la muestra
        // tal cual con dd/MM/yyyy, así que se compara por .Date igual que lo que ve el usuario.
        var from = DateRange?.ElementAtOrDefault(0)?.Date;
        var to = DateRange?.ElementAtOrDefault(1)?.Date;
        if (from != null) result = result.Where(l => l.DisplayDate.Date >= from);
        if (to != null) result = result.Where(l => l.DisplayDate.Date <= to);

        result = SortBy switch
        {
            LaborSort.CreatedAsc => result.OrderBy(l => l.CreatedAt),
            LaborSort.DateDesc => result.OrderByDescending(l => l.DisplayDate).ThenByDescending(l => l.CreatedAt),
            LaborSort.DateAsc => result.OrderBy(l => l.DisplayDate).ThenBy(l => l.CreatedAt),
            LaborSort.Priority => result.OrderByDescending(l => l.Priority).ThenBy(l => l.DisplayDate),
            LaborSort.Lot => result.OrderBy(l => l.FieldName).ThenBy(l => l.LotName).ThenBy(l => l.DisplayDate),
            _ => result.OrderByDescending(l => l.CreatedAt)
        };

        return result.ToList();
    }

    internal static bool Contains(string? value, string term) =>
        value != null && value.Contains(term, StringComparison.OrdinalIgnoreCase);
}

public static class LaborSort
{
    public const string CreatedDesc = "created-desc";
    public const string CreatedAsc = "created-asc";
    public const string DateDesc = "date-desc";
    public const string DateAsc = "date-asc";
    public const string Priority = "priority";
    public const string Lot = "lot";
}

/// <summary>Búsqueda por texto + campo, para las pantallas de lotes y campos.</summary>
public sealed class LotFilter
{
    public string? Search { get; set; }
    public Guid? FieldId { get; set; }

    public bool Matches(string? lotName, string? fieldName, Guid? fieldId)
    {
        if (FieldId != null && fieldId != FieldId)
            return false;
        if (string.IsNullOrWhiteSpace(Search))
            return true;
        var term = Search.Trim();
        return LaborFilter.Contains(lotName, term) || LaborFilter.Contains(fieldName, term);
    }
}
