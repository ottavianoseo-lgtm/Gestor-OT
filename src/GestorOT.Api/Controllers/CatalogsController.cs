using GestorOT.Application.Interfaces;
using GestorOT.Domain.Entities;
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
            .Select(lt => new LaborTypeDto(
                lt.Id, lt.Name, lt.Description, lt.ExternalErpId))
            .ToListAsync(ct);
    }

    [HttpGet("employees")]
    public async Task<ActionResult<List<EmployeeDto>>> GetEmployees(CancellationToken ct)
    {
        return await _context.Employees
            .AsNoTracking()
            .Select(e => new EmployeeDto(
                e.Id, e.FullName, e.ExternalErpId, e.Email, e.Position))
            .ToListAsync(ct);
    }
}
