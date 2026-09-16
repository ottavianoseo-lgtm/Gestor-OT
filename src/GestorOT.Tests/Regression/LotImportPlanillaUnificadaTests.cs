using ClosedXML.Excel;
using GestorOT.Domain.Entities;
using GestorOT.Infrastructure.Data;
using GestorOT.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using System.Security.Claims;
using Xunit;

namespace GestorOT.Tests.Regression;

/// <summary>
/// La planilla unificada que manda el cliente trae tres cosas que el importador original no
/// contemplaba y que dejaban afuera la mayor parte del archivo:
///
///   1. El GIS sale de shapefile con cada anillo como polígono suelto del MultiPolygon, así que
///      los agujeros llegan como shells anidados y OGC los marca inválidos. Antes eran filas en
///      Error y el botón Importar quedaba deshabilitado.
///   2. Trae una columna 'Centro ERP' constante por campo, que es el centro de costo al que
///      imputan los pases G4. Antes se ignoraba y había que cargarlo a mano lote por lote.
///   3. Un lote con doble cultivo ocupa dos filas con la misma superficie: sumar por fila
///      inflaba el total de hectáreas.
/// </summary>
public class LotImportPlanillaUnificadaTests
{
    private const long CentroDeCiervoBlanco = 10120000;

    /// <summary>Cuadrado de 1x1 con un hueco de 0,2x0,2 escrito como polígono suelto.</summary>
    private const string GisConAgujeroMalExportado =
        "{\"type\":\"MultiPolygon\",\"coordinates\":["
        + "[[[-59.60,-37.30],[-59.59,-37.30],[-59.59,-37.29],[-59.60,-37.29],[-59.60,-37.30]]],"
        + "[[[-59.596,-37.296],[-59.594,-37.296],[-59.594,-37.294],[-59.596,-37.294],[-59.596,-37.296]]]"
        + "]}";

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

