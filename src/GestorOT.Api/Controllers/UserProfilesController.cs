using System.Security.Claims;
using GestorOT.Application.Interfaces;
using GestorOT.Domain.Entities;
using GestorOT.Shared.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestorOT.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UserProfilesController : ControllerBase
{
    private readonly IApplicationDbContext _context;
    private readonly IAuthService _authService;

    public UserProfilesController(IApplicationDbContext context, IAuthService authService)
    {
        _context = context;
        _authService = authService;
    }

    private bool IsSuperAdmin => User.IsInRole("SuperAdmin") 
        || User.FindFirst(ClaimTypes.Role)?.Value == "SuperAdmin";

    [HttpGet]
    public async Task<ActionResult<List<UserProfileDto>>> GetUsers()
    {
        List<UserProfile> users;
        Dictionary<Guid, string> tenants;

        if (IsSuperAdmin)
        {
            var query = _context.UserProfiles.AsQueryable();
            if (_context.CurrentTenantId != Guid.Empty)
            {
                query = query.Where(u => u.TenantId == _context.CurrentTenantId);
            }
            else
            {
                query = query.IgnoreQueryFilters();
            }

            users = await query.ToListAsync();

            tenants = await _context.Tenants
                .IgnoreQueryFilters()
                .ToDictionaryAsync(t => t.Id, t => t.Name);
        }
        else
        {
            var tenantId = _context.CurrentTenantId;
            users = await _context.UserProfiles
                .Where(u => u.TenantId == tenantId)
                .ToListAsync();

            var tenantName = await _context.Tenants
                .Where(t => t.Id == tenantId)
                .Select(t => t.Name)
                .FirstOrDefaultAsync() ?? "Empresa";

            tenants = new Dictionary<Guid, string> { { tenantId, tenantName } };
        }

        return users.Select(u => new UserProfileDto(
            u.Id,
            u.Email,
            u.DisplayName,
            u.Role,
            u.IsActive,
            u.CreatedAt,
            u.TenantId,
            u.TenantId == Guid.Empty ? "Sistema / Global" : (tenants.TryGetValue(u.TenantId, out var name) ? name : "Empresa")
        )).ToList();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<UserProfileDto>> GetUser(Guid id)
    {
        UserProfile? u;
        if (IsSuperAdmin)
        {
            u = await _context.UserProfiles.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == id);
        }
        else
        {
            u = await _context.UserProfiles.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == _context.CurrentTenantId);
        }

        if (u == null) return NotFound();

        var tenant = u.TenantId == Guid.Empty 
            ? null 
            : await _context.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == u.TenantId);

        return new UserProfileDto(u.Id, u.Email, u.DisplayName, u.Role, u.IsActive, u.CreatedAt, u.TenantId, u.TenantId == Guid.Empty ? "Sistema / Global" : tenant?.Name);
    }

    [HttpPost]
    public async Task<ActionResult<UserProfileDto>> CreateUser(UserProfileDto dto)
    {
        Guid tenantId;
        if (IsSuperAdmin)
        {
            if (dto.Role == "SuperAdmin")
            {
                tenantId = Guid.Empty;
            }
            else
            {
                tenantId = dto.TenantId != Guid.Empty ? dto.TenantId : _context.CurrentTenantId;
                if (tenantId == Guid.Empty)
                {
                    return BadRequest(new { Message = "Debe especificar una empresa para este usuario." });
                }
            }
        }
        else
        {
            if (dto.Role == "SuperAdmin")
            {
                return Forbid();
            }
            // Non-SuperAdmin users can ONLY create users for their own tenant
            tenantId = _context.CurrentTenantId;
        }

        var user = new UserProfile
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Email = dto.Email,
            DisplayName = dto.DisplayName,
            Role = dto.Role,
            IsActive = dto.IsActive,
            CreatedAt = DateTime.UtcNow
        };

        var rawPassword = !string.IsNullOrWhiteSpace(dto.Password) ? dto.Password : "admin123";
        _authService.CreatePasswordHash(rawPassword, out var hash, out var salt);
        user.PasswordHash = hash;
        user.PasswordSalt = salt;

        _context.UserProfiles.Add(user);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetUser), new { id = user.Id }, 
            new UserProfileDto(user.Id, user.Email, user.DisplayName, user.Role, user.IsActive, user.CreatedAt, user.TenantId));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateUser(Guid id, UserProfileDto dto)
    {
        UserProfile? user;
        if (IsSuperAdmin)
        {
            user = await _context.UserProfiles.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == id);
        }
        else
        {
            user = await _context.UserProfiles.FirstOrDefaultAsync(u => u.Id == id && u.TenantId == _context.CurrentTenantId);
        }

        if (user == null) return NotFound();

        if (!IsSuperAdmin)
        {
            if (user.Role == "SuperAdmin" || dto.Role == "SuperAdmin")
            {
                return Forbid();
            }
            // Forzar a mantener el tenant propio
            dto = dto with { TenantId = _context.CurrentTenantId };
        }

        user.Email = dto.Email;
        user.DisplayName = dto.DisplayName;
        user.Role = dto.Role;
        user.IsActive = dto.IsActive;

        if (IsSuperAdmin && dto.TenantId != Guid.Empty)
        {
            user.TenantId = dto.TenantId;
        }

        if (!string.IsNullOrWhiteSpace(dto.Password))
        {
            _authService.CreatePasswordHash(dto.Password, out var newHash, out var newSalt);
            user.PasswordHash = newHash;
            user.PasswordSalt = newSalt;
        }

        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{id}/set-password")]
    public async Task<IActionResult> SetPassword(Guid id, [FromBody] SetPasswordRequestDto req)
    {
        if (string.IsNullOrWhiteSpace(req.NewPassword) || req.NewPassword.Length < 6)
        {
            return BadRequest(new { Message = "La contraseña debe tener al menos 6 caracteres." });
        }

        UserProfile? user;
        if (IsSuperAdmin)
        {
            user = await _context.UserProfiles.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == id);
        }
        else
        {
            user = await _context.UserProfiles.FirstOrDefaultAsync(u => u.Id == id && u.TenantId == _context.CurrentTenantId);
        }

        if (user == null) return NotFound();

        if (!IsSuperAdmin && user.Role == "SuperAdmin")
        {
            return Forbid();
        }

        _authService.CreatePasswordHash(req.NewPassword, out var hash, out var salt);
        user.PasswordHash = hash;
        user.PasswordSalt = salt;

        await _context.SaveChangesAsync();
        return Ok(new { Message = "Contraseña actualizada correctamente." });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteUser(Guid id)
    {
        UserProfile? user;
        if (IsSuperAdmin)
        {
            user = await _context.UserProfiles.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == id);
        }
        else
        {
            user = await _context.UserProfiles.FirstOrDefaultAsync(u => u.Id == id && u.TenantId == _context.CurrentTenantId);
        }

        if (user == null) return NotFound();

        if (!IsSuperAdmin && user.Role == "SuperAdmin")
        {
            return Forbid();
        }

        _context.UserProfiles.Remove(user);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}
