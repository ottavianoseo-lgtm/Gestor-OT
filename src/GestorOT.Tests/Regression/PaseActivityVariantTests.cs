using GestorOT.Domain.Entities;
using GestorOT.Domain.Enums;
using GestorOT.Infrastructure.Data;
using GestorOT.Infrastructure.Services;
using GestorOT.Shared.Dtos;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using System.Security.Claims;
using Xunit;

namespace GestorOT.Tests.Regression;

/// <summary>
/// La misma regla imputa a perfiles distintos segun la actividad de la labor (la aplicacion de
/// fertilizante va a un perfil en maiz y a otro en soja). La variante de la actividad pisa los
/// perfiles de la regla; sin variante, o sin actividad, salen los de la regla con aviso.
/// </summary>
public class PaseActivityVariantTests
{
    private const long PerfilDebeGeneral = 900;
    private const long PerfilHaberGeneral = 901;
    private const long PerfilDebeMaiz = 100;
    private const long PerfilHaberMaiz = 101;
    private const long PerfilDebeSoja = 200;
    private const long PerfilDebeFertMaiz = 300;
    private const long PerfilHaberFertMaiz = 301;

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

    private record Seeded(Guid TenantId, Guid Maiz, Guid Soja, Guid Girasol, Dictionary<string, Guid> Labores);

    private Seeded Seed(string dbName)
    {
        var tenantId = Guid.NewGuid();
        using var ctx = CreateContext(dbName, tenantId);

        var maiz = new ErpActivity { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Maiz" };
        var soja = new ErpActivity { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Soja" };
        var girasol = new ErpActivity { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Girasol" };
        ctx.ErpActivities.AddRange(maiz, soja, girasol);

        var laborType = new LaborType { Id = Guid.NewGuid(), TenantId = tenantId, Name = "FERTILIZACION", ExternalErpId = "50798" };
        ctx.LaborTypes.Add(laborType);

        var reglaLabor = new AccountConfiguration
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Description = "Labor propia",
            CodEmpresa = 4,
            CodComprobante = 1077,
            CodMoneda = 1,
            CodPerfilDebe = PerfilDebeGeneral,
            CodPerfilHaber = PerfilHaberGeneral
        };
        reglaLabor.Variants.Add(new AccountConfigurationVariant
        {
            Id = Guid.NewGuid(), ErpActivityId = maiz.Id, CodPerfilDebe = PerfilDebeMaiz, CodPerfilHaber = PerfilHaberMaiz
        });
        // Solo el debe: el haber sale de la regla.
        reglaLabor.Variants.Add(new AccountConfigurationVariant
        {
            Id = Guid.NewGuid(), ErpActivityId = soja.Id, CodPerfilDebe = PerfilDebeSoja
        });

        var reglaFert = new AccountConfiguration
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AppliesTo = AccountRuleTarget.Supply,
            Description = "Insumos - FERTILIZANTE",
            SupplySubGroup = "FERTILIZANTE",
            CodEmpresa = 4,
            CodComprobante = 1077,
            CodMoneda = 2
        };
        reglaFert.Variants.Add(new AccountConfigurationVariant
        {
            Id = Guid.NewGuid(), ErpActivityId = maiz.Id, CodPerfilDebe = PerfilDebeFertMaiz, CodPerfilHaber = PerfilHaberFertMaiz
        });
        ctx.AccountConfigurations.AddRange(reglaLabor, reglaFert);

        var field = new Field { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Amanda", CodCentro = 10270000 };
        var lot = new Lot { Id = Guid.NewGuid(), TenantId = tenantId, FieldId = field.Id, Name = "Lote 1" };
        ctx.Fields.Add(field);
        ctx.Lots.Add(lot);

        var urea = new Inventory { Id = Guid.NewGuid(), TenantId = tenantId, ItemName = "Urea", ExternalErpId = "50111", SubGrupoConcepto = "FERTILIZANTE" };
        ctx.Inventories.Add(urea);

        var labores = new Dictionary<string, Guid>();
        foreach (var (nombre, actividad) in new (string, Guid?)[]
                 { ("maiz", maiz.Id), ("soja", soja.Id), ("girasol", girasol.Id), ("sin", null) })
        {
            var labor = new Labor
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                LotId = lot.Id,
                LaborTypeId = laborType.Id,
                ErpActivityId = actividad,
                Status = LaborStatus.Realized,
                ExecutionDate = new DateTime(2026, 2, 21, 0, 0, 0, DateTimeKind.Utc),
                Hectares = 10,
                EffectiveArea = 10
            };
            ctx.Labors.Add(labor);
            ctx.LaborSupplies.Add(new LaborSupply
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                LaborId = labor.Id,
                SupplyId = urea.Id,
                PlannedHectares = 10,
                PlannedDose = 1,
                PlannedTotal = 10
            });
            labores[nombre] = labor.Id;
        }

        ctx.SaveChanges();
        return new Seeded(tenantId, maiz.Id, soja.Id, girasol.Id, labores);
    }

