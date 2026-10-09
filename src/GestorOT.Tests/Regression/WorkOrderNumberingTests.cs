using GestorOT.Domain.Entities;
using GestorOT.Infrastructure.Data;
using GestorOT.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Xunit;

namespace GestorOT.Tests.Regression;

/// <summary>
/// Las OTs se numeran solas, ascendentes desde la ultima del tenant. Antes la OT creada desde
/// labores sueltas quedaba sin numero, y sin numero no hay comprobante para el pase al G4.
/// </summary>
public class WorkOrderNumberingTests
{
    private static ApplicationDbContext CreateContext(string dbName, Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim("tenant_id", tenantId.ToString()),
                new Claim(ClaimTypes.Role, "Admin")
            }, "TestAuth"))
        };

        return new ApplicationDbContext(options, new HttpContextAccessor { HttpContext = httpContext });
    }

    private static void Seed(string dbName, Guid tenantId, params string[] numeros)
    {
        using var ctx = CreateContext(dbName, tenantId);
        foreach (var n in numeros)
        {
            ctx.WorkOrders.Add(new WorkOrder { Id = Guid.NewGuid(), TenantId = tenantId, OTNumber = n });
        }
        ctx.SaveChanges();
    }

    [Fact]
    public async Task SinOts_EmpiezaEnUno()
    {
        var db = Guid.NewGuid().ToString();
        var tenant = Guid.NewGuid();

        using var ctx = CreateContext(db, tenant);
        Assert.Equal(1, await WorkOrderNumbering.NextAsync(ctx));
    }

    [Fact]
    public async Task SigueDesdeElMayor_AunqueTenganPrefijoOEstenVacias()
    {
        var db = Guid.NewGuid().ToString();
        var tenant = Guid.NewGuid();
        Seed(db, tenant, "569", "OT-718", "", "sin numero", "700");

        using var ctx = CreateContext(db, tenant);
        Assert.Equal(719, await WorkOrderNumbering.NextAsync(ctx));
    }

    [Fact]
    public async Task NoMiraLasOtsDeOtroTenant()
    {
        var db = Guid.NewGuid().ToString();
        var tenant = Guid.NewGuid();
        Seed(db, tenant, "10");
        Seed(db, Guid.NewGuid(), "9000");

        using var ctx = CreateContext(db, tenant);
        Assert.Equal(11, await WorkOrderNumbering.NextAsync(ctx));
    }
}
