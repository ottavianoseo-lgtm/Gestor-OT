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
    public void CurrentTenantService_NonSuperAdmin_CannotOverrideTenantWithHeader()
    {
        var tenantId = Guid.NewGuid();
        var attackerTenantId = Guid.NewGuid();

        var claims = new[]
        {
            new Claim("tenant_id", tenantId.ToString()),
            new Claim(ClaimTypes.Role, "Admin")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        var httpContext = new DefaultHttpContext { User = principal };
        httpContext.Request.Headers["X-Tenant-ID"] = attackerTenantId.ToString();

        var accessor = new HttpContextAccessor { HttpContext = httpContext };
        var tenantService = new CurrentTenantService(accessor);

        // Debería devolver siempre tenantId del JWT y no attackerTenantId
        Assert.Equal(tenantId, tenantService.TenantId);
    }

    [Fact]
    public void CurrentTenantService_SuperAdmin_CanOperateGloballyOrWithHeader()
    {
        var selectedTenantId = Guid.NewGuid();

        var claims = new[]
        {
            new Claim("tenant_id", Guid.Empty.ToString()),
            new Claim(ClaimTypes.Role, "SuperAdmin")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        // Sin header: opera en modo Global (Guid.Empty)
        var httpContextGlobal = new DefaultHttpContext { User = principal };
        var accessorGlobal = new HttpContextAccessor { HttpContext = httpContextGlobal };
        var tenantServiceGlobal = new CurrentTenantService(accessorGlobal);
        Assert.Equal(Guid.Empty, tenantServiceGlobal.TenantId);

        // Con header: adopta el tenant seleccionado
        var httpContextSelected = new DefaultHttpContext { User = principal };
        httpContextSelected.Request.Headers["X-Tenant-ID"] = selectedTenantId.ToString();
        var accessorSelected = new HttpContextAccessor { HttpContext = httpContextSelected };
        var tenantServiceSelected = new CurrentTenantService(accessorSelected);
        Assert.Equal(selectedTenantId, tenantServiceSelected.TenantId);
    }

    [Fact]
    public async Task ApplicationDbContext_NonSuperAdmin_CannotOverrideTenantFilterWithHeader()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        var claims = new[]
        {
            new Claim("tenant_id", tenantA.ToString()),
            new Claim(ClaimTypes.Role, "Admin")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        var httpContext = new DefaultHttpContext { User = principal };
        httpContext.Request.Headers["X-Tenant-ID"] = tenantB.ToString(); // Intento de spoofing

        var accessor = new HttpContextAccessor { HttpContext = httpContext };
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new ApplicationDbContext(options, accessor);

        // Sembrar datos para Tenant A y Tenant B
        context.Fields.Add(new Field { Id = Guid.NewGuid(), TenantId = tenantA, Name = "Campo Tenant A" });
        context.Fields.Add(new Field { Id = Guid.NewGuid(), TenantId = tenantB, Name = "Campo Tenant B" });
        await context.SaveChangesAsync();

        var visibleFields = await context.Fields.ToListAsync();

        // El usuario solo debe ver los campos de Tenant A
        Assert.Single(visibleFields);
        Assert.Equal("Campo Tenant A", visibleFields[0].Name);
        Assert.Equal(tenantA, visibleFields[0].TenantId);
    }

    [Fact]
    public async Task ApplicationDbContext_SuperAdmin_CanQueryAllOrSpecificTenant()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        var claims = new[]
        {
            new Claim("tenant_id", Guid.Empty.ToString()),
            new Claim(ClaimTypes.Role, "SuperAdmin")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        // 1. SuperAdmin en modo Global (sin header)
        var httpContextGlobal = new DefaultHttpContext { User = principal };
        var accessorGlobal = new HttpContextAccessor { HttpContext = httpContextGlobal };

        using (var context = new ApplicationDbContext(options, accessorGlobal))
        {
            context.Fields.Add(new Field { Id = Guid.NewGuid(), TenantId = tenantA, Name = "Campo Tenant A" });
            context.Fields.Add(new Field { Id = Guid.NewGuid(), TenantId = tenantB, Name = "Campo Tenant B" });
            await context.SaveChangesAsync();

            var allFields = await context.Fields.ToListAsync();
            Assert.Equal(2, allFields.Count);
        }

        // 2. SuperAdmin con header para Tenant B
        var httpContextSelected = new DefaultHttpContext { User = principal };
        httpContextSelected.Request.Headers["X-Tenant-ID"] = tenantB.ToString();
        var accessorSelected = new HttpContextAccessor { HttpContext = httpContextSelected };

        using (var context = new ApplicationDbContext(options, accessorSelected))
        {
            var tenantBFields = await context.Fields.ToListAsync();
            Assert.Single(tenantBFields);
            Assert.Equal("Campo Tenant B", tenantBFields[0].Name);
        }
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
        Assert.Contains("agropecuarianorte", adminUser.Email);
    }
}