    private async Task<(List<PaseImputacion> Pases, List<string> Warnings)> GenerarAsync(string dbName, Seeded s)
    {
        using var ctx = CreateContext(dbName, s.TenantId);
        var service = new PaseBuilderService(ctx, NullLogger<PaseBuilderService>.Instance);

        var result = await service.GenerarLoteAsync(s.TenantId, null, s.Labores.Values.ToList());
        Assert.True(result.Success, result.Error);

        return (await ctx.PasesImputacion.AsNoTracking().ToListAsync(), result.Warnings.ToList());
    }

    private static PaseImputacion Linea(List<PaseImputacion> pases, Guid laborId, long concepto) =>
        pases.Single(p => p.LaborId == laborId && p.CodConcepto == concepto);

    [Fact]
    public async Task VarianteDeLaActividad_PisaLosPerfilesDeLaRegla()
    {
        var dbName = Guid.NewGuid().ToString();
        var s = Seed(dbName);

        var (pases, _) = await GenerarAsync(dbName, s);

        var labor = Linea(pases, s.Labores["maiz"], 50798);
        Assert.Equal(PerfilDebeMaiz, labor.CodPerfilImputacionDebe);
        Assert.Equal(PerfilHaberMaiz, labor.CodPerfilImputacionHaber);

        var insumo = Linea(pases, s.Labores["maiz"], 50111);
        Assert.Equal(PerfilDebeFertMaiz, insumo.CodPerfilImputacionDebe);
        Assert.Equal(PerfilHaberFertMaiz, insumo.CodPerfilImputacionHaber);
    }

    [Fact]
    public async Task VarianteParcial_TomaDeLaReglaLoQueDejaVacio()
    {
        var dbName = Guid.NewGuid().ToString();
        var s = Seed(dbName);

        var (pases, _) = await GenerarAsync(dbName, s);
        var labor = Linea(pases, s.Labores["soja"], 50798);

        Assert.Equal(PerfilDebeSoja, labor.CodPerfilImputacionDebe);
        Assert.Equal(PerfilHaberGeneral, labor.CodPerfilImputacionHaber);
    }

    [Fact]
    public async Task SinVarianteOSinActividad_SaleConLosDeLaRegla_YAvisa()
    {
        var dbName = Guid.NewGuid().ToString();
        var s = Seed(dbName);

        var (pases, warnings) = await GenerarAsync(dbName, s);

        foreach (var nombre in new[] { "girasol", "sin" })
        {
            var labor = Linea(pases, s.Labores[nombre], 50798);
            Assert.Equal(PerfilDebeGeneral, labor.CodPerfilImputacionDebe);
            Assert.Equal(PerfilHaberGeneral, labor.CodPerfilImputacionHaber);
        }

        Assert.Contains(warnings, w => w.Contains("no tiene perfiles para la actividad Girasol"));
        Assert.Contains(warnings, w => w.Contains("no tiene actividad"));
        // Un aviso por labor y regla, no uno por insumo.
        Assert.Single(warnings, w => w.Contains("no tiene actividad") && w.Contains("'Labor propia'"));
    }

