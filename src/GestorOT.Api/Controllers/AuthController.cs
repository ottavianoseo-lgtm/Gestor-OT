using System.Security.Claims;
using GestorOT.Application.Interfaces;
using GestorOT.Shared.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestorOT.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
// Anónimo por definición: login es la puerta de entrada, logout tiene que funcionar aunque el
// token ya venció, y "me" es cómo el cliente averigua si hay sesión.
[AllowAnonymous]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponseDto>> Login([FromBody] LoginRequestDto request, CancellationToken ct)
    {
        var result = await _authService.LoginAsync(request, ct);

        if (!result.Success || string.IsNullOrEmpty(result.Token))
        {
            return Unauthorized(result);
        }

        // Set HTTP-Only Cookie for session persistence
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.AddDays(7)
        };

        Response.Cookies.Append("GestorOT_SessionToken", result.Token, cookieOptions);

        return Ok(result);
    }

    [HttpPost("logout")]
    public IActionResult Logout()
    {
        Response.Cookies.Delete("GestorOT_SessionToken");
        return Ok(new { Message = "Sesión cerrada correctamente." });
    }

    [HttpGet("me")]
    public async Task<ActionResult<AuthUserInfoDto>> GetCurrentUser(CancellationToken ct)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        var userInfo = await _authService.GetCurrentUserInfoAsync(userId, ct);
        if (userInfo == null)
        {
            return NotFound();
        }

        return Ok(userInfo);
    }
}
