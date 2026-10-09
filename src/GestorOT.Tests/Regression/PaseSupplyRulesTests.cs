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
/// Los insumos de una labor se imputan con sus propias reglas (Aplica a: Insumo), no con la de
/// la labor. En la planilla con la que importan al G4 la labor de contratista va con comprobante
/// 1080, el contratista y ARS; sus insumos con 1077, sin persona, USD, lista 3 y contra stock.
/// La cuenta del debe del insumo depende del rubro (semillas, herbicidas...).
/// </summary>
public class PaseSupplyRulesTests
{
    private const long CentroDelLote = 10270000;
    private const long CuentaDebeSemillas = 11740325;
    private const long CuentaDebeInsumoGeneral = 11740360;

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

    private record Seeded(Guid TenantId, Guid LaborId, Guid SemillaId, Guid HerbicidaId);

    private Seeded Seed(string dbName, bool conReglasDeInsumo)
    {
        var tenantId = Guid.NewGuid();
        using var ctx = CreateContext(dbName, tenantId);

        var laborType = new LaborType { Id = Guid.NewGuid(), TenantId = tenantId, Name = "SIEMBRA", ExternalErpId = "50798" };
        ctx.LaborTypes.Add(laborType);

        ctx.AccountConfigurations.Add(new AccountConfiguration
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Description = "Labor contratista",
            CodEmpresa = 4,
            CodComprobante = 1080,
            CodMoneda = 1,
            CodListaDePrecios = 7,
            CodPersona = 339,
            CodCuentaDebeGestion = 11740310,
            CodCuentaHaberGestion = 21100010,
            CodCuentaHaberCentro = 10000005
        });

        if (conReglasDeInsumo)
        {
            AccountConfiguration ReglaInsumo(string descripcion, string? rubro, long debe) => new()
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                AppliesTo = AccountRuleTarget.Supply,
                Description = descripcion,
                SupplySubGroup = rubro,
                CodEmpresa = 4,
                CodComprobante = 1077,
                CodMoneda = 2,
                CodListaDePrecios = 3,
                CodCuentaDebeGestion = debe,
                CodCuentaHaberGestion = 11732000,
                CodCuentaHaberCentro = 10010000
            };

