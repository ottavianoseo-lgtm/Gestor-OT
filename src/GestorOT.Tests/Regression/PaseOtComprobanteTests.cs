using ClosedXML.Excel;
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
/// Para el G4 la OT es el comprobante. Como en la planilla con la que importan hoy:
///   - las labores de una OT (una por lote) van en un mismo pase (idAgrupacionPase),
///   - numeroComprobante es el numero de la OT ("00000569"),
///   - puntoVenta es la campania (25/26 -> "02526").
/// Antes el pase se abria por labor y esos dos campos salian con el valor fijo de la regla.
/// </summary>
public class PaseOtComprobanteTests
{
    private const int PuntoVentaDeLaRegla = 7;

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

    private record Seeded(
        Guid TenantId,
        Guid Ot569,
        Guid Ot570,
        Guid OtSinNumero,
        Guid LaborSuelta,
        Guid[] LaboresOt569,
        Guid LaborOt569OtraFecha);

    private Seeded Seed(string dbName)
    {
        var tenantId = Guid.NewGuid();
        using var ctx = CreateContext(dbName, tenantId);

        var laborType = new LaborType { Id = Guid.NewGuid(), TenantId = tenantId, Name = "SIEMBRA GIRASOL", ExternalErpId = "50798" };
        ctx.LaborTypes.Add(laborType);

        ctx.AccountConfigurations.Add(new AccountConfiguration
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CodEmpresa = 4,
            CodComprobante = 1080,
            CodMoneda = 1,
            PuntoVenta = PuntoVentaDeLaRegla,
            NoImputaCentro = true
        });

