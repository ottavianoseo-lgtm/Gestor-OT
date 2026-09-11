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
/// Que regla contable le toca a cada labor. Una regla declara hasta tres dimensiones opcionales
/// (tipo de labor, actividad del ERP, propia/contratista) y aplica si todas las que declara
/// coinciden; gana la que declara más. Antes era un FirstOrDefault() sobre las del tipo, que con
/// más de una regla elegía arbitrariamente y sin avisar.
/// </summary>
public class PaseConfigResolutionTests
{
    private ApplicationDbContext CreateContext(string dbName, Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("tenant_id", tenantId.ToString()),
            new Claim(ClaimTypes.Role, "Admin")
        }, "TestAuth"));

        return new ApplicationDbContext(options, new HttpContextAccessor { HttpContext = httpContext });
    }

    private sealed class Escenario
    {
        public required string DbName { get; init; }
        public required Guid TenantId { get; init; }
        public required Guid LaborId { get; init; }
    }

    /// <summary>
    /// Arma una labor COSECHA sobre la actividad SOJA, propia o de contratista, junto con las
    /// reglas que se le pasen. Cada regla es (descripcion, tipo?, actividad?, modo?, puntoVenta)
    /// y el puntoVenta se usa como marca para saber cuál ganó.
    /// </summary>
    private Escenario Armar(
        bool esContratista,
        bool laborConActividad,
        params (string Desc, bool ConTipo, bool ConActividad, LaborExecutionMode? Modo, int Marca)[] reglas)
    {
        var dbName = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid();

        using var ctx = CreateContext(dbName, tenantId);

        var laborType = new LaborType { Id = Guid.NewGuid(), TenantId = tenantId, Name = "COSECHA", ExternalErpId = "50956" };
        ctx.LaborTypes.Add(laborType);

        var actividad = new ErpActivity { Id = Guid.NewGuid(), TenantId = tenantId, Name = "SOJA 1RA", ExternalErpId = "77" };
        ctx.ErpActivities.Add(actividad);

        foreach (var (desc, conTipo, conActividad, modo, marca) in reglas)
        {
            ctx.AccountConfigurations.Add(new AccountConfiguration
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Description = desc,
                LaborTypeId = conTipo ? laborType.Id : null,
                ErpActivityId = conActividad ? actividad.Id : null,
                ExecutionMode = modo,
                CodEmpresa = 1,
                CodComprobante = 1,
                CodMoneda = 1,
                PuntoVenta = marca
            });
        }

        var field = new Field { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Campo" };
        ctx.Fields.Add(field);
        var lot = new Lot { Id = Guid.NewGuid(), TenantId = tenantId, FieldId = field.Id, Name = "Lote 1" };
        ctx.Lots.Add(lot);

        var labor = new Labor
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            LotId = lot.Id,
            LaborTypeId = laborType.Id,
            ErpActivityId = laborConActividad ? actividad.Id : null,
            IsExternalBilling = esContratista,
            Status = LaborStatus.Realized,
            ExecutionDate = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc),
            Hectares = 10,
            EffectiveArea = 10,
            Rate = 1
        };
        ctx.Labors.Add(labor);
        ctx.SaveChanges();

        return new Escenario { DbName = dbName, TenantId = tenantId, LaborId = labor.Id };
    }

    /// <summary>Devuelve la marca (PuntoVenta) de la regla que terminó aplicándose.</summary>
    private async Task<(int Marca, List<string> Warnings)> ReglaAplicadaAsync(Escenario e)
    {
        using var ctx = CreateContext(e.DbName, e.TenantId);
        var service = new PaseBuilderService(ctx, NullLogger<PaseBuilderService>.Instance);

        var result = await service.GenerarLoteAsync(
            e.TenantId, null, new List<Guid> { e.LaborId }, "test reglas");

        Assert.True(result.Success, result.Error);

        var pase = await ctx.PasesImputacion.AsNoTracking().FirstAsync(p => p.LaborId == e.LaborId);
        return (pase.PuntoVenta, result.Warnings);
    }

    [Fact]
    public async Task GanaLaReglaDeTipoMasActividad_SobreLaDeSoloTipo()
    {
        var e = Armar(esContratista: false, laborConActividad: true,
            ("solo tipo", true, false, null, 10),
            ("tipo + actividad", true, true, null, 20));

        var (marca, _) = await ReglaAplicadaAsync(e);

        Assert.Equal(20, marca);
    }

    [Fact]
    public async Task GanaLaReglaDeTipoMasActividadMasModo_SobreLaDeTipoMasActividad()
    {
        var e = Armar(esContratista: true, laborConActividad: true,
            ("tipo + actividad", true, true, null, 20),
            ("tipo + actividad + contratista", true, true, LaborExecutionMode.Contractor, 30));

        var (marca, _) = await ReglaAplicadaAsync(e);

        Assert.Equal(30, marca);
    }

    [Fact]
    public async Task NoAplicaLaReglaDeOtroModo()
    {
        // La labor es propia: la regla de contratista no puede ganar aunque sea más específica.
        var e = Armar(esContratista: false, laborConActividad: true,
            ("solo tipo", true, false, null, 10),
            ("tipo + actividad + contratista", true, true, LaborExecutionMode.Contractor, 30));

        var (marca, _) = await ReglaAplicadaAsync(e);

        Assert.Equal(10, marca);
    }

    [Fact]
    public async Task NoAplicaLaReglaDeActividad_SiLaLaborNoTieneEsaActividad()
    {
        var e = Armar(esContratista: false, laborConActividad: false,
            ("general", false, false, null, 1),
            ("tipo + actividad", true, true, null, 20));

        var (marca, _) = await ReglaAplicadaAsync(e);

        Assert.Equal(1, marca);
    }

    [Fact]
    public async Task ReglaPorActividadSola_AplicaSinImportarLaTarea()
    {
        // Es la combinación que una escalera de casos fijos no cubriría.
        var e = Armar(esContratista: false, laborConActividad: true,
            ("general", false, false, null, 1),
            ("solo actividad", false, true, null, 40));

        var (marca, _) = await ReglaAplicadaAsync(e);

        Assert.Equal(40, marca);
    }

    [Fact]
    public async Task CaeEnLaGeneral_CuandoNingunaOtraAplica()
    {
        var e = Armar(esContratista: false, laborConActividad: true,
            ("general", false, false, null, 1));

        var (marca, _) = await ReglaAplicadaAsync(e);

        Assert.Equal(1, marca);
    }

    [Fact]
    public async Task Avisa_CuandoDosReglasEmpatanEnEspecificidad()
    {
        var e = Armar(esContratista: false, laborConActividad: true,
            ("primera", true, false, null, 10),
            ("segunda", true, false, null, 11));

        var (_, warnings) = await ReglaAplicadaAsync(e);

        Assert.Contains(warnings, w => w.Contains("igual de especificas", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(warnings, w => w.Contains("primera") && w.Contains("segunda"));
    }

    [Fact]
    public async Task ElOverrideDeLaLabor_GanaSobreTodaLaJerarquia()
    {
        var e = Armar(esContratista: false, laborConActividad: true,
            ("general", false, false, null, 1),
            ("tipo + actividad", true, true, null, 20));

        // Se apunta la labor a la regla general, que es la menos específica de todas.
        using (var ctx = CreateContext(e.DbName, e.TenantId))
        {
            var general = await ctx.AccountConfigurations.FirstAsync(c => c.PuntoVenta == 1);
            var labor = await ctx.Labors.FirstAsync(l => l.Id == e.LaborId);
            labor.AccountConfigurationId = general.Id;
            await ctx.SaveChangesAsync();
        }

        var (marca, _) = await ReglaAplicadaAsync(e);

        Assert.Equal(1, marca);
    }
}
