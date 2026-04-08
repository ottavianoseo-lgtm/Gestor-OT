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
