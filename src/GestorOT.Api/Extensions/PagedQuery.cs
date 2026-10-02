using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using GestorOT.Shared.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestorOT.Api.Extensions;

/// <summary>
/// Parámetros obligatorios de los endpoints <c>GET .../search</c>. Existen para consumidores
/// automáticos (el MCP): nunca devuelven la tabla entera, y filtro, orden y corte van en SQL.
/// Sin page/pageSize, o con pageSize fuera de rango, [ApiController] responde 400 solo.
/// </summary>
public sealed class PagedQuery
{
    public const int MaxPageSize = 100;

    [Required, Range(1, int.MaxValue)]
    public int? Page { get; init; }

    [Required, Range(1, MaxPageSize)]
    public int? PageSize { get; init; }

    /// <summary>Una de las claves que declara el endpoint. Vacío = la primera.</summary>
    public string? SortBy { get; init; }

    /// <summary>asc o desc. Vacío = la dirección por defecto de esa clave.</summary>
    public string? SortDir { get; init; }
}

/// <summary>
/// Claves de orden que acepta un endpoint de búsqueda. La primera es la de por defecto, y
/// siempre se desempata por <c>tieBreaker</c> (el Id) para que las páginas no se pisen.
/// </summary>
public sealed class SortMap<T>
{
    private readonly List<(string Name, bool DefaultDesc, Func<IQueryable<T>, bool, IOrderedQueryable<T>> Apply)> _keys = new();
    private readonly Expression<Func<T, Guid>> _tieBreaker;

    public SortMap(Expression<Func<T, Guid>> tieBreaker)
    {
        _tieBreaker = tieBreaker;
    }

    public SortMap<T> Add<TKey>(string name, Expression<Func<T, TKey>> key, bool defaultDesc = false)
    {
        _keys.Add((name, defaultDesc, (q, desc) => desc ? q.OrderByDescending(key) : q.OrderBy(key)));
        return this;
    }

    public bool TryApply(IQueryable<T> query, PagedQuery paging, out IQueryable<T> ordered, out string? error)
    {
        ordered = query;
        error = null;

        var key = string.IsNullOrWhiteSpace(paging.SortBy)
            ? _keys[0]
            : _keys.FirstOrDefault(k => k.Name.Equals(paging.SortBy.Trim(), StringComparison.OrdinalIgnoreCase));
        if (key.Name is null)
        {
            error = $"sortBy '{paging.SortBy}' inválido. Opciones: {string.Join(", ", _keys.Select(k => k.Name))}.";
            return false;
        }

        bool desc;
        switch (paging.SortDir?.Trim().ToLowerInvariant())
        {
            case null or "": desc = key.DefaultDesc; break;
            case "asc": desc = false; break;
            case "desc": desc = true; break;
            default:
                error = $"sortDir '{paging.SortDir}' inválido. Usar asc o desc.";
                return false;
        }

        ordered = key.Apply(query, desc).ThenBy(_tieBreaker);
        return true;
    }
}

public static class PagedQueryExtensions
{
    /// <summary>Ordena, cuenta y corta en SQL, y proyecta en SQL.</summary>
    public static async Task<ActionResult<PagedResult<TOut>>> ToPagedAsync<T, TOut>(
        this IQueryable<T> query, PagedQuery paging, SortMap<T> sorts,
        Expression<Func<T, TOut>> projection, CancellationToken ct)
    {
        if (!sorts.TryApply(query, paging, out var ordered, out var error))
            return new BadRequestObjectResult(new ProblemDetails { Title = error, Status = 400 });

        var (page, size) = (paging.Page!.Value, paging.PageSize!.Value);
        var total = await query.CountAsync(ct);
        var items = await ordered.Skip((page - 1) * size).Take(size).Select(projection).ToListAsync(ct);
        return new PagedResult<TOut>(items, total, page, size);
    }

    /// <summary>
    /// Igual, pero el mapeo corre en memoria sobre la página ya cortada: para DTOs que no se
    /// traducen a SQL (ej. LaborsController.MapToDto con sus Include).
    /// </summary>
    public static async Task<ActionResult<PagedResult<TOut>>> ToPagedMappedAsync<T, TOut>(
        this IQueryable<T> query, PagedQuery paging, SortMap<T> sorts,
        Func<T, TOut> map, CancellationToken ct)
    {
        if (!sorts.TryApply(query, paging, out var ordered, out var error))
            return new BadRequestObjectResult(new ProblemDetails { Title = error, Status = 400 });

        var (page, size) = (paging.Page!.Value, paging.PageSize!.Value);
        var total = await query.CountAsync(ct);
        var items = await ordered.Skip((page - 1) * size).Take(size).ToListAsync(ct);
        return new PagedResult<TOut>(items.Select(map).ToList(), total, page, size);
    }

    /// <summary>Patrón para ILIKE '%texto%' escapando los comodines que escriba el usuario.</summary>
    public static string ContainsPattern(string text) =>
        "%" + text.Trim().Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_") + "%";

    /// <summary>
    /// Las fechas de query string pueden llegar Local o Unspecified según el formato, y Npgsql
    /// solo acepta UTC contra timestamptz.
    /// </summary>
    public static DateTime AsUtc(this DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}
