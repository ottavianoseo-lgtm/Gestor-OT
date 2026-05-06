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
                lt.Id, lt.Name, lt.Description, lt.ExternalErpId))
            .ToListAsync(ct);
    }

    [HttpGet("activities")]
    public async Task<ActionResult<List<ErpActivityDto>>> GetActivities(CancellationToken ct)
    {
        return await _context.ErpActivities
            .AsNoTracking()
            .OrderBy(a => a.Name)
            .Select(a => new ErpActivityDto(
                a.Id, a.Name, a.ExternalErpId))
            .ToListAsync(ct);
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
}

public class UpdateContactRoleRequest
{
    public ContactRole Role { get; set; }
}
