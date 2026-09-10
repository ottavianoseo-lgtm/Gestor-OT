using GestorOT.Domain.Entities;
using GestorOT.Domain.Enums;
using GestorOT.Infrastructure.Data;
using GestorOT.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using System.Security.Claims;
using Xunit;

namespace GestorOT.Tests.Regression;

/// <summary>
/// El centro de costo del pase G4 sale del lote, no del tipo de labor. Antes salía siempre de
/// AccountConfiguration, así que toda labor del mismo tipo imputaba al mismo centro sin importar
/// en qué lote se hizo, y el pase no servía para costo por lote.
///
/// La cadena replica la de Ganadería (el registro pisa la plantilla):
///     CampaignLot.CodCentro  ??  Lot.CodCentro  ??  AccountConfiguration.CodCuentaDebeCentro
/// </summary>
public class PaseCostCenterTests
{
    private const long CentroDelLote = 50101;
    private const long CentroDeLaCampania = 50202;
    private const long CentroDeLaConfig = 50999;

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

    private record Seeded(Guid TenantId, Guid LaborConCentroDeLote, Guid LaborConCentroDeCampania, Guid LaborSinCentro);

    private Seeded Seed(string dbName)
    {
        var tenantId = Guid.NewGuid();

        using var ctx = CreateContext(dbName, tenantId);

        var laborType = new LaborType { Id = Guid.NewGuid(), TenantId = tenantId, Name = "COSECHA SOJA", ExternalErpId = "50956" };
        ctx.LaborTypes.Add(laborType);

        // Regla contable general: aporta el centro solo como último recurso.
        ctx.AccountConfigurations.Add(new AccountConfiguration
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            LaborTypeId = null,
            CodEmpresa = 1,
            CodComprobante = 1,
            CodMoneda = 1,
            CodCuentaDebeCentro = CentroDeLaConfig,
            CodCuentaHaberCentro = CentroDeLaConfig
        });

        var field = new Field { Id = Guid.NewGuid(), TenantId = tenantId, Name = "La Juanita" };
        ctx.Fields.Add(field);

        var loteConCentro = new Lot { Id = Guid.NewGuid(), TenantId = tenantId, FieldId = field.Id, Name = "Lote 1", CodCentro = CentroDelLote };
        var loteSinCentro = new Lot { Id = Guid.NewGuid(), TenantId = tenantId, FieldId = field.Id, Name = "Lote 2", CodCentro = null };
        ctx.Lots.AddRange(loteConCentro, loteSinCentro);

        var campaign = new Campaign { Id = Guid.NewGuid(), TenantId = tenantId, Name = "26/27" };
        ctx.Campaigns.Add(campaign);

        // Mismo lote, pero la campaña le pone otro centro: tiene que ganar el de la campaña.
        var campaignLot = new CampaignLot
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CampaignId = campaign.Id,
            LotId = loteConCentro.Id,
            CodCentro = CentroDeLaCampania
        };
        ctx.CampaignLots.Add(campaignLot);

        Labor NuevaLabor(Guid lotId, Guid? campaignLotId) => new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            LotId = lotId,
            CampaignLotId = campaignLotId,
            LaborTypeId = laborType.Id,
            Status = LaborStatus.Realized,
            ExecutionDate = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc),
            Hectares = 100,
            EffectiveArea = 100,
            Rate = 1
        };

        var laborLote = NuevaLabor(loteConCentro.Id, null);
        var laborCampania = NuevaLabor(loteConCentro.Id, campaignLot.Id);
        var laborSinCentro = NuevaLabor(loteSinCentro.Id, null);

        ctx.Labors.AddRange(laborLote, laborCampania, laborSinCentro);
        ctx.SaveChanges();

        return new Seeded(tenantId, laborLote.Id, laborCampania.Id, laborSinCentro.Id);
    }

    private async Task<Dictionary<Guid, PaseImputacion>> GenerarPasesAsync(string dbName, Seeded seeded)
    {
        using var ctx = CreateContext(dbName, seeded.TenantId);
        var service = new PaseBuilderService(ctx, NullLogger<PaseBuilderService>.Instance);

        var result = await service.GenerarLoteAsync(
            seeded.TenantId,
            workOrderIds: null,
            laborIds: new List<Guid> { seeded.LaborConCentroDeLote, seeded.LaborConCentroDeCampania, seeded.LaborSinCentro },
            descripcion: "test centros");

        Assert.True(result.Success, $"La generación falló: {result.Error}");

        return await ctx.PasesImputacion
            .AsNoTracking()
            .Where(p => p.LaborId != null)
            .ToDictionaryAsync(p => p.LaborId!.Value, p => p);
    }

    [Fact]
    public async Task Pase_TomaElCentroDelLote_CuandoElLoteLoTiene()
    {
        var dbName = Guid.NewGuid().ToString();
        var seeded = Seed(dbName);

        var pases = await GenerarPasesAsync(dbName, seeded);

        var pase = pases[seeded.LaborConCentroDeLote];
        Assert.Equal(CentroDelLote, pase.CodCuentaDebeCentro);
        Assert.Equal(CentroDelLote, pase.CodCuentaHaberCentro);
    }

    [Fact]
    public async Task Pase_TomaElCentroDeLaCampania_CuandoPisaAlDelLote()
    {
        var dbName = Guid.NewGuid().ToString();
        var seeded = Seed(dbName);

        var pases = await GenerarPasesAsync(dbName, seeded);

        var pase = pases[seeded.LaborConCentroDeCampania];
        Assert.Equal(CentroDeLaCampania, pase.CodCuentaDebeCentro);
    }

    [Fact]
    public async Task Pase_CaeEnElCentroDeLaConfig_CuandoElLoteNoTiene()
    {
        var dbName = Guid.NewGuid().ToString();
        var seeded = Seed(dbName);

        var pases = await GenerarPasesAsync(dbName, seeded);

        var pase = pases[seeded.LaborSinCentro];
        Assert.Equal(CentroDeLaConfig, pase.CodCuentaDebeCentro);
    }

    [Fact]
    public async Task DosLaboresDelMismoTipo_EnLotesDistintos_ImputanACentrosDistintos()
    {
        // Es la regresión que importa: antes las dos salían con el centro de la config.
        var dbName = Guid.NewGuid().ToString();
        var seeded = Seed(dbName);

        var pases = await GenerarPasesAsync(dbName, seeded);

        var unLote = pases[seeded.LaborConCentroDeLote].CodCuentaDebeCentro;
        var otroLote = pases[seeded.LaborSinCentro].CodCuentaDebeCentro;

        Assert.NotEqual(unLote, otroLote);
    }
}
