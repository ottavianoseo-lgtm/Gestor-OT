using GestorOT.Api.Extensions;
using GestorOT.Application.Interfaces;
using GestorOT.Domain.Entities;
using GestorOT.Domain.Enums;
using GestorOT.Shared.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestorOT.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CatalogsController : ControllerBase
{
    private readonly IApplicationDbContext _context;

    public CatalogsController(IApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet("labor-types")]
    public async Task<ActionResult<List<LaborTypeDto>>> GetLaborTypes(CancellationToken ct)
    {
        return await _context.LaborTypes
            .AsNoTracking()
            .OrderBy(lt => lt.Name)
            .Select(lt => new LaborTypeDto(
                lt.Id, lt.Name, lt.Description, lt.ExternalErpId, lt.ExecutionMode))
            .ToListAsync(ct);
    }

    [HttpGet("activities")]
    public async Task<ActionResult<List<ErpActivityDto>>> GetActivities([FromQuery] bool includeInactive = false, CancellationToken ct = default)
    {
        var tenantActivities = await _context.ErpActivities
            .AsNoTracking()
            .Where(a => a.TenantId == _context.CurrentTenantId && (includeInactive || a.IsActive))
            .OrderBy(a => a.Name)
            .Select(a => new ErpActivityDto(
                a.Id, a.Name, a.ExternalErpId, a.IsActive))
            .ToListAsync(ct);

        if (tenantActivities.Any() || _context.CurrentTenantId == Guid.Empty)
        {
            return tenantActivities;
        }

        return await _context.ErpActivities
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(a => a.TenantId == Guid.Empty && (includeInactive || a.IsActive))
            .OrderBy(a => a.Name)
            .Select(a => new ErpActivityDto(
                a.Id, a.Name, a.ExternalErpId, a.IsActive))
            .ToListAsync(ct);
    }

    [HttpPut("activities/{id:guid}/toggle-active")]
    public async Task<IActionResult> ToggleActivityActive(Guid id, CancellationToken ct)
    {
        var activity = await _context.ErpActivities
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(a => (a.TenantId == _context.CurrentTenantId || a.TenantId == Guid.Empty) && a.Id == id, ct);
        if (activity == null) return NotFound("Actividad no encontrada.");

        if (activity.TenantId == Guid.Empty && _context.CurrentTenantId != Guid.Empty)
        {
            // Si es una actividad global, la clonamos para el tenant para no mutar el catálogo global
            var localized = new ErpActivity
            {
                Id = Guid.NewGuid(),
                TenantId = _context.CurrentTenantId,
                Name = activity.Name,
                ExternalErpId = activity.ExternalErpId,
                IsActive = !activity.IsActive
            };
            _context.ErpActivities.Add(localized);
            await _context.SaveChangesAsync(ct);
            return Ok(new { localized.Id, localized.IsActive });
        }

        activity.IsActive = !activity.IsActive;
        await _context.SaveChangesAsync(ct);

        return Ok(new { activity.Id, activity.IsActive });
    }

    [HttpGet("contacts")]
    public async Task<ActionResult<List<ContactDto>>> GetContacts(CancellationToken ct)
    {
        return await _context.Contacts
            .AsNoTracking()
            .Select(c => new ContactDto(
                c.Id, c.FullName, c.ExternalErpId, c.Email, c.Position, c.LegalName, c.VatNumber, c.Role))
            .ToListAsync(ct);
    }

    [HttpPut("contacts/{id:guid}/role")]
    public async Task<IActionResult> UpdateContactRole(Guid id, [FromBody] UpdateContactRoleRequest request)
    {
        var contact = await _context.Contacts.FirstOrDefaultAsync(c => c.Id == id);
        if (contact == null) return NotFound();

        contact.Role = request.Role;
        await _context.SaveChangesAsync();
        return NoContent();
    }

    // --- Búsquedas paginadas (ver PagedQuery) ---

    private static readonly SortMap<LaborType> LaborTypeSorts = new SortMap<LaborType>(t => t.Id)
        .Add("name", t => t.Name);

    [HttpGet("labor-types/search")]
    public Task<ActionResult<PagedResult<LaborTypeDto>>> SearchLaborTypes(
        [FromQuery] PagedQuery paging,
        [FromQuery] string? search = null,
        [FromQuery] LaborExecutionMode? executionMode = null,
        CancellationToken ct = default)
    {
        var query = _context.LaborTypes.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = PagedQueryExtensions.ContainsPattern(search);
            query = query.Where(t => EF.Functions.ILike(t.Name, pattern) || EF.Functions.ILike(t.Description ?? "", pattern));
        }
        if (executionMode.HasValue)
            query = query.Where(t => t.ExecutionMode == executionMode);

        return query.ToPagedAsync(paging, LaborTypeSorts,
            t => new LaborTypeDto(t.Id, t.Name, t.Description, t.ExternalErpId, t.ExecutionMode), ct);
    }

    private static readonly SortMap<ErpActivity> ActivitySorts = new SortMap<ErpActivity>(a => a.Id)
        .Add("name", a => a.Name);

    [HttpGet("activities/search")]
    public async Task<ActionResult<PagedResult<ErpActivityDto>>> SearchActivities(
        [FromQuery] PagedQuery paging,
        [FromQuery] string? search = null,
        [FromQuery] bool includeInactive = false,
        CancellationToken ct = default)
    {
        // Mismo criterio que GetActivities: si el tenant no tiene actividades propias, se usan las globales.
        var tenantId = _context.CurrentTenantId;
        var hasOwn = tenantId == Guid.Empty || await _context.ErpActivities.AnyAsync(a => a.TenantId == tenantId, ct);
        var query = hasOwn
            ? _context.ErpActivities.AsNoTracking().Where(a => a.TenantId == tenantId)
            : _context.ErpActivities.IgnoreQueryFilters().AsNoTracking().Where(a => a.TenantId == Guid.Empty);

        query = query.Where(a => includeInactive || a.IsActive);
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(a => EF.Functions.ILike(a.Name, PagedQueryExtensions.ContainsPattern(search)));

        return await query.ToPagedAsync(paging, ActivitySorts,
            a => new ErpActivityDto(a.Id, a.Name, a.ExternalErpId, a.IsActive), ct);
    }

    private static readonly SortMap<Contact> ContactSorts = new SortMap<Contact>(c => c.Id)
        .Add("name", c => c.FullName)
        .Add("legalName", c => c.LegalName)
        .Add("role", c => c.Role);

    [HttpGet("contacts/search")]
    public Task<ActionResult<PagedResult<ContactDto>>> SearchContacts(
        [FromQuery] PagedQuery paging,
        [FromQuery] string? search = null,
        [FromQuery] List<ContactRole>? roles = null,
        CancellationToken ct = default)
    {
        var query = _context.Contacts.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = PagedQueryExtensions.ContainsPattern(search);
            query = query.Where(c => EF.Functions.ILike(c.FullName, pattern)
                || EF.Functions.ILike(c.LegalName ?? "", pattern)
                || EF.Functions.ILike(c.Email ?? "", pattern));
        }
        if (roles is { Count: > 0 })
            query = query.Where(c => roles.Contains(c.Role));

        return query.ToPagedAsync(paging, ContactSorts,
            c => new ContactDto(c.Id, c.FullName, c.ExternalErpId, c.Email, c.Position, c.LegalName, c.VatNumber, c.Role), ct);
    }
}

public class UpdateContactRoleRequest
{
    public ContactRole Role { get; set; }
}
