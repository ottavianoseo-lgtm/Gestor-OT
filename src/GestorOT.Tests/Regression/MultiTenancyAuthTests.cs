using System.Security.Claims;
using GestorOT.Api.Extensions;
using GestorOT.Domain.Entities;
using GestorOT.Infrastructure.Data;
using GestorOT.Infrastructure.Services;
using GestorOT.Shared.Dtos;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace GestorOT.Tests.Regression;

public class MultiTenancyAuthTests
{
    private ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    private IConfiguration CreateConfiguration()
    {
        var inMemorySettings = new Dictionary<string, string?>
        {
            {"Jwt:SecretKey", "TestSecretKey_MultiTenancy_JWT_Token_2026!#$"},
            {"Jwt:Issuer", "GestorOTTest"},
            {"Jwt:Audience", "GestorOTClientTest"}
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();
    }

    [Fact]
    public void PasswordHashing_HashAndVerify_ReturnsTrueForCorrectPassword()
    {
        var context = CreateContext();
        var config = CreateConfiguration();
        var authService = new AuthService(context, config);

        string password = "MySecretPassword123!";
        authService.CreatePasswordHash(password, out var hash, out var salt);

        Assert.NotNull(hash);
        Assert.NotNull(salt);
        Assert.True(authService.VerifyPasswordHash(password, hash, salt));
        Assert.False(authService.VerifyPasswordHash("WrongPassword", hash, salt));
    }

    [Fact]
    public void GenerateJwtToken_IncludesTenantIdAndRoleClaims()
    {
        var context = CreateContext();
        var config = CreateConfiguration();
        var authService = new AuthService(context, config);

        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var userInfo = new AuthUserInfoDto(
            userId,
            "admin@empresa.com",
            "Admin Test",
            "Admin",
            tenantId,
            "Empresa Test"
        );

        var token = authService.GenerateJwtToken(userInfo);
        Assert.NotNull(token);
        Assert.NotEmpty(token);

        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        var tenantClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == "tenant_id")?.Value;
        var roleClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value;

        Assert.Equal(tenantId.ToString(), tenantClaim);
        Assert.Equal("Admin", roleClaim);
    }

    [Fact]
    public void CurrentTenantService_ReadsTenantIdFromUserClaimsFirst()
    {
        var tenantId = Guid.NewGuid();

        var claims = new[]
        {
            new Claim("tenant_id", tenantId.ToString()),
            new Claim(ClaimTypes.Role, "Admin")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        var httpContext = new DefaultHttpContext
        {
            User = principal
        };
        httpContext.Request.Headers["X-Tenant-ID"] = Guid.NewGuid().ToString(); // Headers should be overridden by claim

        var accessor = new HttpContextAccessor { HttpContext = httpContext };
        var tenantService = new CurrentTenantService(accessor);

        Assert.Equal(tenantId, tenantService.TenantId);
    }

    [Fact]
    public async Task CreateTenant_AutomaticallyCreatesAdminUser()
    {
        var context = CreateContext();
        var config = CreateConfiguration();
        var logger = Moq.Mock.Of<Microsoft.Extensions.Logging.ILogger<EncryptionService>>();
        var encryptionService = new EncryptionService(config, logger);
        var tenantService = new TenantService(context, encryptionService);

        var tenantName = "Agropecuaria Norte";
        await tenantService.CreateTenantAsync(tenantName, null, null);

        var tenant = await context.Tenants.FirstOrDefaultAsync(t => t.Name == tenantName);
        Assert.NotNull(tenant);

        var adminUser = await context.UserProfiles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.TenantId == tenant.Id);

        Assert.NotNull(adminUser);
        Assert.Equal("Admin", adminUser.Role);
        Assert.True(adminUser.Email.Contains("agropecuarianorte"));
    }
}
