using GestorOT.Mcp.Api;

namespace GestorOT.Mcp.Tools;

/// <summary>
/// Contrato común de todas las tools de consulta. Filtro, orden y corte los hace la API en SQL
/// (GET .../search); acá solo se reenvían los parámetros y se mapea la página.
/// </summary>
public sealed record PageResult<T>(int Total, int Page, int PageSize, int TotalPages, bool HasMore, List<T> Items)
{
    public static PageResult<T> From<TApi>(ApiPaged<TApi> paged, Func<TApi, T> map)
    {
        var totalPages = Math.Max(1, (int)Math.Ceiling(paged.Total / (double)Math.Max(1, paged.PageSize)));
        return new PageResult<T>(paged.Total, paged.Page, paged.PageSize, totalPages, paged.Page < totalPages, paged.Items.Select(map).ToList());
    }
}

internal static class Paging
{
    public const int DefaultPageSize = 25;

    // Textos de [Description] compartidos por todas las tools de consulta.
    public const string Page = "Página, desde 1. Si la respuesta trae hasMore=true, pedir la siguiente o afinar filtros.";
    public const string PageSize = "Resultados por página (1-100, default 25). Preferir páginas chicas y filtros.";
    public const string SortDir = "'asc' o 'desc'. Vacío = la dirección natural de ese orden.";
}
