using ClosedXML.Excel;
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

public class LaborExcelImportTests
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

    private Stream CreateSampleStandardExcelStream()
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Labores e Insumos");

        string[] headers = ["Fecha", "Establecimiento", "Lote", "Superficie (ha)", "Tipo", "Labor o Insumo", "Dosis", "Unidad", "Contratista", "Modo", "Notas"];
        for (int i = 0; i < headers.Length; i++)
            ws.Cell(1, i + 1).Value = headers[i];

        var rows = new object[][]
        {
            // Labor 1 (Past date -> Realized)
            new object[] { "2026-01-10", "Establecimiento Norte", "Lote 1", 50, "Labor", "Pulverización", 1, "ha", "Propio", "", "Barbecho" },
            new object[] { "2026-01-10", "Establecimiento Norte", "Lote 1", 50, "Herbicida", "Glifosato 66%", 2.5, "litros", "Propio", "", "" },
            new object[] { "2026-01-10", "Establecimiento Norte", "Lote 1", 50, "Coadyuvante", "Aceite Mineral", 0.5, "litros", "Propio", "", "" },
            // Labor 2 (Future date -> Planned)
            new object[] { "2026-12-12", "Establecimiento Norte", "Lote 2", 80, "Labor", "Fertilización", 1, "ha", "Don Carlos", "", "Voleo" },
            new object[] { "2026-12-12", "Establecimiento Norte", "Lote 2", 80, "Fertilizante", "Urea Granulada", 120, "kg", "Don Carlos", "", "" }
        };

        for (int r = 0; r < rows.Length; r++)
        {
            for (int c = 0; c < rows[r].Length; c++)
            {
                var cell = ws.Cell(r + 2, c + 1);
                var val = rows[r][c];
                if (val is int iVal) cell.Value = iVal;
                else if (val is double dVal) cell.Value = dVal;
                else cell.Value = val.ToString();
            }
        }

        var ms = new MemoryStream();
        workbook.SaveAs(ms);
        ms.Position = 0;
        return ms;
    }

    [Fact]
    public async Task GenerateTemplateAsync_CreatesValidWorkbookWithStandardColumns()
    {
        var dbName = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid();
        using var context = CreateContext(dbName, tenantId);
        var service = new LaborExcelImportService(context, NullLogger<LaborExcelImportService>.Instance);

        var (bytes, fileName) = await service.GenerateTemplateAsync();

        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 0);
        Assert.EndsWith(".xlsx", fileName);

        using var ms = new MemoryStream(bytes);
        using var wb = new XLWorkbook(ms);
        var ws = wb.Worksheet("Labores e Insumos");
        Assert.NotNull(ws);
        Assert.Equal("Fecha", ws.Cell(1, 1).GetString());
        Assert.Equal("Lote", ws.Cell(1, 3).GetString());
        Assert.Equal("Tipo", ws.Cell(1, 5).GetString());
        Assert.Equal("Labor o Insumo", ws.Cell(1, 6).GetString());
    }

    [Fact]
    public async Task PreviewAsync_GroupsLaborsAndSuppliesSequentially()
    {
        var dbName = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid();
        var campaignId = Guid.NewGuid();

        using (var context = CreateContext(dbName, tenantId))
        {
            var campaign = new Campaign { Id = campaignId, TenantId = tenantId, Name = "2026-2027", IsActive = true };
            var field = new Field { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Establecimiento Norte" };
            var lot1 = new Lot { Id = Guid.NewGuid(), TenantId = tenantId, FieldId = field.Id, Name = "Lote 1" };
            var lot2 = new Lot { Id = Guid.NewGuid(), TenantId = tenantId, FieldId = field.Id, Name = "Lote 2" };

            var cl1 = new CampaignLot { Id = Guid.NewGuid(), TenantId = tenantId, CampaignId = campaignId, LotId = lot1.Id };
            var cl2 = new CampaignLot { Id = Guid.NewGuid(), TenantId = tenantId, CampaignId = campaignId, LotId = lot2.Id };

            // Seed inventory
            var inv1 = new Inventory { Id = Guid.NewGuid(), TenantId = tenantId, ItemName = "Glifosato 66%", Category = "Herbicida", Unit = "litros" };
            var inv2 = new Inventory { Id = Guid.NewGuid(), TenantId = tenantId, ItemName = "Urea Granulada", Category = "Fertilizante", Unit = "kg" };

            context.Campaigns.Add(campaign);
            context.Fields.Add(field);
            context.Lots.AddRange(lot1, lot2);
            context.CampaignLots.AddRange(cl1, cl2);
            context.Inventories.AddRange(inv1, inv2);
            await context.SaveChangesAsync();
        }

        using (var context = CreateContext(dbName, tenantId))
        {
            var service = new LaborExcelImportService(context, NullLogger<LaborExcelImportService>.Instance);
            using var stream = CreateSampleStandardExcelStream();

            var preview = await service.PreviewAsync(campaignId, stream);

            Assert.NotNull(preview);
            Assert.Equal(2, preview.TotalLabors);
            Assert.Equal(3, preview.TotalSupplies);
            Assert.Equal(130, preview.TotalHectares); // 50 + 80

            // Labor 1 verification
            var labor1 = preview.Labors[0];
            Assert.Equal("Lote 1", labor1.LotName);
            Assert.Equal("Pulverización", labor1.LaborTypeName);
            Assert.Equal(50, labor1.Hectares);
            Assert.Equal("Realized", labor1.Mode);
            Assert.Equal(2, labor1.Supplies.Count);
            Assert.Equal("Glifosato 66%", labor1.Supplies[0].SupplyName);
            Assert.Equal(2.5m, labor1.Supplies[0].Dose);

            // Supply mapping verification
            var glifoMap = preview.SupplyMappings.FirstOrDefault(m => m.RawName == "Glifosato 66%");
            Assert.NotNull(glifoMap);
            Assert.Equal(1.0, glifoMap.Confidence);
            Assert.Equal("High", glifoMap.ConfidenceLevel);

            // Unknown supply should be marked CreateNew
            var aceiteMap = preview.SupplyMappings.FirstOrDefault(m => m.RawName == "Aceite Mineral");
            Assert.NotNull(aceiteMap);
            Assert.Equal("None", aceiteMap.ConfidenceLevel);
            Assert.Equal("CreateNew", aceiteMap.Action);
        }
    }

    [Fact]
    public async Task ExecuteAsync_PersistsLaborsSuppliesAndLearnsAliases()
    {
        var dbName = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid();
        var campaignId = Guid.NewGuid();

        using (var context = CreateContext(dbName, tenantId))
        {
            var campaign = new Campaign { Id = campaignId, TenantId = tenantId, Name = "2026-2027", IsActive = true };
            var field = new Field { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Establecimiento Norte" };
            var lot1 = new Lot { Id = Guid.NewGuid(), TenantId = tenantId, FieldId = field.Id, Name = "Lote 1" };
            var lot2 = new Lot { Id = Guid.NewGuid(), TenantId = tenantId, FieldId = field.Id, Name = "Lote 2" };

            var cl1 = new CampaignLot { Id = Guid.NewGuid(), TenantId = tenantId, CampaignId = campaignId, LotId = lot1.Id };
            var cl2 = new CampaignLot { Id = Guid.NewGuid(), TenantId = tenantId, CampaignId = campaignId, LotId = lot2.Id };

            var inv1 = new Inventory { Id = Guid.NewGuid(), TenantId = tenantId, ItemName = "Glifosato 66%", Category = "Herbicida", Unit = "litros" };
            var inv2 = new Inventory { Id = Guid.NewGuid(), TenantId = tenantId, ItemName = "Urea Granulada", Category = "Fertilizante", Unit = "kg" };

            context.Campaigns.Add(campaign);
            context.Fields.Add(field);
            context.Lots.AddRange(lot1, lot2);
            context.CampaignLots.AddRange(cl1, cl2);
            context.Inventories.AddRange(inv1, inv2);
            await context.SaveChangesAsync();
        }

        using (var context = CreateContext(dbName, tenantId))
        {
            var service = new LaborExcelImportService(context, NullLogger<LaborExcelImportService>.Instance);
            using var stream = CreateSampleStandardExcelStream();
            var preview = await service.PreviewAsync(campaignId, stream);

            // Execute import with preview mappings
            stream.Position = 0;
            var result = await service.ExecuteAsync(campaignId, stream, preview.SupplyMappings);

            Assert.NotNull(result);
            Assert.True(result.Success);
            Assert.Equal(2, result.LaborsCreated);
            Assert.Equal(3, result.SuppliesCreated);
            Assert.Equal(1, result.NewSuppliesCreated); // Aceite Mineral was created

            // Verify in DB
            var dbLabors = await context.Labors.Include(l => l.Supplies).OrderBy(l => l.Hectares).ToListAsync();
            Assert.Equal(2, dbLabors.Count);

            // Labor 1 (Past date -> Realized)
            var l1 = dbLabors[0];
            Assert.Equal(LaborMode.Realized, l1.Mode);
            Assert.Equal(LaborStatus.Realized, l1.Status);
            Assert.Equal(1, l1.RealizedDose);
            Assert.All(l1.Supplies, s => Assert.NotNull(s.RealDose));

            // Labor 2 (Future date -> Planned)
            var l2 = dbLabors[1];
            Assert.Equal(LaborMode.Planned, l2.Mode);
            Assert.Equal(LaborStatus.Planned, l2.Status);
            Assert.Null(l2.RealizedDose);
            Assert.All(l2.Supplies, s => Assert.Null(s.RealDose));

            var dbAliases = await context.SupplyAliases.ToListAsync();
            Assert.NotEmpty(dbAliases);
        }
    }

    [Fact]
    public async Task PreviewAsync_WithRealAmsaFile_ParsesSuccessfully()
    {
        string amsaPath = @"c:\Users\HWLScuffi\workspace\Gestor-OT\Planilla Cultivos 2026-2027 AMSA (1).xlsx";
        if (!File.Exists(amsaPath)) return;

        var dbName = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid();
        var campaignId = Guid.NewGuid();

        using (var context = CreateContext(dbName, tenantId))
        {
            var campaign = new Campaign { Id = campaignId, TenantId = tenantId, Name = "AMSA 2026-2027", IsActive = true };
            context.Campaigns.Add(campaign);
            await context.SaveChangesAsync();
        }

        using (var context = CreateContext(dbName, tenantId))
        {
            var service = new LaborExcelImportService(context, NullLogger<LaborExcelImportService>.Instance);
            using var stream = File.OpenRead(amsaPath);

            var preview = await service.PreviewAsync(campaignId, stream);

            Assert.NotNull(preview);
            Assert.True(preview.TotalLabors > 100, $"Esperaba > 100 labores pero obtuvo {preview.TotalLabors}");
            Assert.True(preview.TotalSupplies > 300, $"Esperaba > 300 insumos pero obtuvo {preview.TotalSupplies}");
            Assert.True(preview.UniqueSuppliesCount >= 40, $"Esperaba >= 40 insumos únicos pero obtuvo {preview.UniqueSuppliesCount}");
            Assert.True(preview.CanProceed);
        }
    }
}
