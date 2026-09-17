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

/// <summary>
/// Subida directa de Excel (verdes se importan, resto a lote pendiente) y
/// resolución manual desde la sección Importaciones Pendientes.
/// </summary>
public class LaborImportBatchTests
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

    private static readonly string[] Headers =
        ["Fecha", "Establecimiento", "Lote", "Superficie (ha)", "Tipo", "Labor o Insumo", "Dosis", "Unidad", "Contratista", "Modo", "Notas"];

    private static MemoryStream BuildExcel(params object[][] rows)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Labores e Insumos");
        for (int i = 0; i < Headers.Length; i++)
            ws.Cell(1, i + 1).Value = Headers[i];
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

    private static object[] LaborRow(string date, string lot, string laborType, string contractor = "Propio", double ha = 50) =>
        [date, "Establecimiento Norte", lot, ha, "Labor", laborType, 1, "ha", contractor, "", ""];

    private static object[] SupplyRow(string date, string lot, string category, string supply, double dose, string unit, string supplier = "Propio", double ha = 50) =>
        [date, "Establecimiento Norte", lot, ha, category, supply, dose, unit, supplier, "", ""];

    private Guid SeedBase(ApplicationDbContext context, Guid tenantId, out Guid campaignId)
    {
        campaignId = Guid.NewGuid();
        context.Campaigns.Add(new Campaign { Id = campaignId, TenantId = tenantId, Name = "2026-2027", IsActive = true });
        var field = new Field { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Establecimiento Norte" };
        context.Fields.Add(field);
        var lot1 = new Lot { Id = Guid.NewGuid(), TenantId = tenantId, FieldId = field.Id, Name = "Lote 1" };
        var lot2 = new Lot { Id = Guid.NewGuid(), TenantId = tenantId, FieldId = field.Id, Name = "Lote 2" };
        context.Lots.AddRange(lot1, lot2);
        context.CampaignLots.AddRange(
            new CampaignLot { Id = Guid.NewGuid(), TenantId = tenantId, CampaignId = campaignId, LotId = lot1.Id },
            new CampaignLot { Id = Guid.NewGuid(), TenantId = tenantId, CampaignId = campaignId, LotId = lot2.Id });
        context.LaborTypes.Add(new LaborType { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Pulverización" });
        context.Inventories.Add(new Inventory { Id = Guid.NewGuid(), TenantId = tenantId, ItemName = "Glifosato 66%", Category = "Herbicida", Unit = "litros" });
        context.Contacts.Add(new Contact { Id = Guid.NewGuid(), TenantId = tenantId, FullName = "Don Carlos", Role = ContactRole.Contractor });
        context.SaveChanges();
        return campaignId;
    }

    [Fact]
    public async Task Upload_AllGreen_ImportsDirectlyWithoutBatch()
    {
        var dbName = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid();
        using var context = CreateContext(dbName, tenantId);
        var campaignId = SeedBase(context, tenantId, out _);
        var service = new LaborExcelImportService(context, NullLogger<LaborExcelImportService>.Instance);

        using var stream = BuildExcel(
            LaborRow("2026-01-10", "Lote 1", "Pulverización", "Don Carlos"),
            SupplyRow("2026-01-10", "Lote 1", "Herbicida", "Glifosato 66%", 2.5, "litros", "Don Carlos"));

        var result = await service.UploadAsync(campaignId, stream, "verde.xlsx", "tester");

        Assert.True(result.Success);
        Assert.Equal(1, result.LaborsCreated);
        Assert.Null(result.PendingBatchId);
        Assert.Equal(0, result.PendingRows);
        Assert.Equal(1, context.Labors.Count());
        var labor = context.Labors.Include(l => l.Supplies).Single();
        Assert.Equal(LaborStatus.Realized, labor.Status);
        Assert.Single(labor.Supplies);
    }

    [Fact]
    public async Task Upload_MixedFile_ImportsGreenAndPersistsPendingBatch()
    {
        var dbName = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid();
        using var context = CreateContext(dbName, tenantId);
        var campaignId = SeedBase(context, tenantId, out _);
        var service = new LaborExcelImportService(context, NullLogger<LaborExcelImportService>.Instance);

        using var stream = BuildExcel(
            // Verde: todo matchea
            LaborRow("2026-01-10", "Lote 1", "Pulverización"),
            SupplyRow("2026-01-10", "Lote 1", "Herbicida", "Glifosato 66%", 2.5, "litros"),
            // Pendiente: lote inexistente
            LaborRow("2026-01-11", "Lote Fantasma", "Pulverización"),
            // Pendiente: tipo sin vincular
            LaborRow("2026-01-12", "Lote 2", "Fertilización Voleo"),
            SupplyRow("2026-01-12", "Lote 2", "Fertilizante", "Urea Granulada", 120, "kg"),
            // Pendiente: proveedor sin vincular
            LaborRow("2026-01-13", "Lote 2", "Pulverización"),
            SupplyRow("2026-01-13", "Lote 2", "Herbicida", "Glifosato 66%", 2.5, "litros", "Distribuidora Fantasma"));

        var result = await service.UploadAsync(campaignId, stream, "mixto.xlsx", "tester");

        Assert.True(result.Success);
        Assert.Equal(1, result.LaborsCreated);
        Assert.NotNull(result.PendingBatchId);
        Assert.Equal(3, result.PendingRows);
        // Lo pendiente todavía no es ninguna labor
        Assert.Equal(1, context.Labors.Count());

        var detail = await service.GetBatchDetailAsync(result.PendingBatchId.Value);
        Assert.NotNull(detail);
        Assert.Equal(3, detail.Preview.TotalLabors);
        Assert.Equal(3, detail.RowStates.Count(s => s.Resolution == "Unresolved"));
        // La vista persistente trae las mismas pestañas de conciliación
        Assert.NotEmpty(detail.Preview.SupplyMappings);
        Assert.NotEmpty(detail.Preview.LaborTypeMappings);

        Assert.Equal(3, await service.GetPendingCountAsync(campaignId));
    }

    [Fact]
    public async Task Upload_SameFileTwice_WarnsDuplicateUnlessForced()
    {
        var dbName = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid();
        using var context = CreateContext(dbName, tenantId);
        var campaignId = SeedBase(context, tenantId, out _);
        var service = new LaborExcelImportService(context, NullLogger<LaborExcelImportService>.Instance);

        byte[] bytes;
        using (var s = BuildExcel(LaborRow("2026-01-11", "Lote Fantasma", "Pulverización")))
        {
            bytes = s.ToArray();
        }

        var first = await service.UploadAsync(campaignId, new MemoryStream(bytes), "dup.xlsx", "tester");
        Assert.NotNull(first.PendingBatchId);

        var second = await service.UploadAsync(campaignId, new MemoryStream(bytes), "dup.xlsx", "tester");
        Assert.Equal(first.PendingBatchId, second.DuplicateOfBatchId);
        Assert.False(second.Success);
        Assert.Equal(1, context.LaborImportBatches.Count());

        var forced = await service.UploadAsync(campaignId, new MemoryStream(bytes), "dup.xlsx", "tester", force: true);
        Assert.NotEqual(first.PendingBatchId, forced.PendingBatchId);
        Assert.Equal(2, context.LaborImportBatches.Count());
    }

    [Fact]
    public async Task BatchWorkflow_MapTypeThenImport_CompletesBatch()
    {
        var dbName = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid();
        using var context = CreateContext(dbName, tenantId);
        var campaignId = SeedBase(context, tenantId, out _);
        var service = new LaborExcelImportService(context, NullLogger<LaborExcelImportService>.Instance);

        using var stream = BuildExcel(
            LaborRow("2026-01-12", "Lote 2", "Fertilización Voleo"),
            SupplyRow("2026-01-12", "Lote 2", "Fertilizante", "Urea Granulada", 120, "kg"));

        var upload = await service.UploadAsync(campaignId, stream, "resolver.xlsx", "tester");
        Assert.NotNull(upload.PendingBatchId);
        Assert.Equal(0, context.Labors.Count());

        // 1. Guardar el match manual del tipo a un concepto del ERP
        var detail = await service.GetBatchDetailAsync(upload.PendingBatchId.Value);
        Assert.NotNull(detail);
        var pulvId = context.LaborTypes.Single(t => t.Name == "Pulverización").Id;
        var typeMap = detail.Preview.LaborTypeMappings.Single(m => m.RawName == "Fertilización Voleo");
        typeMap.MatchedLaborTypeId = pulvId;
        typeMap.MatchedLaborTypeName = "Pulverización";
        // El insumo Urea no existe: crearlo desde la conciliación
        var supplyMap = detail.Preview.SupplyMappings.Single(m => m.RawName == "Urea Granulada");
        supplyMap.Action = "CreateNew";
        supplyMap.NewItemName = "Urea Granulada";
        supplyMap.NewCategory = "Fertilizante";
        supplyMap.NewUnit = "kg";
        await service.SaveBatchMappingsAsync(upload.PendingBatchId.Value, new LaborImportBatchMappingsDto
        {
            SupplyMappings = detail.Preview.SupplyMappings,
            LaborTypeMappings = detail.Preview.LaborTypeMappings,
            SupplierMappings = detail.Preview.SupplierMappings
        });

        // 2. Importar lo pendiente ya matchable
        var resolve = await service.ImportBatchRowsAsync(upload.PendingBatchId.Value, null);

        Assert.True(resolve.Success);
        Assert.Equal(1, resolve.Imported);
        Assert.True(resolve.BatchCompleted);
        Assert.Equal(1, context.Labors.Count());
        var labor = context.Labors.Include(l => l.Supplies).Single();
        Assert.Equal(pulvId, labor.LaborTypeId);
        Assert.Single(labor.Supplies);
        Assert.Equal("Urea Granulada", context.Inventories.Single(i => i.ItemName == "Urea Granulada").ItemName);
        Assert.Equal(0, await service.GetPendingCountAsync(campaignId));
    }

    [Fact]
    public async Task BatchWorkflow_Discard_MarksExcludedWithoutCreatingLabors()
    {
        var dbName = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid();
        using var context = CreateContext(dbName, tenantId);
        var campaignId = SeedBase(context, tenantId, out _);
        var service = new LaborExcelImportService(context, NullLogger<LaborExcelImportService>.Instance);

        using var stream = BuildExcel(LaborRow("2026-01-11", "Lote Fantasma", "Pulverización"));
        var upload = await service.UploadAsync(campaignId, stream, "descartar.xlsx", "tester");
        Assert.NotNull(upload.PendingBatchId);

        var discard = await service.DiscardBatchRowsAsync(upload.PendingBatchId.Value, null);

        Assert.True(discard.Success);
        Assert.Equal(1, discard.Excluded);
        Assert.True(discard.BatchCompleted);
        Assert.Equal(0, context.Labors.Count());

        var detail = await service.GetBatchDetailAsync(upload.PendingBatchId.Value);
        Assert.NotNull(detail);
        Assert.Equal("Completed", detail.Batch.Status);
        Assert.Equal("Excluded", detail.RowStates.Single().Resolution);
    }

    [Fact]
    public async Task BatchWorkflow_Reevaluate_PicksUpNewlyCreatedContact()
    {
        var dbName = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid();
        using var context = CreateContext(dbName, tenantId);
        var campaignId = SeedBase(context, tenantId, out _);
        var service = new LaborExcelImportService(context, NullLogger<LaborExcelImportService>.Instance);

        using var stream = BuildExcel(
            LaborRow("2026-01-13", "Lote 2", "Pulverización", "Nuevo Proveedor SA"),
            SupplyRow("2026-01-13", "Lote 2", "Herbicida", "Glifosato 66%", 2.5, "litros"));
        var upload = await service.UploadAsync(campaignId, stream, "reevaluar.xlsx", "tester");
        Assert.NotNull(upload.PendingBatchId);
        Assert.Equal(1, upload.PendingRows);

        // Se da de alta el contacto en el padrón después de subir el archivo
        context.Contacts.Add(new Contact { Id = Guid.NewGuid(), TenantId = tenantId, FullName = "Nuevo Proveedor SA", Role = ContactRole.Contractor });
        context.SaveChanges();

        var detail = await service.ReevaluateBatchAsync(upload.PendingBatchId.Value);
        Assert.NotNull(detail);
        var row = detail.Preview.Labors.Single();
        Assert.NotNull(row.ContactId);
        Assert.Equal("Nuevo Proveedor SA", row.MatchedContactName);

        var resolve = await service.ImportBatchRowsAsync(upload.PendingBatchId.Value, null);
        Assert.True(resolve.Success);
        Assert.Equal(1, resolve.Imported);
    }
}