            ctx.AccountConfigurations.AddRange(
                ReglaInsumo("Insumos general", null, CuentaDebeInsumoGeneral),
                ReglaInsumo("Semillas", "SEMILLAS", CuentaDebeSemillas));
        }

        var field = new Field { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Amanda", CodCentro = CentroDelLote };
        var lot = new Lot { Id = Guid.NewGuid(), TenantId = tenantId, FieldId = field.Id, Name = "Lote 1" };
        ctx.Fields.Add(field);
        ctx.Lots.Add(lot);

        var semilla = new Inventory { Id = Guid.NewGuid(), TenantId = tenantId, ItemName = "Girasol P101", ExternalErpId = "50410", SubGrupoConcepto = "Semillas" };
        var herbicida = new Inventory { Id = Guid.NewGuid(), TenantId = tenantId, ItemName = "Glifosato", ExternalErpId = "50048", SubGrupoConcepto = "HERBICIDAS" };
        ctx.Inventories.AddRange(semilla, herbicida);

        var labor = new Labor
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            LotId = lot.Id,
            LaborTypeId = laborType.Id,
            Status = LaborStatus.Realized,
            ExecutionDate = new DateTime(2026, 2, 21, 0, 0, 0, DateTimeKind.Utc),
            Hectares = 28,
            EffectiveArea = 28,
            Rate = 12537
        };
        ctx.Labors.Add(labor);

        LaborSupply Aplicacion(Inventory insumo, decimal total) => new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            LaborId = labor.Id,
            SupplyId = insumo.Id,
            PlannedHectares = 28,
            PlannedDose = total / 28,
            PlannedTotal = total
        };

        ctx.LaborSupplies.AddRange(Aplicacion(semilla, 5.6m), Aplicacion(herbicida, 7m));
        ctx.SaveChanges();

        return new Seeded(tenantId, labor.Id, semilla.Id, herbicida.Id);
    }

    private async Task<(List<PaseImputacion> Pases, List<string> Warnings)> GenerarAsync(string dbName, Seeded s)
    {
        using var ctx = CreateContext(dbName, s.TenantId);
        var service = new PaseBuilderService(ctx, NullLogger<PaseBuilderService>.Instance);

        var result = await service.GenerarLoteAsync(s.TenantId, null, new List<Guid> { s.LaborId });
        Assert.True(result.Success, result.Error);

        return (await ctx.PasesImputacion.AsNoTracking().ToListAsync(), result.Warnings.ToList());
    }

    private static PaseImputacion Linea(List<PaseImputacion> pases, string concepto) =>
        pases.Single(p => p.CodConcepto == long.Parse(concepto));

    [Fact]
    public async Task Insumo_UsaLaReglaDeInsumo_YNoLaDeLaLabor()
    {
        var dbName = Guid.NewGuid().ToString();
        var s = Seed(dbName, conReglasDeInsumo: true);

        var (pases, _) = await GenerarAsync(dbName, s);
        var herbicida = Linea(pases, "50048");

        Assert.Equal(1077, herbicida.CodComprobante);
        Assert.Null(herbicida.CodPersona);
        Assert.Equal(2, herbicida.CodMoneda);
        Assert.Equal(3, herbicida.CodListaDePrecios);
        Assert.Equal(11732000, herbicida.CodCuentaHaberGestion);
        Assert.Equal(10010000, herbicida.CodCuentaHaberCentro);
        Assert.Equal(CentroDelLote, herbicida.CodCuentaDebeCentro);
        Assert.Equal(28, herbicida.CantidadAuxiliar);
        Assert.Equal(7, herbicida.Cantidad);
    }

    [Fact]
    public async Task Labor_NoTomaLasReglasDeInsumo()
    {
        var dbName = Guid.NewGuid().ToString();
        var s = Seed(dbName, conReglasDeInsumo: true);

        var (pases, _) = await GenerarAsync(dbName, s);
        var labor = Linea(pases, "50798");

        Assert.Equal(1080, labor.CodComprobante);
        Assert.Equal(339, labor.CodPersona);
        Assert.Equal(7, labor.CodListaDePrecios);
        Assert.Equal(10000005, labor.CodCuentaHaberCentro);
    }

    [Fact]
    public async Task ReglaDelRubro_LeGanaALaGeneralDeInsumos()
    {
        var dbName = Guid.NewGuid().ToString();
        var s = Seed(dbName, conReglasDeInsumo: true);

        var (pases, _) = await GenerarAsync(dbName, s);

        Assert.Equal(CuentaDebeSemillas, Linea(pases, "50410").CodCuentaDebeGestion);
        Assert.Equal(CuentaDebeInsumoGeneral, Linea(pases, "50048").CodCuentaDebeGestion);
    }

    [Fact]
    public async Task LaborEInsumos_VanEnPasesDistintos()
    {
        var dbName = Guid.NewGuid().ToString();
        var s = Seed(dbName, conReglasDeInsumo: true);

        var (pases, _) = await GenerarAsync(dbName, s);

        var grupoLabor = Linea(pases, "50798").IdAgrupacionPase;
        var gruposInsumos = pases.Where(p => p.CodConcepto != 50798).Select(p => p.IdAgrupacionPase).Distinct().ToList();

        Assert.Single(gruposInsumos);
        Assert.NotEqual(grupoLabor, gruposInsumos[0]);
    }

    [Fact]
    public async Task LaborSinConcepto_SeOmite_YSusInsumosSiguen()
    {
        var dbName = Guid.NewGuid().ToString();
        var s = Seed(dbName, conReglasDeInsumo: true);
        using (var ctx = CreateContext(dbName, s.TenantId))
        {
            var tipo = await ctx.LaborTypes.SingleAsync();
            tipo.ExternalErpId = null;
            tipo.Name = "TIPO SIN CONCEPTO";
            await ctx.SaveChangesAsync();
        }

        var (pases, warnings) = await GenerarAsync(dbName, s);

        Assert.DoesNotContain(pases, p => p.CodConcepto == 0);
        Assert.Equal(2, pases.Count);
        Assert.Contains(warnings, w => w.Contains("no tiene concepto del ERP"));
    }

    [Fact]
    public async Task InsumoSinConcepto_SeOmite_YAvisa()
    {
        var dbName = Guid.NewGuid().ToString();
        var s = Seed(dbName, conReglasDeInsumo: true);
        using (var ctx = CreateContext(dbName, s.TenantId))
        {
            var herbicida = await ctx.Inventories.SingleAsync(i => i.Id == s.HerbicidaId);
            herbicida.ExternalErpId = null;
            await ctx.SaveChangesAsync();
        }

        var (pases, warnings) = await GenerarAsync(dbName, s);

        Assert.DoesNotContain(pases, p => p.CodConcepto == 0);
        Assert.Contains(warnings, w => w.Contains("Glifosato") && w.Contains("no tiene concepto del ERP"));
    }

    [Fact]
    public async Task SinReglasDeInsumo_ElInsumoSigueConLaDeLaLabor_YAvisa()
    {
        var dbName = Guid.NewGuid().ToString();
        var s = Seed(dbName, conReglasDeInsumo: false);

        var (pases, warnings) = await GenerarAsync(dbName, s);
        var herbicida = Linea(pases, "50048");

        Assert.Equal(1080, herbicida.CodComprobante);
        Assert.Equal(339, herbicida.CodPersona);
        Assert.Single(warnings, w => w.Contains("No hay reglas contables de insumo"));
    }
}
