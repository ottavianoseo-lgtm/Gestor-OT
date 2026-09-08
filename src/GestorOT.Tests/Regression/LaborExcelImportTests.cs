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

            var lt1 = new LaborType { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Pulverización", ExternalErpId = "ERP-1" };
            var lt2 = new LaborType { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Fertilización", ExternalErpId = "ERP-2" };

            context.Campaigns.Add(campaign);
            context.Fields.Add(field);
            context.Lots.AddRange(lot1, lot2);
            context.CampaignLots.AddRange(cl1, cl2);
            context.Inventories.AddRange(inv1, inv2);
            context.LaborTypes.AddRange(lt1, lt2);
            await context.SaveChangesAsync();
        }

        using (var context = CreateContext(dbName, tenantId))
        {
            var service = new LaborExcelImportService(context, NullLogger<LaborExcelImportService>.Instance);
            using var stream = CreateSampleStandardExcelStream();
            var preview = await service.PreviewAsync(campaignId, stream);

            // Execute import with preview mappings
            stream.Position = 0;
            var result = await service.ExecuteAsync(campaignId, stream, preview.SupplyMappings, preview.LaborTypeMappings);

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

    [Fact]
    public async Task LaborExcelImport_MatchesContractorContact_AndSetsContactIdAndIsExternalBilling()
    {
        var dbName = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid();
        var campaignId = Guid.NewGuid();
        var contractorContactId = Guid.NewGuid();

        using (var context = CreateContext(dbName, tenantId))
        {
            var campaign = new Campaign { Id = campaignId, TenantId = tenantId, Name = "2026-2027", IsActive = true };
            var field = new Field { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Establecimiento Norte" };
            var lot1 = new Lot { Id = Guid.NewGuid(), TenantId = tenantId, FieldId = field.Id, Name = "Lote 1" };
            var lot2 = new Lot { Id = Guid.NewGuid(), TenantId = tenantId, FieldId = field.Id, Name = "Lote 2" };

            var cl1 = new CampaignLot { Id = Guid.NewGuid(), TenantId = tenantId, CampaignId = campaignId, LotId = lot1.Id };
            var cl2 = new CampaignLot { Id = Guid.NewGuid(), TenantId = tenantId, CampaignId = campaignId, LotId = lot2.Id };

            var contact = new Contact
            {
                Id = contractorContactId,
                TenantId = tenantId,
                FullName = "Don Carlos",
                Role = ContactRole.Contractor
            };

            var lt1 = new LaborType { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Pulverización", ExternalErpId = "ERP-1" };
            var lt2 = new LaborType { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Fertilización", ExternalErpId = "ERP-2" };

            context.Campaigns.Add(campaign);
            context.Fields.Add(field);
            context.Lots.AddRange(lot1, lot2);
            context.CampaignLots.AddRange(cl1, cl2);
            context.Contacts.Add(contact);
            context.LaborTypes.AddRange(lt1, lt2);
            await context.SaveChangesAsync();
        }

        using (var context = CreateContext(dbName, tenantId))
        {
            var service = new LaborExcelImportService(context, NullLogger<LaborExcelImportService>.Instance);
            using var stream = CreateSampleStandardExcelStream();

            var preview = await service.PreviewAsync(campaignId, stream);

            Assert.Equal(2, preview.Labors.Count);

            // Labor 1 ("Propio")
            var laborPropio = preview.Labors[0];
            Assert.Null(laborPropio.ContactId);
            Assert.False(laborPropio.IsExternalBilling);

            // Labor 2 ("Don Carlos" matching contact)
            var laborCarlos = preview.Labors[1];
            Assert.Equal(contractorContactId, laborCarlos.ContactId);
            Assert.Equal("Don Carlos", laborCarlos.MatchedContactName);
            Assert.True(laborCarlos.IsExternalBilling);

            // Execute import
            stream.Position = 0;
            var result = await service.ExecuteAsync(campaignId, stream, preview.SupplyMappings, preview.LaborTypeMappings);
            Assert.True(result.Success);

            // Check database
            var dbLabors = await context.Labors.OrderBy(l => l.Hectares).ToListAsync();
            Assert.Equal(2, dbLabors.Count);

            // Labor 1 in DB
            Assert.Null(dbLabors[0].ContactId);
            Assert.False(dbLabors[0].IsExternalBilling);

            // Labor 2 in DB
            Assert.Equal(contractorContactId, dbLabors[1].ContactId);
            Assert.True(dbLabors[1].IsExternalBilling);
        }
    }

    [Fact]
    public async Task LaborExcelImport_MatchesLaborType_AndLinksToExistingLaborTypeWithExternalErpId()
    {
        var dbName = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid();
        var campaignId = Guid.NewGuid();
        var lotId = Guid.NewGuid();
        var campaignLotId = Guid.NewGuid();
        var laborTypeId = Guid.NewGuid();

        using (var context = CreateContext(dbName, tenantId))
        {
            context.Campaigns.Add(new Campaign { Id = campaignId, Name = "2026/2027", TenantId = tenantId });
            context.Lots.Add(new Lot { Id = lotId, Name = "Lote 1", TenantId = tenantId });
            context.CampaignLots.Add(new CampaignLot { Id = campaignLotId, CampaignId = campaignId, LotId = lotId, TenantId = tenantId });

            // Add an existing LaborType with ExternalErpId from ERP
            context.LaborTypes.Add(new LaborType
            {
                Id = laborTypeId,
                TenantId = tenantId,
                Name = "Fertilización al Voleo",
                ExternalErpId = "1042",
                Description = "LABORES POR HECTAREA"
            });

            await context.SaveChangesAsync();
        }

        // Create Excel with "Fertilizacion" as labor name (AMSA style)
        using var stream = new MemoryStream();
        using (var wb = new ClosedXML.Excel.XLWorkbook())
        {
            var ws = wb.Worksheets.Add("AMSA");
            ws.Cell(1, 1).Value = "Fecha";
            ws.Cell(1, 2).Value = "Establecimiento";
            ws.Cell(1, 3).Value = "Lote";
            ws.Cell(1, 4).Value = "Sup";
            ws.Cell(1, 5).Value = "Produc/labor";
            ws.Cell(1, 6).Value = "Dosis";
            ws.Cell(1, 7).Value = "Tipo";
            ws.Cell(1, 8).Value = "Unidad";
            ws.Cell(1, 9).Value = "Total";
            ws.Cell(1, 10).Value = "Contr/prove";
            ws.Cell(1, 11).Value = "real/presup";

            // Labor row
            ws.Cell(2, 1).Value = new DateTime(2026, 8, 5);
            ws.Cell(2, 2).Value = "Campo Norte";
            ws.Cell(2, 3).Value = "Lote 1";
            ws.Cell(2, 4).Value = 100;
            ws.Cell(2, 5).Value = "Fertilizacion";
            ws.Cell(2, 6).Value = 1;
            ws.Cell(2, 7).Value = "Labor";
            ws.Cell(2, 8).Value = "ha";
            ws.Cell(2, 9).Value = 100;
            ws.Cell(2, 10).Value = "Propio";
            ws.Cell(2, 11).Value = "r";

            // Supply row
            ws.Cell(3, 1).Value = new DateTime(2026, 8, 5);
            ws.Cell(3, 2).Value = "Campo Norte";
            ws.Cell(3, 3).Value = "Lote 1";
            ws.Cell(3, 4).Value = 100;
            ws.Cell(3, 5).Value = "Urea";
            ws.Cell(3, 6).Value = 100;
            ws.Cell(3, 7).Value = "Fertilizante";
            ws.Cell(3, 8).Value = "kg";
            ws.Cell(3, 9).Value = 10000;
            ws.Cell(3, 10).Value = "Propio";
            ws.Cell(3, 11).Value = "r";

            wb.SaveAs(stream);
        }

        using var verifyContext = CreateContext(dbName, tenantId);
        var service = new LaborExcelImportService(verifyContext, NullLogger<LaborExcelImportService>.Instance);

            // 1. Preview
            stream.Position = 0;
            var preview = await service.PreviewAsync(campaignId, stream);

            Assert.Single(preview.LaborTypeMappings);
            var laborTypeMap = preview.LaborTypeMappings[0];
            Assert.Equal("Fertilizacion", laborTypeMap.RawName);
            Assert.Null(laborTypeMap.MatchedLaborTypeId); // Suggested only, not auto-linked
            Assert.Equal(laborTypeId, laborTypeMap.SuggestedLaborTypeId);
            Assert.Equal("Fertilización al Voleo", laborTypeMap.SuggestedLaborTypeName);
            Assert.True(laborTypeMap.Confidence >= 0.70);

            // User confirms/links the suggestion
            laborTypeMap.MatchedLaborTypeId = laborTypeMap.SuggestedLaborTypeId;
            laborTypeMap.MatchedLaborTypeName = laborTypeMap.SuggestedLaborTypeName;

            // 2. Execute
            stream.Position = 0;
            var result = await service.ExecuteAsync(campaignId, stream, preview.SupplyMappings, preview.LaborTypeMappings);

            Assert.True(result.Success);
            Assert.Equal(1, result.LaborsCreated);
            Assert.Equal(0, result.NewLaborTypesCreated);

            // 3. Verify in DB that the labor was created with the matched LaborTypeId
            var createdLabor = await verifyContext.Labors.Include(l => l.Type).FirstOrDefaultAsync();
            Assert.NotNull(createdLabor);
            Assert.Equal(laborTypeId, createdLabor.LaborTypeId);
            Assert.NotNull(createdLabor.Type);
            Assert.Equal("1042", createdLabor.Type.ExternalErpId);

            // Ensure no duplicate loose labor types were created
            var allLaborTypes = await verifyContext.LaborTypes.ToListAsync();
            Assert.Single(allLaborTypes);
    }

    [Fact]
    public async Task LaborExcelImport_LearnsLaborTypeAlias_AndNextImportMatchesWithHighConfidenceAndIsFromAlias()
    {
        var dbName = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid();
        var campaignId = Guid.NewGuid();
        var lotId = Guid.NewGuid();
        var campaignLotId = Guid.NewGuid();
        var erpLaborTypeId = Guid.NewGuid();

        using (var context = CreateContext(dbName, tenantId))
        {
            context.Campaigns.Add(new Campaign { Id = campaignId, Name = "2026/2027", TenantId = tenantId });
            context.Lots.Add(new Lot { Id = lotId, Name = "Lote 1", TenantId = tenantId });
            context.CampaignLots.Add(new CampaignLot { Id = campaignLotId, CampaignId = campaignId, LotId = lotId, TenantId = tenantId });

            // ERP LaborType in UPPERCASE
            context.LaborTypes.Add(new LaborType
            {
                Id = erpLaborTypeId,
                TenantId = tenantId,
                Name = "DISCO DOBLE",
                ExternalErpId = "ERP-DISCO-01"
            });

            await context.SaveChangesAsync();
        }

        // Helper to generate stream with "disco doble c/rolo" (lowercase, extra details)
        Stream CreateDiscoExcelStream()
        {
            var ms = new MemoryStream();
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Labores e Insumos");
            string[] headers = ["Fecha", "Establecimiento", "Lote", "Superficie (ha)", "Tipo", "Labor o Insumo", "Dosis", "Unidad", "Contratista", "Modo", "Notas"];
            for (int i = 0; i < headers.Length; i++) ws.Cell(1, i + 1).Value = headers[i];

            ws.Cell(2, 1).Value = "2026-05-10";
            ws.Cell(2, 2).Value = "Campo Norte";
            ws.Cell(2, 3).Value = "Lote 1";
            ws.Cell(2, 4).Value = 50;
            ws.Cell(2, 5).Value = "Labor";
            ws.Cell(2, 6).Value = "disco doble c/rolo";
            ws.Cell(2, 7).Value = 1;
            ws.Cell(2, 8).Value = "ha";
            ws.Cell(2, 9).Value = "Propio";
            ws.Cell(2, 10).Value = "r";
            ws.Cell(2, 11).Value = "";

            wb.SaveAs(ms);
            ms.Position = 0;
            return ms;
        }

        // Run 1: User links "disco doble c/rolo" to ERP "DISCO DOBLE"
        using (var context = CreateContext(dbName, tenantId))
        {
            var service = new LaborExcelImportService(context, NullLogger<LaborExcelImportService>.Instance);
            using var stream1 = CreateDiscoExcelStream();
            var preview1 = await service.PreviewAsync(campaignId, stream1);

            Assert.Single(preview1.LaborTypeMappings);
            var map = preview1.LaborTypeMappings[0];
            Assert.Equal("disco doble c/rolo", map.RawName);
            Assert.False(map.IsFromAlias);

            // User maps it to ERP concept
            map.MatchedLaborTypeId = erpLaborTypeId;
            map.MatchedLaborTypeName = "DISCO DOBLE";
            map.Action = "Match";

            stream1.Position = 0;
            var result1 = await service.ExecuteAsync(campaignId, stream1, preview1.SupplyMappings, preview1.LaborTypeMappings);

            Assert.True(result1.Success);
            Assert.Equal(1, result1.LaborsCreated);
            Assert.Equal(1, result1.AliasesLearned);
            Assert.Equal(0, result1.NewLaborTypesCreated);

            // Verify LaborTypeAlias was saved in DB
            var dbAliases = await context.LaborTypeAliases.ToListAsync();
            Assert.Single(dbAliases);
            Assert.Equal("disco doble c/rolo", dbAliases[0].RawName);
            Assert.Equal(erpLaborTypeId, dbAliases[0].LaborTypeId);

            // Ensure no ad-hoc LaborType was added to LaborTypes table
            var allTypes = await context.LaborTypes.ToListAsync();
            Assert.Single(allTypes);
            Assert.Equal("DISCO DOBLE", allTypes[0].Name);
        }

        // Run 2: Re-previewing the same raw name now matches via Alias with Confidence 1.0!
        using (var context = CreateContext(dbName, tenantId))
        {
            var service = new LaborExcelImportService(context, NullLogger<LaborExcelImportService>.Instance);
            using var stream2 = CreateDiscoExcelStream();
            var preview2 = await service.PreviewAsync(campaignId, stream2);

            Assert.Single(preview2.LaborTypeMappings);
            var map2 = preview2.LaborTypeMappings[0];
            Assert.Equal("disco doble c/rolo", map2.RawName);
            Assert.True(map2.IsFromAlias);
            Assert.Equal(1.0, map2.Confidence);
            Assert.Equal(erpLaborTypeId, map2.MatchedLaborTypeId);
            Assert.Equal("DISCO DOBLE", map2.MatchedLaborTypeName);
        }
    }

    [Fact]
    public async Task LaborExcelImport_UnmatchedLaborType_DoesNotCreateLaborTypeInDatabase()
    {
        var dbName = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid();
        var campaignId = Guid.NewGuid();
        var lotId = Guid.NewGuid();
        var campaignLotId = Guid.NewGuid();

        using (var context = CreateContext(dbName, tenantId))
        {
            context.Campaigns.Add(new Campaign { Id = campaignId, Name = "2026/2027", TenantId = tenantId });
            context.Lots.Add(new Lot { Id = lotId, Name = "Lote 1", TenantId = tenantId });
            context.CampaignLots.Add(new CampaignLot { Id = campaignLotId, CampaignId = campaignId, LotId = lotId, TenantId = tenantId });
            await context.SaveChangesAsync();
        }

        using var ms = new MemoryStream();
        using (var wb = new XLWorkbook())
        {
            var ws = wb.Worksheets.Add("Labores e Insumos");
            string[] headers = ["Fecha", "Establecimiento", "Lote", "Superficie (ha)", "Tipo", "Labor o Insumo", "Dosis", "Unidad", "Contratista", "Modo", "Notas"];
            for (int i = 0; i < headers.Length; i++) ws.Cell(1, i + 1).Value = headers[i];

            ws.Cell(2, 1).Value = "2026-05-10";
            ws.Cell(2, 2).Value = "Campo Norte";
            ws.Cell(2, 3).Value = "Lote 1";
            ws.Cell(2, 4).Value = 50;
            ws.Cell(2, 5).Value = "Labor";
            ws.Cell(2, 6).Value = "Desmalezado Químico Raro";
            ws.Cell(2, 7).Value = 1;
            ws.Cell(2, 8).Value = "ha";
            ws.Cell(2, 9).Value = "Propio";
            ws.Cell(2, 10).Value = "r";
            ws.Cell(2, 11).Value = "";

            wb.SaveAs(ms);
        }

        using (var context = CreateContext(dbName, tenantId))
        {
            var service = new LaborExcelImportService(context, NullLogger<LaborExcelImportService>.Instance);
            ms.Position = 0;
            var preview = await service.PreviewAsync(campaignId, ms);

            Assert.Single(preview.LaborTypeMappings);
            Assert.Null(preview.LaborTypeMappings[0].MatchedLaborTypeId);

            // Execute without assigning ERP concept
            ms.Position = 0;
            var result = await service.ExecuteAsync(campaignId, ms, preview.SupplyMappings, preview.LaborTypeMappings);

            // The labor cannot be created and should be reported in Errors
            Assert.False(result.Success);
            Assert.Equal(0, result.LaborsCreated);
            Assert.Equal(0, result.NewLaborTypesCreated);
            Assert.NotEmpty(result.Errors);
            Assert.Contains("ERP", result.Errors[0]);

            // Ensure absolutely NO LaborType was persisted
            var dbLaborTypes = await context.LaborTypes.ToListAsync();
            Assert.Empty(dbLaborTypes);

            var dbLabors = await context.Labors.ToListAsync();
            Assert.Empty(dbLabors);
        }
    }

    [Fact]
    public async Task LaborExcelImport_ReimportSameExcel_UpdatesExistingLaborsAndSuppliesInsteadOfDuplicating()
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

            var lt1 = new LaborType { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Pulverización", ExternalErpId = "ERP-1" };
            var lt2 = new LaborType { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Fertilización", ExternalErpId = "ERP-2" };

            context.Campaigns.Add(campaign);
            context.Fields.Add(field);
            context.Lots.AddRange(lot1, lot2);
            context.CampaignLots.AddRange(cl1, cl2);
            context.Inventories.AddRange(inv1, inv2);
            context.LaborTypes.AddRange(lt1, lt2);
            await context.SaveChangesAsync();
        }

        using (var context = CreateContext(dbName, tenantId))
        {
            var service = new LaborExcelImportService(context, NullLogger<LaborExcelImportService>.Instance);
            using var stream = CreateSampleStandardExcelStream();
            var preview = await service.PreviewAsync(campaignId, stream);

            // First import
            stream.Position = 0;
            var result1 = await service.ExecuteAsync(campaignId, stream, preview.SupplyMappings, preview.LaborTypeMappings);

            Assert.True(result1.Success);
            Assert.Equal(2, result1.LaborsCreated);
            Assert.Equal(0, result1.LaborsUpdated);

            var dbLaborsCount = await context.Labors.CountAsync();
            Assert.Equal(2, dbLaborsCount);

            // Second import of the EXACT SAME FILE
            stream.Position = 0;
            var result2 = await service.ExecuteAsync(campaignId, stream, preview.SupplyMappings, preview.LaborTypeMappings);

            Assert.True(result2.Success);
            Assert.Equal(0, result2.LaborsCreated); // No new duplicates!
            Assert.Equal(2, result2.LaborsUpdated); // Existing labors updated

            // Count in DB must still be 2, NOT 4
            var dbLaborsAfter = await context.Labors.Include(l => l.Supplies).ToListAsync();
            Assert.Equal(2, dbLaborsAfter.Count);

            // Verify supplies were cleanly replaced and not duplicated
            var l1 = dbLaborsAfter.FirstOrDefault(l => l.Hectares == 50);
            Assert.NotNull(l1);
            Assert.Equal(2, l1.Supplies.Count);
        }
    }
}