        var campaign = new Campaign
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Gruesa 25/26",
            StartDate = new DateOnly(2025, 7, 1),
            EndDate = new DateOnly(2026, 6, 30)
        };
        ctx.Campaigns.Add(campaign);

        var field = new Field { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Amanda" };
        ctx.Fields.Add(field);

        WorkOrder NuevaOt(string numero) => new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            OTNumber = numero,
            CampaignId = campaign.Id
        };

        var ot569 = NuevaOt("569");
        var ot570 = NuevaOt("OT-570");
        var otSinNumero = NuevaOt("sin numero");
        ctx.WorkOrders.AddRange(ot569, ot570, otSinNumero);

        var dia = new DateTime(2026, 2, 21, 0, 0, 0, DateTimeKind.Utc);

        Labor NuevaLabor(Guid? workOrderId, DateTime fecha)
        {
            var lot = new Lot { Id = Guid.NewGuid(), TenantId = tenantId, FieldId = field.Id, Name = $"Lote {Guid.NewGuid():N}"[..10] };
            ctx.Lots.Add(lot);
            return new Labor
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                WorkOrderId = workOrderId,
                LotId = lot.Id,
                LaborTypeId = laborType.Id,
                Status = LaborStatus.Realized,
                ExecutionDate = fecha,
                Hectares = 28,
                EffectiveArea = 28,
                Rate = 12537
            };
        }

        var laboresOt569 = new[] { NuevaLabor(ot569.Id, dia), NuevaLabor(ot569.Id, dia), NuevaLabor(ot569.Id, dia) };
        var otraFecha = NuevaLabor(ot569.Id, dia.AddDays(1));
        var laborOt570 = NuevaLabor(ot570.Id, dia);
        var laborSinNumero = NuevaLabor(otSinNumero.Id, dia);
        var suelta = NuevaLabor(null, dia);

        ctx.Labors.AddRange(laboresOt569);
        ctx.Labors.AddRange(otraFecha, laborOt570, laborSinNumero, suelta);
        ctx.SaveChanges();

        return new Seeded(
            tenantId, ot569.Id, ot570.Id, otSinNumero.Id, suelta.Id,
            laboresOt569.Select(l => l.Id).ToArray(), otraFecha.Id);
    }

    private async Task<(Guid LoteId, List<PaseImputacion> Pases, List<string> Warnings)> GenerarAsync(string dbName, Seeded s)
    {
        using var ctx = CreateContext(dbName, s.TenantId);
        var service = new PaseBuilderService(ctx, NullLogger<PaseBuilderService>.Instance);

        var result = await service.GenerarLoteAsync(
            s.TenantId,
            workOrderIds: new List<Guid> { s.Ot569, s.Ot570, s.OtSinNumero },
            laborIds: new List<Guid> { s.LaborSuelta });

        Assert.True(result.Success, result.Error);

        var pases = await ctx.PasesImputacion.AsNoTracking().ToListAsync();
        return (result.LoteId, pases, result.Warnings.ToList());
    }

    [Fact]
    public async Task LasLaboresDeUnaOt_ConLaMismaFecha_VanEnUnMismoPase()
    {
        var dbName = Guid.NewGuid().ToString();
        var s = Seed(dbName);

        var (_, pases, _) = await GenerarAsync(dbName, s);

        var grupos = pases.Where(p => s.LaboresOt569.Contains(p.LaborId!.Value))
            .Select(p => p.IdAgrupacionPase).Distinct().ToList();
        Assert.Single(grupos);
    }

    [Fact]
    public async Task CadaOt_YCadaFechaDistinta_AbrenOtroPase()
    {
        var dbName = Guid.NewGuid().ToString();
        var s = Seed(dbName);

        var (_, pases, _) = await GenerarAsync(dbName, s);

        var grupoOt569 = pases.First(p => p.LaborId == s.LaboresOt569[0]).IdAgrupacionPase;
        var grupoOtraFecha = pases.Single(p => p.LaborId == s.LaborOt569OtraFecha).IdAgrupacionPase;
        var grupoOt570 = pases.Single(p => p.WorkOrderId == s.Ot570).IdAgrupacionPase;
        var grupoSuelta = pases.Single(p => p.LaborId == s.LaborSuelta).IdAgrupacionPase;

        Assert.Equal(4, new[] { grupoOt569, grupoOtraFecha, grupoOt570, grupoSuelta }.Distinct().Count());
    }

    [Fact]
    public async Task NumeroComprobante_EsElNumeroDeLaOt()
    {
        var dbName = Guid.NewGuid().ToString();
        var s = Seed(dbName);

        var (_, pases, warnings) = await GenerarAsync(dbName, s);

        Assert.All(pases.Where(p => p.WorkOrderId == s.Ot569), p => Assert.Equal(569, p.NumeroComprobante));
        Assert.Equal(570, pases.Single(p => p.WorkOrderId == s.Ot570).NumeroComprobante);
        Assert.Null(pases.Single(p => p.LaborId == s.LaborSuelta).NumeroComprobante);

        Assert.Null(pases.Single(p => p.WorkOrderId == s.OtSinNumero).NumeroComprobante);
        Assert.Contains(warnings, w => w.Contains("sin numero"));
    }

    [Fact]
    public async Task PuntoVenta_EsLaCampania_YSinCampaniaQuedaElDeLaRegla()
    {
        var dbName = Guid.NewGuid().ToString();
        var s = Seed(dbName);

        var (_, pases, _) = await GenerarAsync(dbName, s);

        Assert.All(pases.Where(p => p.WorkOrderId != null), p => Assert.Equal(2526, p.PuntoVenta));
        Assert.Equal(PuntoVentaDeLaRegla, pases.Single(p => p.LaborId == s.LaborSuelta).PuntoVenta);
    }

    [Fact]
    public async Task Xlsx_SacaPuntoVentaYNumeroComprobante_ComoTextoConCeros()
    {
        var dbName = Guid.NewGuid().ToString();
        var s = Seed(dbName);
        var (loteId, _, _) = await GenerarAsync(dbName, s);

        using var ctx = CreateContext(dbName, s.TenantId);
        var export = new PaseXlsxExportService(ctx, NullLogger<PaseXlsxExportService>.Instance);
        var (stream, _) = await export.ExportLoteAsync(s.TenantId, loteId);

        using var wb = new XLWorkbook(stream);
        var ws = wb.Worksheet(1);
        var fila = ws.RowsUsed().Skip(2).First(r => r.Cell(2).GetString() == "569");

        Assert.Equal("02526", fila.Cell(10).GetString());
        Assert.Equal("00000569", fila.Cell(11).GetString());
    }
}
