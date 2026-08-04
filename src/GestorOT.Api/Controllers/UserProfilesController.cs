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

    [HttpGet]
    public async Task<ActionResult<List<UserProfileDto>>> GetUsers()
    {
        var users = await _context.UserProfiles
            .IgnoreQueryFilters()
            .ToListAsync();

        var tenants = await _context.Tenants
            .IgnoreQueryFilters()
            .ToDictionaryAsync(t => t.Id, t => t.Name);

        return users.Select(u => new UserProfileDto(
            u.Id,
            u.Email,
            u.DisplayName,
            u.Role,
            u.IsActive,
            u.CreatedAt,
            u.TenantId,
            tenants.TryGetValue(u.TenantId, out var name) ? name : "Empresa"
        )).ToList();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<UserProfileDto>> GetUser(Guid id)
    {
        var u = await _context.UserProfiles.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == id);
        if (u == null) return NotFound();

        var tenant = await _context.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == u.TenantId);

        return new UserProfileDto(u.Id, u.Email, u.DisplayName, u.Role, u.IsActive, u.CreatedAt, u.TenantId, tenant?.Name);
    }

    [HttpPost]
    public async Task<ActionResult<UserProfileDto>> CreateUser(UserProfileDto dto)
    {
        var tenantId = dto.TenantId != Guid.Empty ? dto.TenantId : _context.CurrentTenantId;

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

        // Create initial password from DTO or default ("admin123")
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
        var user = await _context.UserProfiles.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == id);
        if (user == null) return NotFound();

        user.Email = dto.Email;
        user.DisplayName = dto.DisplayName;
        user.Role = dto.Role;
        user.IsActive = dto.IsActive;

        if (dto.TenantId != Guid.Empty)
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

        var user = await _context.UserProfiles.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == id);
        if (user == null) return NotFound();

        _authService.CreatePasswordHash(req.NewPassword, out var hash, out var salt);
        user.PasswordHash = hash;
        user.PasswordSalt = salt;

        await _context.SaveChangesAsync();
        return Ok(new { Message = "Contraseña actualizada correctamente." });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteUser(Guid id)
    {
        var user = await _context.UserProfiles.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == id);
        if (user == null) return NotFound();

        _context.UserProfiles.Remove(user);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}
