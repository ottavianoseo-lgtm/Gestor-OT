using GestorOT.Data;
using GestorOT.Shared.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestorOT.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UserProfilesController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public UserProfilesController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<List<UserProfileDto>>> GetAll()
    {
        var users = await _context.UserProfiles
            .AsNoTracking()
            .OrderBy(u => u.DisplayName)
            .Select(u => new UserProfileDto(
                u.Id, u.Email, u.DisplayName, u.Role, u.IsActive, u.CreatedAt
            ))
            .ToListAsync();

        return users;
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<UserProfileDto>> GetById(Guid id)
    {
        var user = await _context.UserProfiles.FindAsync(id);
        if (user == null) return NotFound();

        return new UserProfileDto(
            user.Id, user.Email, user.DisplayName, user.Role, user.IsActive, user.CreatedAt
        );
    }

    [HttpPost]
    public async Task<ActionResult<UserProfileDto>> Create(UserProfileDto dto)
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

        return CreatedAtAction(nameof(GetById), new { id = user.Id },
            new UserProfileDto(user.Id, user.Email, user.DisplayName, user.Role, user.IsActive, user.CreatedAt));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UserProfileDto dto)
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

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var user = await _context.UserProfiles.FindAsync(id);
        if (user == null) return NotFound();

        _context.UserProfiles.Remove(user);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}
