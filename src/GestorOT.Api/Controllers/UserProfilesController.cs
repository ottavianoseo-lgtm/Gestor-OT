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

    public UserProfilesController(IApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<List<UserProfileDto>>> GetUsers()
    {
        return await _context.UserProfiles
            .Select(u => new UserProfileDto(
                u.Id,
                u.Email,
                u.DisplayName,
                u.Role,
                u.IsActive,
                u.CreatedAt))
            .ToListAsync();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<UserProfileDto>> GetUser(Guid id)
    {
        var u = await _context.UserProfiles.FindAsync(id);
        if (u == null) return NotFound();

        return new UserProfileDto(u.Id, u.Email, u.DisplayName, u.Role, u.IsActive, u.CreatedAt);
    }

    [HttpPost]
    public async Task<ActionResult<UserProfileDto>> CreateUser(UserProfileDto dto)
    {
        var user = new UserProfile
        {
            Id = Guid.NewGuid(),
            Email = dto.Email,
            DisplayName = dto.DisplayName,
            Role = dto.Role,
            IsActive = dto.IsActive,
            CreatedAt = DateTime.UtcNow
        };

        _context.UserProfiles.Add(user);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetUser), new { id = user.Id }, 
            new UserProfileDto(user.Id, user.Email, user.DisplayName, user.Role, user.IsActive, user.CreatedAt));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateUser(Guid id, UserProfileDto dto)
    {
        var user = await _context.UserProfiles.FindAsync(id);
        if (user == null) return NotFound();

        user.Email = dto.Email;
        user.DisplayName = dto.DisplayName;
        user.Role = dto.Role;
        user.IsActive = dto.IsActive;

        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteUser(Guid id)
    {
        var user = await _context.UserProfiles.FindAsync(id);
        if (user == null) return NotFound();

        _context.UserProfiles.Remove(user);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}
