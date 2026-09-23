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
            // Números exactos de la planilla real: un ">" no habría detectado OT-60 ni el
            // encabezado partido de "Contr/Prove", que leían de menos sin romper nada.
            Assert.Equal(140, preview.TotalLabors);
            Assert.Equal(363, preview.TotalSupplies);
            Assert.Equal(41, preview.UniqueSuppliesCount);
            Assert.True(preview.CanProceed);

            // Contr/Prove se lee en todas las filas (responsable de la labor y proveedor
            // del insumo), no en cero como cuando el salto de línea rompía el match.
            Assert.DoesNotContain(preview.Labors, l => string.IsNullOrWhiteSpace(l.Contractor));
            Assert.Contains(preview.Labors.SelectMany(l => l.Supplies), s => !string.IsNullOrWhiteSpace(s.SupplierRawName));

            // Lote 12: Sup presupuestada 33, Sup. Real 29 (OT-60).
            var lote12 = preview.Labors.Where(l => l.LotName == "12").ToList();
            Assert.NotEmpty(lote12);
            Assert.Contains(lote12, l => l.Hectares == 29);
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

    // OT-26: la columna "Contr/Prove" es la MISMA columna del Excel tanto para la fila de Labor
    // como para las filas de Insumo, pero significa cosas distintas segun la fila (confirmado
    // contra el archivo real "Planilla Cultivos 2026-2027 AMSA (1).xlsx", hoja "Planilla Datos":
    // en la fila "Labor" es el responsable/contratista que ejecuta; en las filas de insumo que
    // siguen es el proveedor de ese insumo puntual, ej. fila Labor "Propio" / fila Herbicida
    // "Lartirigoyen"). Este test verifica que ambos significados se resuelven por separado.
    [Fact]
    public async Task LaborExcelImport_ResolvesSupplierPerInsumoRow_DistinctFromLaborResponsible()
    {
        var dbName = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid();
        var campaignId = Guid.NewGuid();
        var supplierContactId = Guid.NewGuid();

        using (var context = CreateContext(dbName, tenantId))
        {
            var campaign = new Campaign { Id = campaignId, TenantId = tenantId, Name = "2026-2027", IsActive = true };
            var field = new Field { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Campo Norte" };
            var lot1 = new Lot { Id = Guid.NewGuid(), TenantId = tenantId, FieldId = field.Id, Name = "Lote 1" };
            var cl1 = new CampaignLot { Id = Guid.NewGuid(), TenantId = tenantId, CampaignId = campaignId, LotId = lot1.Id };

            var supplierContact = new Contact
            {
                Id = supplierContactId,
                TenantId = tenantId,
                FullName = "Lartirigoyen",
                Role = ContactRole.Contractor
            };

            var lt1 = new LaborType { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Pulverización", ExternalErpId = "ERP-1" };

            context.Campaigns.Add(campaign);
            context.Fields.Add(field);
            context.Lots.Add(lot1);
            context.CampaignLots.Add(cl1);
            context.Contacts.Add(supplierContact);
            context.LaborTypes.Add(lt1);
            await context.SaveChangesAsync();
        }

        using var stream = new MemoryStream();
        using (var wb = new ClosedXML.Excel.XLWorkbook())
        {
            var ws = wb.Worksheets.Add("Labores e Insumos");
            string[] headers = ["Fecha", "Establecimiento", "Lote", "Superficie (ha)", "Tipo", "Labor o Insumo", "Dosis", "Unidad", "Contratista", "Modo", "Notas"];
            for (int i = 0; i < headers.Length; i++) ws.Cell(1, i + 1).Value = headers[i];

            // Fila de Labor: "Contr/Prove" = Propio (el responsable de la labor)
            ws.Cell(2, 1).Value = "2026-02-20";
            ws.Cell(2, 2).Value = "Campo Norte";
            ws.Cell(2, 3).Value = "Lote 1";
            ws.Cell(2, 4).Value = 50;
            ws.Cell(2, 5).Value = "Labor";
            ws.Cell(2, 6).Value = "Pulverización";
            ws.Cell(2, 7).Value = 1;
            ws.Cell(2, 8).Value = "ha";
            ws.Cell(2, 9).Value = "Propio";

            // Fila de Insumo: "Contr/Prove" = Lartirigoyen (el proveedor de ESE insumo)
            ws.Cell(3, 1).Value = "2026-02-20";
            ws.Cell(3, 2).Value = "Campo Norte";
            ws.Cell(3, 3).Value = "Lote 1";
            ws.Cell(3, 4).Value = 50;
            ws.Cell(3, 5).Value = "Herbicida";
            ws.Cell(3, 6).Value = "2,4D";
            ws.Cell(3, 7).Value = 1.5;
            ws.Cell(3, 8).Value = "litros";
            ws.Cell(3, 9).Value = "Lartirigoyen";

            // Fila de Insumo con proveedor que NO matchea ningun contacto existente
            ws.Cell(4, 1).Value = "2026-02-20";
            ws.Cell(4, 2).Value = "Campo Norte";
            ws.Cell(4, 3).Value = "Lote 1";
            ws.Cell(4, 4).Value = 50;
            ws.Cell(4, 5).Value = "Coadyuvante";
            ws.Cell(4, 6).Value = "Aceite Vegetal";
            ws.Cell(4, 7).Value = 0.5;
            ws.Cell(4, 8).Value = "litros";
            ws.Cell(4, 9).Value = "Proveedor Desconocido SRL";

            wb.SaveAs(stream);
        }

        using var context2 = CreateContext(dbName, tenantId);
        var service = new LaborExcelImportService(context2, NullLogger<LaborExcelImportService>.Instance);

        stream.Position = 0;
        var preview = await service.PreviewAsync(campaignId, stream);

        var labor = Assert.Single(preview.Labors);

        // El responsable de la labor ("Propio") sigue sin contacto asignado, sin relacion
        // con el proveedor de sus insumos.
        Assert.Null(labor.ContactId);
        Assert.Equal(2, labor.Supplies.Count);

        var herbicida = labor.Supplies.First(s => s.SupplyName == "2,4D");
        Assert.Equal("Lartirigoyen", herbicida.SupplierRawName);
        Assert.Equal(supplierContactId, herbicida.SupplierContactId);
        Assert.Equal("Lartirigoyen", herbicida.MatchedSupplierName);

        var coadyuvante = labor.Supplies.First(s => s.SupplyName == "Aceite Vegetal");
        Assert.Equal("Proveedor Desconocido SRL", coadyuvante.SupplierRawName);
        Assert.Null(coadyuvante.SupplierContactId);

        // La vista previa expone ambos proveedores para revision/correccion
        Assert.Equal(2, preview.SupplierMappings.Count);
        Assert.Equal(1, preview.UnmatchedSuppliersCount);
        var unmatchedMap = preview.SupplierMappings.First(m => m.RawName == "Proveedor Desconocido SRL");
        Assert.Null(unmatchedMap.MatchedContactId);
        Assert.Equal("None", unmatchedMap.ConfidenceLevel);
        var matchedMap = preview.SupplierMappings.First(m => m.RawName == "Lartirigoyen");
        Assert.Equal(supplierContactId, matchedMap.MatchedContactId);

        // Ejecutar: el insumo sin proveedor matcheado NO bloquea el import (trampa del plan)
        stream.Position = 0;
        var result = await service.ExecuteAsync(campaignId, stream, preview.SupplyMappings, preview.LaborTypeMappings, preview.SupplierMappings);
        Assert.True(result.Success);
        Assert.Empty(result.Errors);

        var dbLabor = await context2.Labors.Include(l => l.Supplies).FirstAsync();
        var dbHerbicida = dbLabor.Supplies.First(s => s.PlannedDose == 1.5m);
        Assert.Equal(supplierContactId, dbHerbicida.SupplierContactId);

        var dbCoadyuvante = dbLabor.Supplies.First(s => s.PlannedDose == 0.5m);
        Assert.Null(dbCoadyuvante.SupplierContactId);
    }

    [Fact]
    public async Task LaborExcelImport_UserCorrectsUnmatchedSupplierMapping_BeforeExecute_LinksLaborSupplyToContact()
    {
        var dbName = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid();
        var campaignId = Guid.NewGuid();
        var manualContactId = Guid.NewGuid();

        using (var context = CreateContext(dbName, tenantId))
        {
            var campaign = new Campaign { Id = campaignId, TenantId = tenantId, Name = "2026-2027", IsActive = true };
            var lot1 = new Lot { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Lote 1" };
            var cl1 = new CampaignLot { Id = Guid.NewGuid(), TenantId = tenantId, CampaignId = campaignId, LotId = lot1.Id };
            var lt1 = new LaborType { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Pulverización", ExternalErpId = "ERP-1" };
            var manualContact = new Contact { Id = manualContactId, TenantId = tenantId, FullName = "Distribuidora Agro SA", Role = ContactRole.Contractor };

            context.Campaigns.Add(campaign);
            context.Lots.Add(lot1);
            context.CampaignLots.Add(cl1);
            context.LaborTypes.Add(lt1);
            context.Contacts.Add(manualContact);
            await context.SaveChangesAsync();
        }

        using var stream = new MemoryStream();
        using (var wb = new ClosedXML.Excel.XLWorkbook())
        {
            var ws = wb.Worksheets.Add("Labores e Insumos");
            string[] headers = ["Fecha", "Establecimiento", "Lote", "Superficie (ha)", "Tipo", "Labor o Insumo", "Dosis", "Unidad", "Contratista", "Modo", "Notas"];
            for (int i = 0; i < headers.Length; i++) ws.Cell(1, i + 1).Value = headers[i];

            ws.Cell(2, 1).Value = "2026-02-20";
            ws.Cell(2, 2).Value = "Campo Norte";
            ws.Cell(2, 3).Value = "Lote 1";
            ws.Cell(2, 4).Value = 50;
            ws.Cell(2, 5).Value = "Labor";
            ws.Cell(2, 6).Value = "Pulverización";
            ws.Cell(2, 7).Value = 1;
            ws.Cell(2, 8).Value = "ha";
            ws.Cell(2, 9).Value = "Propio";

            // Nombre libre que no matchea contra "Distribuidora Agro SA" por ninguna de las
            // reglas de MatchContact (exacto, prefijo, substring).
            ws.Cell(3, 1).Value = "2026-02-20";
            ws.Cell(3, 2).Value = "Campo Norte";
            ws.Cell(3, 3).Value = "Lote 1";
            ws.Cell(3, 4).Value = 50;
            ws.Cell(3, 5).Value = "Herbicida";
            ws.Cell(3, 6).Value = "2,4D";
            ws.Cell(3, 7).Value = 1.5;
            ws.Cell(3, 8).Value = "litros";
            ws.Cell(3, 9).Value = "Insumos XYZ Import";

            wb.SaveAs(stream);
        }

        using var context2 = CreateContext(dbName, tenantId);
        var service = new LaborExcelImportService(context2, NullLogger<LaborExcelImportService>.Instance);

        stream.Position = 0;
        var preview = await service.PreviewAsync(campaignId, stream);

        var supplierMap = Assert.Single(preview.SupplierMappings);
        Assert.Equal("Insumos XYZ Import", supplierMap.RawName);
        Assert.Null(supplierMap.MatchedContactId); // no matcheo automaticamente

        // El usuario corrige manualmente en la vista previa (pestaña de Reconciliación de Proveedores)
        supplierMap.MatchedContactId = manualContactId;
        supplierMap.MatchedContactName = "Distribuidora Agro SA";

        stream.Position = 0;
        var result = await service.ExecuteAsync(campaignId, stream, preview.SupplyMappings, preview.LaborTypeMappings, preview.SupplierMappings);
        Assert.True(result.Success);

        var dbLabor = await context2.Labors.Include(l => l.Supplies).FirstAsync();
        var dbSupply = Assert.Single(dbLabor.Supplies);
        Assert.Equal(manualContactId, dbSupply.SupplierContactId);
    }

    // Regresion: un Excel sin ninguna columna de contratista/proveedor se sigue importando
    // exactamente igual que antes de OT-26 (sin errores, sin proveedor asignado a nada).
    [Fact]
    public async Task LaborExcelImport_WithoutContratistaColumn_ImportsWithoutErrorsAndNoSupplierAssigned()
    {
        var dbName = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid();
        var campaignId = Guid.NewGuid();

        using (var context = CreateContext(dbName, tenantId))
        {
            var campaign = new Campaign { Id = campaignId, TenantId = tenantId, Name = "2026-2027", IsActive = true };
            var lot1 = new Lot { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Lote 1" };
            var cl1 = new CampaignLot { Id = Guid.NewGuid(), TenantId = tenantId, CampaignId = campaignId, LotId = lot1.Id };
            var lt1 = new LaborType { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Pulverización", ExternalErpId = "ERP-1" };

            context.Campaigns.Add(campaign);
            context.Lots.Add(lot1);
            context.CampaignLots.Add(cl1);
            context.LaborTypes.Add(lt1);
            await context.SaveChangesAsync();
        }

        using var stream = new MemoryStream();
        using (var wb = new ClosedXML.Excel.XLWorkbook())
        {
            var ws = wb.Worksheets.Add("Labores e Insumos");
            // Sin columna de Contratista / Contr-Prove
            string[] headers = ["Fecha", "Establecimiento", "Lote", "Superficie (ha)", "Tipo", "Labor o Insumo", "Dosis", "Unidad"];
            for (int i = 0; i < headers.Length; i++) ws.Cell(1, i + 1).Value = headers[i];

            ws.Cell(2, 1).Value = "2026-02-20";
            ws.Cell(2, 2).Value = "Campo Norte";
            ws.Cell(2, 3).Value = "Lote 1";
            ws.Cell(2, 4).Value = 50;
            ws.Cell(2, 5).Value = "Labor";
            ws.Cell(2, 6).Value = "Pulverización";
            ws.Cell(2, 7).Value = 1;
            ws.Cell(2, 8).Value = "ha";

            ws.Cell(3, 1).Value = "2026-02-20";
            ws.Cell(3, 2).Value = "Campo Norte";
            ws.Cell(3, 3).Value = "Lote 1";
            ws.Cell(3, 4).Value = 50;
            ws.Cell(3, 5).Value = "Herbicida";
            ws.Cell(3, 6).Value = "2,4D";
            ws.Cell(3, 7).Value = 1.5;
            ws.Cell(3, 8).Value = "litros";

            wb.SaveAs(stream);
        }

        using var context2 = CreateContext(dbName, tenantId);
        var service = new LaborExcelImportService(context2, NullLogger<LaborExcelImportService>.Instance);

        stream.Position = 0;
        var preview = await service.PreviewAsync(campaignId, stream);

        var labor = Assert.Single(preview.Labors);
        Assert.Null(labor.ContactId);
        var supply = Assert.Single(labor.Supplies);
        Assert.Null(supply.SupplierRawName);
        Assert.Null(supply.SupplierContactId);
        Assert.Empty(preview.SupplierMappings);
        Assert.Equal(0, preview.UnmatchedSuppliersCount);

        stream.Position = 0;
        var result = await service.ExecuteAsync(campaignId, stream, preview.SupplyMappings, preview.LaborTypeMappings, preview.SupplierMappings);
        Assert.True(result.Success);
        Assert.Empty(result.Errors);

        var dbLabor = await context2.Labors.Include(l => l.Supplies).FirstAsync();
        var dbSupply = Assert.Single(dbLabor.Supplies);
        Assert.Null(dbSupply.SupplierContactId);
    }

    /// <summary>
    /// Los mappings de un lote pendiente se congelan al subir el archivo, pero el
    /// catálogo sigue vivo. La FK SupplyAliases -> Inventories es ON DELETE CASCADE:
    /// borrar el insumo se lleva el alias en silencio y deja el MatchedSupplyId
    /// apuntando a la nada. Al importar los pendientes, el alias se recreaba con ese
    /// id muerto y la violación de FK volteaba la transacción entera, sin importar
    /// ninguna fila. Ahora el mapping colgado manda su fila a revisión y el resto pasa.
    /// </summary>
    [Fact]
    public async Task ImportBatchRowsAsync_WhenMappedInventoryWasDeleted_SendsRowToReviewInsteadOfFailingWholeBatch()
    {
        var dbName = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid();
        var campaignId = Guid.NewGuid();
        var ureaId = Guid.NewGuid();

        using (var context = CreateContext(dbName, tenantId))
        {
            var campaign = new Campaign { Id = campaignId, TenantId = tenantId, Name = "2026-2027", IsActive = true };
            var field = new Field { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Establecimiento Norte" };
            var lot1 = new Lot { Id = Guid.NewGuid(), TenantId = tenantId, FieldId = field.Id, Name = "Lote 1" };
            var lot2 = new Lot { Id = Guid.NewGuid(), TenantId = tenantId, FieldId = field.Id, Name = "Lote 2" };

            // Ninguno de los dos entra a la campaña: las dos labores quedan pendientes
            // en el lote, que es el escenario donde el mapping se congela.
            context.Campaigns.Add(campaign);
            context.Fields.Add(field);
            context.Lots.AddRange(lot1, lot2);
            context.Inventories.AddRange(
                new Inventory { Id = Guid.NewGuid(), TenantId = tenantId, ItemName = "Glifosato 66%", Category = "Herbicida", Unit = "litros" },
                new Inventory { Id = Guid.NewGuid(), TenantId = tenantId, ItemName = "Aceite Mineral", Category = "Coadyuvante", Unit = "litros" },
                new Inventory { Id = ureaId, TenantId = tenantId, ItemName = "Urea Granulada", Category = "Fertilizante", Unit = "kg" });
            context.LaborTypes.AddRange(
                new LaborType { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Pulverización", ExternalErpId = "ERP-1" },
                new LaborType { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Fertilización", ExternalErpId = "ERP-2" });
            await context.SaveChangesAsync();
        }

        Guid batchId;
        using (var context = CreateContext(dbName, tenantId))
        {
            var service = new LaborExcelImportService(context, NullLogger<LaborExcelImportService>.Instance);
            using var stream = CreateSampleStandardExcelStream();
            var upload = await service.UploadAsync(campaignId, stream, "labores.xlsx", "tester");
            Assert.NotNull(upload.PendingBatchId);
            batchId = upload.PendingBatchId!.Value;
        }

        // El catálogo se mueve debajo del lote: se borra el insumo y el CASCADE de la
        // FK se lleva el alias aprendido.
        using (var context = CreateContext(dbName, tenantId))
        {
            var urea = await context.Inventories.FirstAsync(i => i.Id == ureaId);
            context.SupplyAliases.RemoveRange(await context.SupplyAliases.Where(a => a.SupplyId == ureaId).ToListAsync());
            context.Inventories.Remove(urea);
            await context.SaveChangesAsync();
        }

        using (var context = CreateContext(dbName, tenantId))
        {
            var service = new LaborExcelImportService(context, NullLogger<LaborExcelImportService>.Instance);

            // Antes reventaba con DbUpdateException por FK_SupplyAliases_Inventories_SupplyId.
            var result = await service.ImportBatchRowsAsync(batchId, null);

            Assert.Contains(result.Errors, e => e.Contains("Urea Granulada"));

            // Y sobre todo: no quedó ningún alias apuntando al insumo borrado.
            Assert.False(await context.SupplyAliases.AnyAsync(a => a.SupplyId == ureaId));
        }
    }

    /// <summary>
    /// Mismo modo de falla que el insumo congelado, por el lado de Contacts: el
    /// proveedor vinculado en la conciliación se borra y el MatchedContactId del
    /// lote queda muerto. Escribirlo en LaborSupply.SupplierContactId violaba el FK
    /// y volteaba la transacción. Un proveedor sin vincular nunca bloqueó una fila,
    /// así que la degradación correcta es importar sin proveedor.
    /// </summary>
    [Fact]
    public async Task ImportBatchRowsAsync_WhenMappedSupplierContactWasDeleted_ImportsWithoutSupplierInsteadOfFailing()
    {
        var dbName = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid();
        var campaignId = Guid.NewGuid();
        var supplierContactId = Guid.NewGuid();

        using (var context = CreateContext(dbName, tenantId))
        {
            var campaign = new Campaign { Id = campaignId, TenantId = tenantId, Name = "2026-2027", IsActive = true };
            var field = new Field { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Campo Norte" };
            var lot1 = new Lot { Id = Guid.NewGuid(), TenantId = tenantId, FieldId = field.Id, Name = "Lote 1" };
            var cl1 = new CampaignLot { Id = Guid.NewGuid(), TenantId = tenantId, CampaignId = campaignId, LotId = lot1.Id };

            context.Campaigns.Add(campaign);
            context.Fields.Add(field);
            context.Lots.Add(lot1);
            context.CampaignLots.Add(cl1);
            context.Inventories.Add(new Inventory { Id = Guid.NewGuid(), TenantId = tenantId, ItemName = "Glifosato 66%", Category = "Herbicida", Unit = "litros" });
            context.LaborTypes.Add(new LaborType { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Pulverización", ExternalErpId = "ERP-1" });
            await context.SaveChangesAsync();
        }

        using var stream = new MemoryStream();
        using (var wb = new ClosedXML.Excel.XLWorkbook())
        {
            var ws = wb.Worksheets.Add("Labores e Insumos");
            string[] headers = ["Fecha", "Establecimiento", "Lote", "Superficie (ha)", "Tipo", "Labor o Insumo", "Dosis", "Unidad", "Contratista", "Modo", "Notas"];
            for (int i = 0; i < headers.Length; i++) ws.Cell(1, i + 1).Value = headers[i];

            ws.Cell(2, 1).Value = "2026-02-20";
            ws.Cell(2, 2).Value = "Campo Norte";
            ws.Cell(2, 3).Value = "Lote 1";
            ws.Cell(2, 4).Value = 50;
            ws.Cell(2, 5).Value = "Labor";
            ws.Cell(2, 6).Value = "Pulverización";
            ws.Cell(2, 7).Value = 1;
            ws.Cell(2, 8).Value = "ha";
            ws.Cell(2, 9).Value = "Propio";

            // Proveedor del insumo: no existe en el padrón, así que la fila queda pendiente.
            ws.Cell(3, 1).Value = "2026-02-20";
            ws.Cell(3, 2).Value = "Campo Norte";
            ws.Cell(3, 3).Value = "Lote 1";
            ws.Cell(3, 4).Value = 50;
            ws.Cell(3, 5).Value = "Herbicida";
            ws.Cell(3, 6).Value = "Glifosato 66%";
            ws.Cell(3, 7).Value = 2.5;
            ws.Cell(3, 8).Value = "litros";
            ws.Cell(3, 9).Value = "Lartirigoyen";

            wb.SaveAs(stream);
        }
        stream.Position = 0;

        Guid batchId;
        using (var context = CreateContext(dbName, tenantId))
        {
            var service = new LaborExcelImportService(context, NullLogger<LaborExcelImportService>.Instance);
            var upload = await service.UploadAsync(campaignId, stream, "labores.xlsx", "tester");
            Assert.NotNull(upload.PendingBatchId);
            batchId = upload.PendingBatchId!.Value;
        }

        // El usuario da de alta el proveedor y lo vincula en la conciliación.
        using (var context = CreateContext(dbName, tenantId))
        {
            context.Contacts.Add(new Contact
            {
                Id = supplierContactId,
                TenantId = tenantId,
                FullName = "Lartirigoyen",
                Role = ContactRole.Contractor
            });
            await context.SaveChangesAsync();

            var service = new LaborExcelImportService(context, NullLogger<LaborExcelImportService>.Instance);
            var detail = await service.GetBatchDetailAsync(batchId);
            Assert.NotNull(detail);
            await service.SaveBatchMappingsAsync(batchId, new LaborImportBatchMappingsDto
            {
                SupplyMappings = detail!.Preview.SupplyMappings,
                LaborTypeMappings = detail.Preview.LaborTypeMappings,
                SupplierMappings = new List<LaborImportSupplierMappingDto>
                {
                    new() { RawName = "Lartirigoyen", MatchedContactId = supplierContactId, MatchedContactName = "Lartirigoyen", Action = "Match" }
                }
            });
        }

        // Y después alguien borra el contacto.
        using (var context = CreateContext(dbName, tenantId))
        {
            context.Contacts.Remove(await context.Contacts.FirstAsync(c => c.Id == supplierContactId));
            await context.SaveChangesAsync();
        }

        using (var context = CreateContext(dbName, tenantId))
        {
            var service = new LaborExcelImportService(context, NullLogger<LaborExcelImportService>.Instance);

            // Antes reventaba con DbUpdateException por el FK contra Contacts.
            var result = await service.ImportBatchRowsAsync(batchId, null);

            Assert.True(result.Success);
            Assert.Equal(1, result.Imported);

            var dbLabor = await context.Labors.Include(l => l.Supplies).SingleAsync();
            var dbSupply = Assert.Single(dbLabor.Supplies);
            Assert.Null(dbSupply.SupplierContactId);
        }
    }

    /// <summary>
    /// Planilla con los encabezados tal cual los manda AMSA: "Contr/Prove" y
    /// "real/presup" traen un salto de línea adentro de la celda, y conviven "Sup"
    /// (presupuestada) con "Sup. Real" (la que se trabajó).
    /// </summary>
    private static Stream CreateAmsaLikeExcelStream()
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Planilla Datos");

        string[] headers =
        [
            "Fecha-1", "Fecha", "Establecimiento", "Lote", "Sup", "Sup. Real",
            "Produc/labor", "Dosis", "Tipo", "Unidad", "Total", "Contr/\nProve", "real/\npresup"
        ];
        for (int i = 0; i < headers.Length; i++)
            ws.Cell(1, i + 1).Value = headers[i];

        var rows = new object?[][]
        {
            // Labor con superficie presupuestada 33 y real 29 -> vale la real
            ["2026-01-10", "2026-01-10", "La Laura", "Lote 12", 33, 29, "Pulverización", 1, "Labor", "ha", 33, "Propio", "r"],
            ["2026-01-10", "2026-01-10", "La Laura", "Lote 12", 33, 29, "Glifosato 66%", 2, "Herbicida", "litros", 66, "Ekun", "r"],
            // Labor sin superficie real cargada -> cae a la presupuestada
            ["2026-01-12", "2026-01-12", "La Laura", "Lote 13", 40, null, "Fertilización", 1, "Labor", "ha", 40, "Don Carlos", "r"]
        };

        for (int r = 0; r < rows.Length; r++)
        {
            for (int c = 0; c < rows[r].Length; c++)
            {
                var val = rows[r][c];
                if (val is null) continue;
                var cell = ws.Cell(r + 2, c + 1);
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

    /// <summary>
    /// OT-60: la planilla trae "Sup" (presupuestada) y "Sup. Real"; la labor se hizo
    /// sobre la real. Antes ganaba "Sup" porque "Sup. Real" no matcheaba ningún caso
    /// del detector y la superficie entraba inflada.
    /// </summary>
    [Fact]
    public async Task PreviewAsync_WhenSheetHasBudgetedAndRealSurface_UsesRealSurface()
    {
        var dbName = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid();
        var campaignId = Guid.NewGuid();

        using (var context = CreateContext(dbName, tenantId))
        {
            context.Campaigns.Add(new Campaign { Id = campaignId, TenantId = tenantId, Name = "AMSA", IsActive = true });
            await context.SaveChangesAsync();
        }

        using (var context = CreateContext(dbName, tenantId))
        {
            var service = new LaborExcelImportService(context, NullLogger<LaborExcelImportService>.Instance);
            using var stream = CreateAmsaLikeExcelStream();

            var preview = await service.PreviewAsync(campaignId, stream);

            Assert.Equal(2, preview.TotalLabors);
            Assert.Equal(29, preview.Labors[0].Hectares);
            // Sin superficie real cargada, vale la presupuestada en vez de quedar en cero.
            Assert.Equal(40, preview.Labors[1].Hectares);
        }
    }

    /// <summary>
    /// El encabezado "Contr/\nProve" tiene un salto de línea adentro: comparándolo sin
    /// aplanar, la columna quedaba en 0 y ni la labor tenía responsable ni el insumo
    /// proveedor. Afectaba al 100% de las filas de la planilla real.
    /// </summary>
    [Fact]
    public async Task PreviewAsync_WhenHeaderHasLineBreak_StillReadsContractorAndSupplier()
    {
        var dbName = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid();
        var campaignId = Guid.NewGuid();

        using (var context = CreateContext(dbName, tenantId))
        {
            context.Campaigns.Add(new Campaign { Id = campaignId, TenantId = tenantId, Name = "AMSA", IsActive = true });
            context.Contacts.Add(new Contact { Id = Guid.NewGuid(), TenantId = tenantId, FullName = "Ekun", Role = ContactRole.Supplier });
            await context.SaveChangesAsync();
        }

        using (var context = CreateContext(dbName, tenantId))
        {
            var service = new LaborExcelImportService(context, NullLogger<LaborExcelImportService>.Instance);
            using var stream = CreateAmsaLikeExcelStream();

            var preview = await service.PreviewAsync(campaignId, stream);

            var labor = preview.Labors[0];
            Assert.Equal("Propio", labor.Contractor);
            var supply = Assert.Single(labor.Supplies);
            Assert.Equal("Ekun", supply.SupplierRawName);
            Assert.NotNull(supply.SupplierContactId);

            Assert.Equal("Don Carlos", preview.Labors[1].Contractor);
        }
    }

    /// <summary>
    /// La planilla escribe el responsable como "Nombre Apellido" y el padron lo tiene
    /// como "APELLIDO NOMBRE" (o con un segundo nombre de mas). Sin matcheo por
    /// palabras la fila quedaba trabada en conciliacion sin forma de resolverla: el
    /// panel tiene solapa para proveedores de insumos, no para el responsable.
    /// </summary>
    [Theory]
    [InlineData("Daniel Paiuzza", "PAIUZZA DANIEL")]
    [InlineData("Luis Gismondi", "GISMONDI LUIS OSCAR")]
    [InlineData("Marcos Lamattina", "Lamattina, Marcos Daniel")]
    [InlineData("Serv. Agrop.", "SERVICIOS AGROPECUARIOS S.R.L.")]
    public async Task PreviewAsync_MatchesContactWithInvertedOrSurnameFirstName(string enPlanilla, string enPadron)
    {
        var dbName = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid();
        var campaignId = Guid.NewGuid();

        using (var context = CreateContext(dbName, tenantId))
        {
            var field = new Field { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Establecimiento Norte" };
            var lot = new Lot { Id = Guid.NewGuid(), TenantId = tenantId, FieldId = field.Id, Name = "Lote 1" };
            context.Campaigns.Add(new Campaign { Id = campaignId, TenantId = tenantId, Name = "2026-2027", IsActive = true });
            context.Fields.Add(field);
            context.Lots.Add(lot);
            context.CampaignLots.Add(new CampaignLot { Id = Guid.NewGuid(), TenantId = tenantId, CampaignId = campaignId, LotId = lot.Id });
            context.Contacts.Add(new Contact { Id = Guid.NewGuid(), TenantId = tenantId, FullName = enPadron, Role = ContactRole.Contractor });
            // Ruido: otro contacto que comparte una palabra no debe confundir el match.
            context.Contacts.Add(new Contact { Id = Guid.NewGuid(), TenantId = tenantId, FullName = "Otro Proveedor SA", Role = ContactRole.Supplier });
            await context.SaveChangesAsync();
        }

        using (var context = CreateContext(dbName, tenantId))
        {
            var service = new LaborExcelImportService(context, NullLogger<LaborExcelImportService>.Instance);
            using var stream = CreateContractorExcelStream(enPlanilla);

            var preview = await service.PreviewAsync(campaignId, stream);

            var labor = Assert.Single(preview.Labors);
            Assert.NotNull(labor.ContactId);
            Assert.Equal(enPadron, labor.MatchedContactName);
        }
    }

    /// <summary>
    /// Con dos contactos igual de parecidos no se elige ninguno: la fila va a
    /// conciliacion en vez de atribuirle la labor a la persona equivocada.
    /// </summary>
    [Fact]
    public async Task PreviewAsync_WhenTwoContactsMatchEquallyWell_LeavesItUnlinked()
    {
        var dbName = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid();
        var campaignId = Guid.NewGuid();

        using (var context = CreateContext(dbName, tenantId))
        {
            var field = new Field { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Establecimiento Norte" };
            var lot = new Lot { Id = Guid.NewGuid(), TenantId = tenantId, FieldId = field.Id, Name = "Lote 1" };
            context.Campaigns.Add(new Campaign { Id = campaignId, TenantId = tenantId, Name = "2026-2027", IsActive = true });
            context.Fields.Add(field);
            context.Lots.Add(lot);
            context.CampaignLots.Add(new CampaignLot { Id = Guid.NewGuid(), TenantId = tenantId, CampaignId = campaignId, LotId = lot.Id });
            context.Contacts.Add(new Contact { Id = Guid.NewGuid(), TenantId = tenantId, FullName = "PEREZ JUAN CARLOS", Role = ContactRole.Contractor });
            context.Contacts.Add(new Contact { Id = Guid.NewGuid(), TenantId = tenantId, FullName = "PEREZ JUAN ESTEBAN", Role = ContactRole.Contractor });
            await context.SaveChangesAsync();
        }

        using (var context = CreateContext(dbName, tenantId))
        {
            var service = new LaborExcelImportService(context, NullLogger<LaborExcelImportService>.Instance);
            using var stream = CreateContractorExcelStream("Juan Perez");

            var preview = await service.PreviewAsync(campaignId, stream);

            Assert.Null(Assert.Single(preview.Labors).ContactId);
        }
    }

    private static Stream CreateContractorExcelStream(string contractor)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Labores e Insumos");

        string[] headers = ["Fecha", "Establecimiento", "Lote", "Superficie (ha)", "Tipo", "Labor o Insumo", "Dosis", "Unidad", "Contratista", "Modo", "Notas"];
        for (int i = 0; i < headers.Length; i++)
            ws.Cell(1, i + 1).Value = headers[i];

        object[] row = ["2026-01-10", "Establecimiento Norte", "Lote 1", 50, "Labor", "Pulverización", 1, "ha", contractor, "", ""];
        for (int c = 0; c < row.Length; c++)
        {
            var cell = ws.Cell(2, c + 1);
            if (row[c] is int iVal) cell.Value = iVal;
            else cell.Value = row[c].ToString();
        }

        var ms = new MemoryStream();
        workbook.SaveAs(ms);
        ms.Position = 0;
        return ms;
    }
}
