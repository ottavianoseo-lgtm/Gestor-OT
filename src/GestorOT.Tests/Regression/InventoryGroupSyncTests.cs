using GestorOT.Api.Extensions;
using GestorOT.Domain.Entities;
using GestorOT.Infrastructure.Data;
using GestorOT.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using System.Security.Claims;
using Xunit;

namespace GestorOT.Tests.Regression;

public class InventoryGroupSyncTests
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
    public async Task GetErpInventoryGroups_RecommendsAgroGroups_AndExcludesLabor()
    {
        var tenantId = Guid.NewGuid();
        var context = CreateContext(Guid.NewGuid().ToString(), tenantId);

        var tenant = new Tenant
        {
            Id = tenantId,
            Name = "Test Agro",
            CreatedAt = DateTime.UtcNow
        };
        context.Tenants.Add(tenant);

        // Seed ErpConcepts
        context.ErpConcepts.AddRange(
            new ErpConcept { Id = Guid.NewGuid(), TenantId = tenantId, ExternalErpId = "1", Description = "Glifosato", GrupoConcepto = "INSUMOS", SubGrupoConcepto = "HERBICIDA" },
            new ErpConcept { Id = Guid.NewGuid(), TenantId = tenantId, ExternalErpId = "2", Description = "Atrazina", GrupoConcepto = "INSUMOS", SubGrupoConcepto = "HERBICIDA" },
            new ErpConcept { Id = Guid.NewGuid(), TenantId = tenantId, ExternalErpId = "3", Description = "Jarrito Cortado", GrupoConcepto = "CAFETERÍA", SubGrupoConcepto = "CAFETERÍA" },
            new ErpConcept { Id = Guid.NewGuid(), TenantId = tenantId, ExternalErpId = "4", Description = "Leche", GrupoConcepto = "CAFETERÍA", SubGrupoConcepto = "CAFETERÍA" },
            new ErpConcept { Id = Guid.NewGuid(), TenantId = tenantId, ExternalErpId = "5", Description = "Siembra Soja", GrupoConcepto = "LABORES", SubGrupoConcepto = "CONTRATISTA" }
        );
        await context.SaveChangesAsync();

        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("tenant_id", tenantId.ToString()),
            new Claim(ClaimTypes.Role, "Admin")
        }, "TestAuth"));
        var accessor = new HttpContextAccessor { HttpContext = httpContext };
        var tenantService = new CurrentTenantService(accessor);

        var config = new ConfigurationBuilder().Build();
        var encService = new EncryptionService(config, NullLogger<EncryptionService>.Instance);

        var erpSyncService = new ErpSyncService(
            context,
            NullLogger<ErpSyncService>.Instance,
            encService,
            tenantService,
            null!,
            null!
        );

        var groups = await erpSyncService.GetErpInventoryGroupsAsync(tenantId);

        // Labores should NOT be in the inventory groups
        Assert.DoesNotContain(groups, g => g.GroupName.Contains("LABOR", StringComparison.OrdinalIgnoreCase));

        // INSUMOS should be present, count 2, and recommended (IsSelected = true)
        var insumosGroup = groups.FirstOrDefault(g => g.GroupName == "INSUMOS");
        Assert.NotNull(insumosGroup);
        Assert.Equal(2, insumosGroup.ItemCount);
        Assert.True(insumosGroup.IsSelected);
        Assert.Contains("Glifosato", insumosGroup.SampleItems);

        // CAFETERIA should be present, count 2, but NOT recommended by default (IsSelected = false)
        var cafeteriaGroup = groups.FirstOrDefault(g => g.GroupName == "CAFETERÍA");
        Assert.NotNull(cafeteriaGroup);
        Assert.Equal(2, cafeteriaGroup.ItemCount);
        Assert.False(cafeteriaGroup.IsSelected);
    }

    [Fact]
    public async Task SyncInventoryWithGroups_SyncsOnlySelected_AndCleansUnselected()
    {
        var tenantId = Guid.NewGuid();
        var context = CreateContext(Guid.NewGuid().ToString(), tenantId);

        var tenant = new Tenant
        {
            Id = tenantId,
            Name = "Test Agro 2",
            CreatedAt = DateTime.UtcNow
        };
        context.Tenants.Add(tenant);

        // Seed ErpConcepts
        context.ErpConcepts.AddRange(
            new ErpConcept { Id = Guid.NewGuid(), TenantId = tenantId, ExternalErpId = "101", Description = "Urea Granulada", Stock = 50, GrupoConcepto = "INSUMOS", SubGrupoConcepto = "FERTILIZANTE" },
            new ErpConcept { Id = Guid.NewGuid(), TenantId = tenantId, ExternalErpId = "102", Description = "Café en Grano", Stock = 10, GrupoConcepto = "CAFETERÍA", SubGrupoConcepto = "CAFETERÍA" }
        );

        // Existing inventories (with an old rogue cafeteria item)
        context.Inventories.AddRange(
            new Inventory { Id = Guid.NewGuid(), TenantId = tenantId, ExternalErpId = "102", ItemName = "Café en Grano", Category = "CAFETERÍA", GrupoConcepto = "CAFETERÍA" }
        );
        await context.SaveChangesAsync();

        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("tenant_id", tenantId.ToString()),
            new Claim(ClaimTypes.Role, "Admin")
        }, "TestAuth"));
        var accessor = new HttpContextAccessor { HttpContext = httpContext };
        var tenantService = new CurrentTenantService(accessor);

        var config = new ConfigurationBuilder().Build();
        var encService = new EncryptionService(config, NullLogger<EncryptionService>.Instance);

        var erpSyncService = new ErpSyncService(
            context,
            NullLogger<ErpSyncService>.Instance,
            encService,
            tenantService,
            null!,
            null!
        );

        // Sync only INSUMOS, with cleanUnselected = true
        var result = await erpSyncService.SyncInventoryWithGroupsAsync(tenantId, new List<string> { "INSUMOS" }, cleanUnselected: true);

        Assert.True(result.Success);
        Assert.Equal(1, result.SyncedCount);
        Assert.Equal(1, result.CleanedCount);

        // Verify Inventories: Urea Granulada is present, Café en Grano is removed!
        var inventories = await context.Inventories.IgnoreQueryFilters().Where(i => i.TenantId == tenantId).ToListAsync();
        Assert.Single(inventories);
        Assert.Equal("Urea Granulada", inventories[0].ItemName);
        Assert.Equal("INSUMOS", inventories[0].GrupoConcepto);

        // Verify Tenant preferences saved
        var updatedTenant = await context.Tenants.FindAsync(tenantId);
        Assert.NotNull(updatedTenant?.AllowedInventoryGroupsJson);
        Assert.Contains("INSUMOS", updatedTenant.AllowedInventoryGroupsJson);
    }
}