        var accessor = new HttpContextAccessor { HttpContext = httpContext };
        return new ApplicationDbContext(options, accessor);
    }

    private async Task<Guid> SeedCampaignAsync(string dbName, Guid tenantId)
    {
        var campaignId = Guid.NewGuid();
        using var context = CreateContext(dbName, tenantId);
        context.Campaigns.Add(new Campaign
        {
            Id = campaignId,
            TenantId = tenantId,
            Name = "AMAND 26-27",
            StartDate = new DateOnly(2026, 7, 1),
            EndDate = new DateOnly(2027, 6, 30),
            Status = "Active"
        });
        await context.SaveChangesAsync();
        return campaignId;
    }

    /// <summary>
    /// Réplica de la planilla real: las columnas de apoyo del GIS y el Centro ERP van después
    /// de la geometría, y el lote 11L aparece dos veces por doble cultivo.
    /// </summary>
    private Stream CreatePlanillaUnificadaStream()
    {
        using var workbook = new XLWorkbook();

        // La planilla real trae la hoja de instrucciones primero: el importador tiene que
        // encontrar igual la de datos por nombre.
        workbook.Worksheets.Add("Instrucciones").Cell(1, 2).Value = "Planilla unificada";

        var ws = workbook.Worksheets.Add("Campos_Lotes_Rotacion");

        string[] headers =
        [
            "Campo", "Lote", "Superficie Declarada (ha)", "Cultivo Actual", "Fecha Desde",
            "Fecha Hasta", "Notas", "GIS (GeoJSON)", "GIS - Nombre origen", "GIS - Superficie (ha)",
            "Ciclo", "Centro ERP", "Estado"
        ];
        for (int i = 0; i < headers.Length; i++)
            ws.Cell(1, i + 1).Value = headers[i];

        var filas = new (string Lote, decimal Sup, string Cultivo, string Desde, string Hasta, string Gis, string Ciclo)[]
        {
            ("11L", 44, "Cebada", "2026-06-01", "2026-12-20", GisConAgujeroMalExportado, "Fina"),
            ("11L", 44, "Soja 2", "2026-12-20", "2027-05-15", GisConAgujeroMalExportado, "Gruesa 2da"),
            ("Cerro1", 12, "", "", "", "", "")
        };

        for (int r = 0; r < filas.Length; r++)
        {
            var f = filas[r];
            ws.Cell(r + 2, 1).Value = "Ciervo Blanco";
            ws.Cell(r + 2, 2).Value = f.Lote;
            ws.Cell(r + 2, 3).Value = f.Sup;
            ws.Cell(r + 2, 4).Value = f.Cultivo;
            ws.Cell(r + 2, 5).Value = f.Desde;
            ws.Cell(r + 2, 6).Value = f.Hasta;
            ws.Cell(r + 2, 8).Value = f.Gis;
            ws.Cell(r + 2, 9).Value = f.Lote;
            ws.Cell(r + 2, 10).Value = 45.24;
            ws.Cell(r + 2, 11).Value = f.Ciclo;
            ws.Cell(r + 2, 12).Value = CentroDeCiervoBlanco;
            ws.Cell(r + 2, 13).Value = "OK";
        }

        var ms = new MemoryStream();
        workbook.SaveAs(ms);
        ms.Position = 0;
        return ms;
    }

    [Fact]
    public async Task PreviewAsync_GeometriaMalExportada_EntraComoAdvertenciaYNoComoError()
    {
        var dbName = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid();
        var campaignId = await SeedCampaignAsync(dbName, tenantId);

        using var context = CreateContext(dbName, tenantId);
        var service = new LotExcelImportService(context, NullLogger<LotExcelImportService>.Instance);
        using var stream = CreatePlanillaUnificadaStream();

        var summary = await service.PreviewAsync(campaignId, stream);

        // La regresión: con la geometría rechazada estas filas quedaban en Error y el modal
        // deshabilitaba el botón Importar.
        Assert.Equal(0, summary.ErrorRows);
        Assert.Equal(2, summary.GeometryRows);
        Assert.Equal(2, summary.RepairedGeometryRows);

        var conGis = summary.Rows.Where(r => r.HasGeometry).ToList();
        Assert.All(conGis, r => Assert.Equal("Warning", r.Status));
        Assert.All(conGis, r => Assert.Contains("normalizó", r.ValidationMessage!));
    }

    [Fact]
    public async Task PreviewAsync_SumaHectareasPorLoteYNoPorFila()
    {
        var dbName = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid();
        var campaignId = await SeedCampaignAsync(dbName, tenantId);

        using var context = CreateContext(dbName, tenantId);
        var service = new LotExcelImportService(context, NullLogger<LotExcelImportService>.Instance);
        using var stream = CreatePlanillaUnificadaStream();

        var summary = await service.PreviewAsync(campaignId, stream);

        Assert.Equal(3, summary.TotalRows);
        Assert.Equal(2, summary.NewLotsCount);
        // 44 (11L, aunque ocupe dos filas) + 12 (Cerro1). Por fila daba 100.
        Assert.Equal(56m, summary.TotalHectares);
        Assert.Equal(1, summary.FieldsWithCodCentro);
    }

    [Fact]
    public async Task ExecuteAsync_AsignaElCentroErpAlCampoYGuardaLaGeometriaNormalizada()
    {
        var dbName = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid();
        var campaignId = await SeedCampaignAsync(dbName, tenantId);

        using (var context = CreateContext(dbName, tenantId))
        {
            var service = new LotExcelImportService(context, NullLogger<LotExcelImportService>.Instance);
            using var stream = CreatePlanillaUnificadaStream();

            var result = await service.ExecuteAsync(campaignId, stream);

            Assert.True(result.Success);
            Assert.Equal(1, result.FieldsCreated);
            Assert.Equal(2, result.LotsCreated);
            Assert.Equal(1, result.CodCentrosAssigned);
            // 11L sale en dos filas con la misma superficie: son 44, no 88.
            Assert.Equal(56m, result.TotalHectares);
        }

        using (var context = CreateContext(dbName, tenantId))
        {
            var campo = await context.Fields.SingleAsync(f => f.Name == "Ciervo Blanco");
            Assert.Equal(CentroDeCiervoBlanco, campo.CodCentro);

            var lote = await context.Lots.SingleAsync(l => l.Name == "11L");
            Assert.NotNull(lote.Geometry);
            Assert.True(lote.Geometry!.IsValid);
            Assert.Equal(4326, lote.Geometry.SRID);

            // Cuadrado de 0,01 x 0,01 menos el hueco de 0,002 x 0,002. Si se hubiera resuelto
            // por unión en vez de por agujero, el área daría 0,0001: el cuadrado entero.
            Assert.Equal(0.000096, lote.Geometry.Area, 12);

            // Doble cultivo: dos rotaciones sobre el mismo CampaignLot.
            var campLot = await context.CampaignLots
                .Include(cl => cl.Rotations)
                .SingleAsync(cl => cl.LotId == lote.Id);
            Assert.Equal(2, campLot.Rotations.Count);
            Assert.Equal(44m, campLot.ProductiveArea);
        }
    }

    [Fact]
    public async Task ExecuteAsync_ReimportarConOtroCentro_PisaElDelCampo()
    {
        var dbName = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid();
        var campaignId = await SeedCampaignAsync(dbName, tenantId);

        using (var context = CreateContext(dbName, tenantId))
        {
            context.Fields.Add(new Field
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = "Ciervo Blanco",
                CreatedAt = DateTime.UtcNow,
                CodCentro = 99999999
            });
            await context.SaveChangesAsync();
        }

        using (var context = CreateContext(dbName, tenantId))
        {
            var service = new LotExcelImportService(context, NullLogger<LotExcelImportService>.Instance);
            using var stream = CreatePlanillaUnificadaStream();

            var result = await service.ExecuteAsync(campaignId, stream);

            Assert.Equal(0, result.FieldsCreated);
            Assert.Equal(1, result.CodCentrosAssigned);
        }

        using (var context = CreateContext(dbName, tenantId))
        {
            var campo = await context.Fields.SingleAsync(f => f.Name == "Ciervo Blanco");
            Assert.Equal(CentroDeCiervoBlanco, campo.CodCentro);
        }
    }
}
