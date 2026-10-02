using GestorOT.Api.Extensions;
using GestorOT.Application.Interfaces;
using GestorOT.Domain.Entities;
using GestorOT.Shared.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestorOT.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ErpPeopleController : ControllerBase
{
    private readonly IApplicationDbContext _context;

    public ErpPeopleController(IApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<List<ErpPersonDto>>> GetErpPeople()
    {
        var people = await _context.ErpPeople
            .AsNoTracking()
            .OrderBy(p => p.FullName)
            .ToListAsync();

        return people.Select(p => new ErpPersonDto(
            p.Id,
            p.ExternalErpId,
            p.FullName,
            null,
            p.VatNumber,
            p.IsActivated,
            p.LinkedContactId
        )).ToList();
    }

    // --- Búsqueda paginada (ver PagedQuery) ---

    private static readonly SortMap<ErpPerson> PersonSorts = new SortMap<ErpPerson>(p => p.Id)
        .Add("name", p => p.FullName)
        .Add("vatNumber", p => p.VatNumber);

    /// <summary>activated=false: las que todavía no son contacto (las que se pueden activar).</summary>
    [HttpGet("search")]
    public Task<ActionResult<PagedResult<ErpPersonDto>>> SearchErpPeople(
        [FromQuery] PagedQuery paging,
        [FromQuery] string? search = null,
        [FromQuery] bool? activated = null,
        CancellationToken ct = default)
    {
        var query = _context.ErpPeople.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = PagedQueryExtensions.ContainsPattern(search);
            query = query.Where(p => EF.Functions.ILike(p.FullName, pattern)
                || EF.Functions.ILike(p.VatNumber ?? "", pattern)
                || EF.Functions.ILike(p.ExternalErpId, pattern));
        }
        if (activated.HasValue)
            query = query.Where(p => p.IsActivated == activated.Value);

        return query.ToPagedAsync(paging, PersonSorts,
            p => new ErpPersonDto(p.Id, p.ExternalErpId, p.FullName, null, p.VatNumber, p.IsActivated, p.LinkedContactId), ct);
    }

    [HttpPost("activate")]
    public async Task<IActionResult> ActivateContact([FromBody] ActivateContactRequest request)
    {
        var person = await _context.ErpPeople.FindAsync(request.ErpPersonId);
        if (person == null)
            return NotFound("Persona no encontrada en el directorio ERP.");

        if (person.IsActivated || person.LinkedContactId.HasValue)
            return BadRequest("Esta persona ya ha sido activada como contacto.");

        var existingContact = await _context.Contacts
            .FirstOrDefaultAsync(c => c.ErpPersonId == person.Id);

        if (existingContact != null)
        {
            person.IsActivated = true;
            person.LinkedContactId = existingContact.Id;
            await _context.SaveChangesAsync();
            return Ok(new { Message = "El contacto ya existía y fue enlazado exitosamente.", ContactId = existingContact.Id });
        }

        var newContact = new Contact
        {
            Id = Guid.NewGuid(),
            FullName = person.FullName,
            Position = null,
            Role = request.Role,
            ErpPersonId = person.Id
        };

        _context.Contacts.Add(newContact);
        
        person.IsActivated = true;
        person.LinkedContactId = newContact.Id;

        await _context.SaveChangesAsync();

        return Ok(new { Message = "Contacto activado exitosamente.", ContactId = newContact.Id });
    }
}
