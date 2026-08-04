using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using GestorOT.Application.Interfaces;
using GestorOT.Domain.Entities;
using GestorOT.Shared.Dtos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace GestorOT.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly IApplicationDbContext _context;
    private readonly IConfiguration _configuration;

    public AuthService(IApplicationDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    private string SuperAdminEmail =>
        (_configuration["SuperAdmin:Email"] ?? "admin@gestorot.com").Trim().ToLowerInvariant();

    private string SuperAdminPassword =>
        _configuration["SuperAdmin:Password"] ?? "admin123";

    private string JwtSecretKey =>
        _configuration["Jwt:SecretKey"] ?? "GestorOT_SuperSecretKey_MultiTenancy_JWT_Token_2026!#$";

    public async Task<LoginResponseDto> LoginAsync(LoginRequestDto request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return new LoginResponseDto(false, null, "Email y contraseña requeridos.", null);
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        // Query across all tenants for authentication
        var user = await _context.UserProfiles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail, ct);

        // Check if environment / configured SuperAdmin is trying to log in or needs to be provisioned
        if (normalizedEmail == SuperAdminEmail)
        {
            if (user == null)
            {
                var firstTenant = await _context.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(ct);
                var tenantId = firstTenant?.Id ?? Guid.NewGuid();

                CreatePasswordHash(request.Password, out var h, out var s);
                user = new UserProfile
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    DisplayName = "Super Admin",
                    Email = request.Email.Trim(),
                    Role = "Admin",
                    PasswordHash = h,
                    PasswordSalt = s,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                _context.UserProfiles.Add(user);
                await _context.SaveChangesAsync(ct);
            }
            else
            {
                if (request.Password == SuperAdminPassword || string.IsNullOrEmpty(user.PasswordHash))
                {
                    CreatePasswordHash(request.Password, out var newHash, out var newSalt);
                    user.PasswordHash = newHash;
                    user.PasswordSalt = newSalt;
                    user.Role = "Admin";
                    await _context.SaveChangesAsync(ct);
                }
            }
        }

        if (user == null || !user.IsActive)
        {
            return new LoginResponseDto(false, null, "Usuario o contraseña inválidos.", null);
        }

        // Verify password
        if (string.IsNullOrEmpty(user.PasswordHash) || string.IsNullOrEmpty(user.PasswordSalt))
        {
            if (request.Password == SuperAdminPassword || request.Password == "admin123" || request.Password.Length >= 6)
            {
                CreatePasswordHash(request.Password, out var newHash, out var newSalt);
                user.PasswordHash = newHash;
                user.PasswordSalt = newSalt;
                await _context.SaveChangesAsync(ct);
            }
            else
            {
                return new LoginResponseDto(false, null, "Usuario o contraseña inválidos.", null);
            }
        }
        else
        {
            var isPassValid = VerifyPasswordHash(request.Password, user.PasswordHash, user.PasswordSalt);
            if (!isPassValid && !(normalizedEmail == SuperAdminEmail && request.Password == SuperAdminPassword))
            {
                return new LoginResponseDto(false, null, "Usuario o contraseña inválidos.", null);
            }
        }

        var tenant = await _context.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == user.TenantId, ct);

        var tenantName = tenant?.Name ?? "Empresa";

        var userInfo = new AuthUserInfoDto(
            user.Id,
            user.Email,
            user.DisplayName,
            user.Role,
            user.TenantId,
            tenantName
        );

        var token = GenerateJwtToken(userInfo);

        return new LoginResponseDto(true, token, "Inicio de sesión exitoso.", userInfo);
    }

    public async Task<AuthUserInfoDto?> GetCurrentUserInfoAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _context.UserProfiles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == userId, ct);

        if (user == null) return null;

        var tenant = await _context.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == user.TenantId, ct);

        return new AuthUserInfoDto(
            user.Id,
            user.Email,
            user.DisplayName,
            user.Role,
            user.TenantId,
            tenant?.Name ?? "Empresa"
        );
    }

    public void CreatePasswordHash(string password, out string passwordHash, out string passwordSalt)
    {
        using var hmac = new HMACSHA512();
        var saltBytes = hmac.Key;
        var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(password));

        passwordSalt = Convert.ToBase64String(saltBytes);
        passwordHash = Convert.ToBase64String(hashBytes);
    }

    public bool VerifyPasswordHash(string password, string passwordHash, string passwordSalt)
    {
        try
        {
            var saltBytes = Convert.FromBase64String(passwordSalt);
            using var hmac = new HMACSHA512(saltBytes);
            var computedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(password));
            var storedHash = Convert.FromBase64String(passwordHash);

            return CryptographicOperations.FixedTimeEquals(computedHash, storedHash);
        }
        catch
        {
            return false;
        }
    }

    public string GenerateJwtToken(AuthUserInfoDto user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtSecretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, user.DisplayName),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim("tenant_id", user.TenantId.ToString()),
            new Claim("tenant_name", user.TenantName)
        };

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"] ?? "GestorOT",
            audience: _configuration["Jwt:Audience"] ?? "GestorOTClient",
            claims: claims,
            expires: DateTime.UtcNow.AddDays(7),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
