using GestorOT.Domain.Entities;
using GestorOT.Infrastructure.Data;
using GestorOT.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using System.Security.Claims;
using Xunit;

namespace GestorOT.Tests.Regression;

/// <summary>Borrar un lote de pases se lleva sus pases y no toca los de otro tenant.</summary>
public class PaseLoteDeleteTests
{
    private ApplicationDbContext CreateContext(string dbName, Guid tenantId)
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

    private static PaseImputacion Pase(Guid tenantId, Guid loteId) => new()
    {
        Id = Guid.NewGuid(), TenantId = tenantId, PaseLoteId = loteId, IdReferencia = "1", CodEmpresa = 4, CodComprobante = 1077, CodMoneda = 1
    };

    [Fact]
    public async Task BorraElLoteYSusPases_SoloDelTenant()
    {
        var dbName = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid();
        var lote = new PaseLote { Id = Guid.NewGuid(), TenantId = tenantId };
        var otro = new PaseLote { Id = Guid.NewGuid(), TenantId = tenantId };

        using (var ctx = CreateContext(dbName, tenantId))
        {
            ctx.PasesLote.AddRange(lote, otro);
            ctx.PasesImputacion.AddRange(Pase(tenantId, lote.Id), Pase(tenantId, lote.Id), Pase(tenantId, otro.Id));
            await ctx.SaveChangesAsync();
        }

        using (var ctx = CreateContext(dbName, Guid.NewGuid()))
        {
            var ajeno = new PaseBuilderService(ctx, NullLogger<PaseBuilderService>.Instance);
            Assert.False(await ajeno.DeleteLoteAsync(Guid.NewGuid(), lote.Id));
        }

        using (var ctx = CreateContext(dbName, tenantId))
        {
            var service = new PaseBuilderService(ctx, NullLogger<PaseBuilderService>.Instance);
            Assert.True(await service.DeleteLoteAsync(tenantId, lote.Id));
        }

        using (var ctx = CreateContext(dbName, tenantId))
        {
            Assert.Equal(new[] { otro.Id }, await ctx.PasesLote.Select(l => l.Id).ToListAsync());
            Assert.Single(await ctx.PasesImputacion.ToListAsync());
        }
    }
}
