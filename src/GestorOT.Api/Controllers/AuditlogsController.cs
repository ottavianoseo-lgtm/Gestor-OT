using GestorOT.Api.Extensions;
using GestorOT.Application.Interfaces;
using GestorOT.Application.Services;
using GestorOT.Domain.Entities;
using GestorOT.Shared.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestorOT.Api.Controllers;

[ApiController]
[Route("api/auditlogs")]
public class AuditlogsController : ControllerBase
{
    private readonly IAuditLogQueryService _queryService;
    private readonly IApplicationDbContext _context;

    public AuditlogsController(IAuditLogQueryService queryService, IApplicationDbContext context)
    {
        _queryService = queryService;
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<List<AuditLogDto>>> GetLogs([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        var logs = await _queryService.GetLogsAsync(page, pageSize);
        return Ok(logs);
    }

    // --- Búsqueda paginada (ver PagedQuery) ---

    private static readonly SortMap<AuditLog> AuditSorts = new SortMap<AuditLog>(a => a.Id)
        .Add("timestamp", a => a.Timestamp, defaultDesc: true)
        .Add("action", a => a.Action)
        .Add("entityType", a => a.EntityType)
        .Add("user", a => a.UserEmail);

    /// <summary>
    /// dateFrom inclusivo y dateBefore exclusivo, en UTC, sobre la fecha del cambio.
    /// search busca en el email del usuario y en el id de la entidad.
    /// </summary>
    [HttpGet("search")]
    public Task<ActionResult<PagedResult<AuditLogDto>>> SearchLogs(
        [FromQuery] PagedQuery paging,
        [FromQuery] string? entityType = null,
        [FromQuery] string? action = null,
        [FromQuery] DateTime? dateFrom = null,
        [FromQuery] DateTime? dateBefore = null,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        var query = _context.AuditLogs.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(entityType)) query = query.Where(a => a.EntityType == entityType);
        if (!string.IsNullOrWhiteSpace(action)) query = query.Where(a => a.Action == action);

        if (dateFrom.HasValue)
        {
            var from = dateFrom.Value.AsUtc();
            query = query.Where(a => a.Timestamp >= from);
        }
        if (dateBefore.HasValue)
        {
            var before = dateBefore.Value.AsUtc();
            query = query.Where(a => a.Timestamp < before);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = PagedQueryExtensions.ContainsPattern(search);
            query = query.Where(a => EF.Functions.ILike(a.UserEmail ?? "", pattern)
                || EF.Functions.ILike(a.EntityId ?? "", pattern));
        }

        return query.ToPagedAsync(paging, AuditSorts,
            a => new AuditLogDto(a.Id, a.UserEmail, a.Action, a.EntityType, a.EntityId, a.OldValue, a.NewValue, a.Timestamp), ct);
    }
}
