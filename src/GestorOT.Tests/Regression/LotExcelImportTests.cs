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

public class LotExcelImportTests
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

    private Stream CreateSampleExcelStream()
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Campos_Lotes_Rotacion");

        string[] headers = ["Campo", "Lote", "Superficie Declarada (ha)", "Cultivo Actual", "Fecha Desde", "Fecha Hasta", "Notas"];
        for (int i = 0; i < headers.Length; i++)
            ws.Cell(1, i + 1).Value = headers[i];

        var rows = new (string Campo, string Lote, decimal Sup, string Cultivo, string Desde, string Hasta, string Notas)[]
        {
            ("Bassi-Prieto", "Bassi", 67, "Maíz tardío", "2026-11-01", "2027-07-01", "Nota 1"),
            ("Bassi-Prieto", "Lobianco", 27, "Maíz tardío", "2026-11-01", "2027-07-01", ""),
            ("Breit", "Breit", 155, "Girasol", "2026-09-01", "2027-05-01", ""),
            ("Corral", "Kiko", 43, "Trigo", "2026-04-01", "2026-12-25", "Lote bajo"),
            ("Daniel Lasca", "Tanque", 50, "Soja 1°", "", "2027-05-01", "Fecha desde inferida")
        };

        for (int r = 0; r < rows.Length; r++)
        {
            var row = rows[r];
            ws.Cell(r + 2, 1).Value = row.Campo;
            ws.Cell(r + 2, 2).Value = row.Lote;
            ws.Cell(r + 2, 3).Value = row.Sup;
            ws.Cell(r + 2, 4).Value = row.Cultivo;
            ws.Cell(r + 2, 5).Value = row.Desde;
            ws.Cell(r + 2, 6).Value = row.Hasta;
            ws.Cell(r + 2, 7).Value = row.Notas;
        }

        var ms = new MemoryStream();
        workbook.SaveAs(ms);
        ms.Position = 0;
        return ms;
    }

    [Fact]
    public async Task PreviewAsync_CorrectlyParsesRowsAndDetectsNewEntities()
    {
        var dbName = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid();
        var campaignId = Guid.NewGuid();

        using (var context = CreateContext(dbName, tenantId))
        {
            context.Campaigns.Add(new Campaign
            {
                Id = campaignId,
                TenantId = tenantId,
                Name = "Campaña 26-27",
                StartDate = new DateOnly(2026, 7, 1),
                EndDate = new DateOnly(2027, 6, 30),
                Status = "Active"
            });

            // Pre-seed an existing field and lot
            var existingField = new Field { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Bassi-Prieto" };
            context.Fields.Add(existingField);
            context.Lots.Add(new Lot { Id = Guid.NewGuid(), TenantId = tenantId, FieldId = existingField.Id, Name = "Bassi" });

            await context.SaveChangesAsync();
        }

        using (var context = CreateContext(dbName, tenantId))
        {
            var service = new LotExcelImportService(context, NullLogger<LotExcelImportService>.Instance);
            using var stream = CreateSampleExcelStream();

            var summary = await service.PreviewAsync(campaignId, stream);

            Assert.Equal(5, summary.TotalRows);
            Assert.Equal(4, summary.ValidRows);
            Assert.Equal(1, summary.WarningRows); // Daniel Lasca had missing Fecha Desde
            Assert.Equal(0, summary.ErrorRows);

            Assert.Equal(3, summary.NewFieldsCount); // Breit, Corral, Daniel Lasca
            Assert.Equal(1, summary.ExistingFieldsCount); // Bassi-Prieto
            Assert.Equal(4, summary.NewLotsCount);
            Assert.Equal(1, summary.ExistingLotsCount); // Bassi in Bassi-Prieto
            Assert.Equal(342m, summary.TotalHectares); // 67 + 27 + 155 + 43 + 50
        }
    }

    [Fact]
    public async Task ExecuteAsync_CreatesEntitiesAndRotationsInCampaign()
    {
        var dbName = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid();
        var campaignId = Guid.NewGuid();

        using (var context = CreateContext(dbName, tenantId))
        {
            context.Campaigns.Add(new Campaign
            {
                Id = campaignId,
                TenantId = tenantId,
                Name = "Campaña 26-27",
                StartDate = new DateOnly(2026, 7, 1),
                EndDate = new DateOnly(2027, 6, 30),
                Status = "Active"
            });
            await context.SaveChangesAsync();
        }

        using (var context = CreateContext(dbName, tenantId))
        {
            var service = new LotExcelImportService(context, NullLogger<LotExcelImportService>.Instance);
            using var stream = CreateSampleExcelStream();

            var result = await service.ExecuteAsync(campaignId, stream);

            Assert.True(result.Success);
            Assert.Equal(4, result.FieldsCreated); // Bassi-Prieto, Breit, Corral, Daniel Lasca
            Assert.Equal(5, result.LotsCreated);
            Assert.Equal(5, result.CampaignLotsLinked);
            Assert.Equal(5, result.RotationsCreated);
            Assert.Equal(342m, result.TotalHectares);
        }

        // Verify entities in DB
        using (var context = CreateContext(dbName, tenantId))
        {
            var fields = await context.Fields.Include(f => f.Lots).ToListAsync();
            Assert.Equal(4, fields.Count);

            var campFields = await context.CampaignFields.Where(cf => cf.CampaignId == campaignId).ToListAsync();
            Assert.Equal(4, campFields.Count);

            var bassiField = fields.First(f => f.Name == "Bassi-Prieto");
            var bassiCf = campFields.First(cf => cf.FieldId == bassiField.Id);
            Assert.Equal(94m, bassiCf.AllocatedHectares); // 67 + 27

            var campLots = await context.CampaignLots.Include(cl => cl.Rotations).Where(cl => cl.CampaignId == campaignId).ToListAsync();
            Assert.Equal(5, campLots.Count);
            Assert.All(campLots, cl => Assert.NotEmpty(cl.Rotations));

            // Verify fallback date for Daniel Lasca
            var danielField = fields.First(f => f.Name == "Daniel Lasca");
            var tanqueLot = danielField.Lots.First(l => l.Name == "Tanque");
            var tanqueCl = campLots.First(cl => cl.LotId == tanqueLot.Id);
            var rotation = tanqueCl.Rotations.First();
            Assert.Equal(new DateOnly(2026, 7, 1), rotation.StartDate); // Campaign.StartDate fallback
        }
    }

    [Fact]
    public async Task ExecuteAsync_WithDuplicateActivitiesInDatabase_SucceedsWithoutArgumentException()
    {
        var dbName = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid();
        var campaignId = Guid.NewGuid();

        using (var context = CreateContext(dbName, tenantId))
        {
            context.Campaigns.Add(new Campaign
            {
                Id = campaignId,
                TenantId = tenantId,
                Name = "Campaña Test",
                StartDate = new DateOnly(2026, 7, 1),
                EndDate = new DateOnly(2027, 6, 30),
                Status = "Active"
            });

            // Simulate duplicate activities from ERP sync (e.g. "Soja 2º" and "soja 2º")
            context.ErpActivities.Add(new ErpActivity { Id = Guid.NewGuid(), Name = "Soja 2º", IsActive = false });
            context.ErpActivities.Add(new ErpActivity { Id = Guid.NewGuid(), Name = "soja 2º", IsActive = true });
            context.ErpActivities.Add(new ErpActivity { Id = Guid.NewGuid(), Name = "Maíz tardío", IsActive = true });

            await context.SaveChangesAsync();
        }

        using (var context = CreateContext(dbName, tenantId))
        {
            var service = new LotExcelImportService(context, NullLogger<LotExcelImportService>.Instance);
            using var fileStream = CreateSampleExcelStream();

            var result = await service.ExecuteAsync(campaignId, fileStream);

            Assert.NotNull(result);
            Assert.True(result.CampaignLotsLinked > 0);
        }
    }

    private Stream CreateExcelWithLoteIdStream(string? loteId, string campo = "Breit", string lote = "Breit", decimal sup = 155)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Campos_Lotes_Rotacion");

        string[] headers = ["lote_id", "Campo", "Lote", "Superficie Declarada (ha)", "Cultivo Actual", "Fecha Desde", "Fecha Hasta", "Notas"];
        for (int i = 0; i < headers.Length; i++)
            ws.Cell(1, i + 1).Value = headers[i];

        if (loteId != null) ws.Cell(2, 1).Value = loteId;
        ws.Cell(2, 2).Value = campo;
        ws.Cell(2, 3).Value = lote;
        ws.Cell(2, 4).Value = sup;
        ws.Cell(2, 5).Value = "Girasol";
        ws.Cell(2, 6).Value = "2026-09-01";
        ws.Cell(2, 7).Value = "2027-05-01";
        ws.Cell(2, 8).Value = "";

        var ms = new MemoryStream();
        workbook.SaveAs(ms);
        ms.Position = 0;
        return ms;
    }

    private async Task<Guid> SeedCampaignAsync(string dbName, Guid tenantId)
    {
        var campaignId = Guid.NewGuid();
        using var context = CreateContext(dbName, tenantId);
        context.Campaigns.Add(new Campaign
        {
            Id = campaignId,
            TenantId = tenantId,
            Name = "AMSA 26-27",
            StartDate = new DateOnly(2026, 7, 1),
            EndDate = new DateOnly(2027, 6, 30),
            Status = "Active"
        });
        await context.SaveChangesAsync();
        return campaignId;
    }

    [Fact]
    public async Task PreviewAsync_ConColumnaLoteId_CuentaLoteId()
    {
        var dbName = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid();
        var campaignId = await SeedCampaignAsync(dbName, tenantId);
        var testId = Guid.NewGuid().ToString();

        using var context = CreateContext(dbName, tenantId);
        var service = new LotExcelImportService(context, NullLogger<LotExcelImportService>.Instance);
        using var stream = CreateExcelWithLoteIdStream(testId);

        var summary = await service.PreviewAsync(campaignId, stream);

        var row = Assert.Single(summary.Rows);
        Assert.Equal(testId, row.ExternalErpId);
        Assert.Equal(1, summary.RowsWithLoteId);
        Assert.Equal(0, summary.ErrorRows);
    }

    [Fact]
    public async Task ExecuteAsync_ConLoteIdGuid_CreaLoteConEseIdExacto()
    {
        var dbName = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid();
        var campaignId = await SeedCampaignAsync(dbName, tenantId);
        var expectedGuid = Guid.Parse("d8689ff3-c82d-44aa-9c70-ea8d73b06214");

        using (var context = CreateContext(dbName, tenantId))
        {
            var service = new LotExcelImportService(context, NullLogger<LotExcelImportService>.Instance);
            using var stream = CreateExcelWithLoteIdStream(expectedGuid.ToString());

            var result = await service.ExecuteAsync(campaignId, stream);

            Assert.True(result.Success);
            Assert.Equal(1, result.LotsCreated);
            Assert.Equal(1, result.LotIdsAssigned);
        }

        using (var context = CreateContext(dbName, tenantId))
        {
            var lot = await context.Lots.FirstAsync();
            Assert.Equal(expectedGuid, lot.Id);
            Assert.Equal(expectedGuid.ToString(), lot.ExternalErpId);
        }
    }

    [Fact]
    public async Task ExecuteAsync_ConLoteIdExistente_ActualizaLoteSinDuplicar()
    {
        var dbName = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid();
        var campaignId = await SeedCampaignAsync(dbName, tenantId);
        var existingLotId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        var fieldId = Guid.NewGuid();

        using (var context = CreateContext(dbName, tenantId))
        {
            var field = new Field { Id = fieldId, TenantId = tenantId, Name = "Breit" };
            context.Fields.Add(field);
            context.Lots.Add(new Lot
            {
                Id = existingLotId,
                TenantId = tenantId,
                FieldId = fieldId,
                Name = "Breit Viejo",
                ExternalErpId = existingLotId.ToString(),
                CadastralArea = 100
            });
            await context.SaveChangesAsync();
        }

        using (var context = CreateContext(dbName, tenantId))
        {
            var service = new LotExcelImportService(context, NullLogger<LotExcelImportService>.Instance);
            using var stream = CreateExcelWithLoteIdStream(existingLotId.ToString(), campo: "Breit", lote: "Breit Nuevo", sup: 120);

            var result = await service.ExecuteAsync(campaignId, stream);

            Assert.True(result.Success);
            Assert.Equal(0, result.LotsCreated);
            Assert.Equal(1, result.LotsRenamed);
        }

        using (var context = CreateContext(dbName, tenantId))
        {
            var lots = await context.Lots.ToListAsync();
            var lot = Assert.Single(lots);
            Assert.Equal(existingLotId, lot.Id);
            Assert.Equal("Breit Nuevo", lot.Name);
        }
    }
}

