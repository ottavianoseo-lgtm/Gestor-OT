using GestorOT.Api.Controllers;
using GestorOT.Domain.Entities;
using GestorOT.Infrastructure.Data;
using GestorOT.Shared.Dtos;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Xunit;

namespace GestorOT.Tests.Regression;

public class ErpActivitySyncTests
{
    private ApplicationDbContext CreateContext(string dbName, Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        var httpContext = new DefaultHttpContext();
        if (tenantId != Guid.Empty)
        {
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim("tenant_id", tenantId.ToString()),
                new Claim(ClaimTypes.Role, "Admin")
            }, "TestAuth"));
        }

        var accessor = new HttpContextAccessor { HttpContext = httpContext };

        return new ApplicationDbContext(options, accessor);
    }

    [Fact]
    public async Task GetActivities_ReturnsTenantActivities_WhenTenantHasSyncedActivities()
    {
        var dbName = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid();

        // 1. Seed global fallback activity using global context (Guid.Empty)
        using (var globalContext = CreateContext(dbName, Guid.Empty))
        {
            globalContext.ErpActivities.Add(new ErpActivity
            {
                Id = Guid.NewGuid(),
                Name = "Trigo Global",
                TenantId = Guid.Empty,
                ExternalErpId = null
            });
            await globalContext.SaveChangesAsync();
        }

        // 2. Add tenant synced activity and another tenant activity using tenant context
        using (var tenantContext = CreateContext(dbName, tenantId))
        {
            tenantContext.ErpActivities.Add(new ErpActivity
            {
                Id = Guid.NewGuid(),
                Name = "TRIGO PAN ERP",
                TenantId = tenantId,
                ExternalErpId = "101"
            });

            // Another tenant's activity
            tenantContext.ErpActivities.Add(new ErpActivity
            {
                Id = Guid.NewGuid(),
                Name = "SOJA OTRO TENANT",
                TenantId = Guid.NewGuid(),
                ExternalErpId = "202"
            });

            await tenantContext.SaveChangesAsync();

            var controller = new CatalogsController(tenantContext);
            var result = await controller.GetActivities(false, CancellationToken.None);

            var activities = result.Value;
            Assert.NotNull(activities);
            Assert.Single(activities);
            Assert.Equal("TRIGO PAN ERP", activities[0].Name);
            Assert.Equal("101", activities[0].ExternalErpId);
        }
    }

    [Fact]
    public async Task GetActivities_FallsBackToGlobalActivities_WhenTenantHasNoSyncedActivities()
    {
        var dbName = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid();

        // Seed global fallback activity using global context
        using (var globalContext = CreateContext(dbName, Guid.Empty))
        {
            globalContext.ErpActivities.Add(new ErpActivity
            {
                Id = Guid.NewGuid(),
                Name = "Maíz Semilla",
                TenantId = Guid.Empty,
                ExternalErpId = null
            });
            await globalContext.SaveChangesAsync();
        }

        // Tenant with no synced activities
        using (var tenantContext = CreateContext(dbName, tenantId))
        {
            var controller = new CatalogsController(tenantContext);
            var result = await controller.GetActivities(false, CancellationToken.None);

            var activities = result.Value;
            Assert.NotNull(activities);
            Assert.Single(activities);
            Assert.Equal("Maíz Semilla", activities[0].Name);
        }
    }

    [Fact]
    public async Task GetActivities_FiltersInactive_UnlessIncludeInactiveIsTrue()
    {
        var dbName = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid();

        using (var context = CreateContext(dbName, tenantId))
        {
            context.ErpActivities.Add(new ErpActivity
            {
                Id = Guid.NewGuid(),
                Name = "Girasol Activo",
                TenantId = tenantId,
                ExternalErpId = "301",
                IsActive = true
            });

            context.ErpActivities.Add(new ErpActivity
            {
                Id = Guid.NewGuid(),
                Name = "Alfalfa Inactiva",
                TenantId = tenantId,
                ExternalErpId = "302",
                IsActive = false
            });

            await context.SaveChangesAsync();

            var controller = new CatalogsController(context);

            // Default: only active
            var activeResult = await controller.GetActivities(includeInactive: false, CancellationToken.None);
            Assert.NotNull(activeResult.Value);
            Assert.Single(activeResult.Value);
            Assert.Equal("Girasol Activo", activeResult.Value[0].Name);

            // With includeInactive: true
            var allResult = await controller.GetActivities(includeInactive: true, CancellationToken.None);
            Assert.NotNull(allResult.Value);
            Assert.Equal(2, allResult.Value.Count);
        }
    }

    [Fact]
    public async Task ToggleActivityActive_FlipsStateSuccessfully()
    {
        var dbName = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid();
        var activityId = Guid.NewGuid();

        using (var context = CreateContext(dbName, tenantId))
        {
            context.ErpActivities.Add(new ErpActivity
            {
                Id = activityId,
                Name = "Cebada",
                TenantId = tenantId,
                ExternalErpId = "401",
                IsActive = true
            });
            await context.SaveChangesAsync();

            var controller = new CatalogsController(context);

            // First toggle: true -> false
            var toggleRes1 = await controller.ToggleActivityActive(activityId, CancellationToken.None);
            Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(toggleRes1);

            var updated1 = await context.ErpActivities.FindAsync(activityId);
            Assert.NotNull(updated1);
            Assert.False(updated1.IsActive);

            // Second toggle: false -> true
            var toggleRes2 = await controller.ToggleActivityActive(activityId, CancellationToken.None);
            Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(toggleRes2);

            var updated2 = await context.ErpActivities.FindAsync(activityId);
            Assert.NotNull(updated2);
            Assert.True(updated2.IsActive);
        }
    }
}