    [Fact]
    public async Task LaborSinActividad_TomaLaDeLaRotacion_OLaProximaSiEsBarbecho()
    {
        var dbName = Guid.NewGuid().ToString();
        var s = Seed(dbName);

        using (var ctx = CreateContext(dbName, s.TenantId))
        {
            var labor = await ctx.Labors.SingleAsync(l => l.Id == s.Labores["sin"]);
            var campaignLot = new CampaignLot { Id = Guid.NewGuid(), TenantId = s.TenantId, CampaignId = Guid.NewGuid(), LotId = labor.LotId };
            // La labor es del 21/02/2026: el maiz arranca despues, asi que es su barbecho. La
            // soja ya termino y no cuenta.
            campaignLot.Rotations.Add(new Rotation
            {
                Id = Guid.NewGuid(), TenantId = s.TenantId, ErpActivityId = s.Soja,
                StartDate = new DateOnly(2025, 11, 1), EndDate = new DateOnly(2026, 2, 1)
            });
            campaignLot.Rotations.Add(new Rotation
            {
                Id = Guid.NewGuid(), TenantId = s.TenantId, ErpActivityId = s.Maiz,
                StartDate = new DateOnly(2026, 3, 1), EndDate = new DateOnly(2026, 8, 30)
            });
            ctx.CampaignLots.Add(campaignLot);
            labor.CampaignLotId = campaignLot.Id;
            await ctx.SaveChangesAsync();
        }

        var (pases, _) = await GenerarAsync(dbName, s);

        var linea = Linea(pases, s.Labores["sin"], 50798);
        Assert.Equal(PerfilDebeMaiz, linea.CodPerfilImputacionDebe);
        Assert.Equal(PerfilHaberMaiz, linea.CodPerfilImputacionHaber);

        using var verif = CreateContext(dbName, s.TenantId);
        Assert.Equal(s.Maiz, (await verif.Labors.AsNoTracking().SingleAsync(l => l.Id == s.Labores["sin"])).ErpActivityId);
    }

    [Fact]
    public async Task Guardar_ReemplazaLasVariantes_YDescartaLasVacias()
    {
        var dbName = Guid.NewGuid().ToString();
        var s = Seed(dbName);

        using (var ctx = CreateContext(dbName, s.TenantId))
        {
            var service = new PaseBuilderService(ctx, NullLogger<PaseBuilderService>.Instance);
            var dto = (await service.GetAccountConfigurationsAsync(s.TenantId))
                .Single(c => c.Description == "Labor propia");
            Assert.Equal(2, dto.Variants.Count);

            dto.Variants = new List<AccountConfigurationVariantDto>
            {
                new() { ErpActivityId = s.Maiz, CodPerfilDebe = 111, CodPerfilHaber = 112 },
                new() { ErpActivityId = s.Girasol, CodPerfilDebe = 400 },
                // Sin perfiles no pisa nada: no se guarda.
                new() { ErpActivityId = s.Soja }
            };
            await service.SaveAccountConfigurationAsync(s.TenantId, dto);
        }

        using (var ctx = CreateContext(dbName, s.TenantId))
        {
            var variantes = await ctx.AccountConfigurationVariants.AsNoTracking().ToListAsync();
            var deLaLabor = variantes.Where(v => v.CodPerfilDebe != PerfilDebeFertMaiz).ToList();

            Assert.Equal(2, deLaLabor.Count);
            Assert.Contains(deLaLabor, v => v.ErpActivityId == s.Maiz && v.CodPerfilDebe == 111 && v.CodPerfilHaber == 112);
            Assert.Contains(deLaLabor, v => v.ErpActivityId == s.Girasol && v.CodPerfilDebe == 400);
            Assert.DoesNotContain(deLaLabor, v => v.ErpActivityId == s.Soja);
        }
    }
}
