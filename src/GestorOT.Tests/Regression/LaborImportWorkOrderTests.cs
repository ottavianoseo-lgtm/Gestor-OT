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
/// La planilla de operativos agrupa labores por "Nro OT": cada número es una Orden de
/// Trabajo con su responsable, y el estado ORDEN es una labor planeada aunque la fecha
/// ya haya pasado (se postergó por clima y todavía no se hizo).
/// </summary>
public class LaborImportWorkOrderTests
{
    private static ApplicationDbContext CreateContext(string dbName, Guid tenantId)
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

    private static Stream CreateWorkbook()
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Labores e Insumos");
        string[] headers = ["Nro OT", "Fecha", "Establecimiento", "Lote", "Superficie (ha)", "Tipo", "Labor o Insumo", "Dosis", "Unidad", "Contratista / Maquinaria", "Responsable", "Modo", "Notas"];
        for (int i = 0; i < headers.Length; i++) ws.Cell(1, i + 1).Value = headers[i];

        var rows = new object[][]
        {
            new object[] { "711", "2026-04-09", "La Palma", "LP-P10", 4.6, "Labor", "Siembra", 1, "ha", "Propio", "FARIAS WALTER", "Realizada", "" },
            new object[] { "711", "2026-04-09", "La Palma", "LP-P10", 4.6, "Insumo", "MAP", 80, "kg", "", "", "Realizada", "" },
            new object[] { "711", "2026-04-10", "La Palma", "LP-P11", 4.5, "Labor", "Siembra", 1, "ha", "Propio", "FARIAS WALTER", "Realizada", "" },
            new object[] { "711", "2026-04-10", "La Palma", "LP-P11", 4.5, "Insumo", "MAP", 80, "kg", "", "", "Realizada", "" },
            new object[] { "767", "2026-09-01", "La Palma", "LP-P10", 4.6, "Labor", "Siembra", 1, "ha", "Propio", "LAGOS FRANCO", "Planeada", "" },
        };
        for (int r = 0; r < rows.Length; r++)
            for (int c = 0; c < rows[r].Length; c++)
            {
                var v = rows[r][c];
                var cell = ws.Cell(r + 2, c + 1);
                if (v is int i) cell.Value = i; else if (v is double d) cell.Value = d; else cell.Value = v.ToString();
            }

        var ms = new MemoryStream();
        wb.SaveAs(ms);
        ms.Position = 0;
        return ms;
    }

    private static async Task SeedAsync(string dbName, Guid tenantId, Guid campaignId, Guid fieldId, params object[] extra)
    {
        using var context = CreateContext(dbName, tenantId);
        var lot10 = new Lot { Id = Guid.NewGuid(), TenantId = tenantId, FieldId = fieldId, Name = "LP-P10" };
        var lot11 = new Lot { Id = Guid.NewGuid(), TenantId = tenantId, FieldId = fieldId, Name = "LP-P11" };
        context.Campaigns.Add(new Campaign { Id = campaignId, TenantId = tenantId, Name = "2026-2027", IsActive = true });
        context.Fields.Add(new Field { Id = fieldId, TenantId = tenantId, Name = "La Palma" });
        context.Lots.AddRange(lot10, lot11);
        context.CampaignLots.AddRange(
            new CampaignLot { Id = Guid.NewGuid(), TenantId = tenantId, CampaignId = campaignId, LotId = lot10.Id },
            new CampaignLot { Id = Guid.NewGuid(), TenantId = tenantId, CampaignId = campaignId, LotId = lot11.Id });
        context.Inventories.Add(new Inventory { Id = Guid.NewGuid(), TenantId = tenantId, ItemName = "MAP", Category = "Fertilizante", Unit = "kg" });
        context.LaborTypes.Add(new LaborType { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Siembra", ExternalErpId = "ERP-1" });
        foreach (var e in extra) context.Add(e);
        await context.SaveChangesAsync();
    }

    private static async Task ImportAsync(string dbName, Guid tenantId, Guid campaignId)
    {
        using var context = CreateContext(dbName, tenantId);
        var service = new LaborExcelImportService(context, NullLogger<LaborExcelImportService>.Instance);
        using var stream = CreateWorkbook();
        var preview = await service.PreviewAsync(campaignId, stream);
        stream.Position = 0;
        await service.ExecuteAsync(campaignId, stream, preview.SupplyMappings, preview.LaborTypeMappings);
    }

    [Fact]
    public async Task PreviewAsync_ReadsWorkOrderNumberResponsibleAndExplicitPlannedMode()
    {
        var dbName = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid();
        var campaignId = Guid.NewGuid();
        await SeedAsync(dbName, tenantId, campaignId, Guid.NewGuid());

        using var context = CreateContext(dbName, tenantId);
        var service = new LaborExcelImportService(context, NullLogger<LaborExcelImportService>.Instance);
        using var stream = CreateWorkbook();
        var preview = await service.PreviewAsync(campaignId, stream);

        Assert.Equal(3, preview.TotalLabors);
        Assert.Equal("711", preview.Labors[0].WorkOrderNumber);
        Assert.Equal("FARIAS WALTER", preview.Labors[0].WorkOrderResponsible);
        Assert.Equal("Realized", preview.Labors[0].Mode);
        Assert.Equal("767", preview.Labors[2].WorkOrderNumber);
        Assert.Equal("Planned", preview.Labors[2].Mode);
    }

    [Fact]
    public async Task ExecuteAsync_GroupsLaborsIntoWorkOrdersAndRespectsPlannedMode()
    {
        var dbName = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid();
        var campaignId = Guid.NewGuid();
        var fieldId = Guid.NewGuid();
        var farias = new Contact { Id = Guid.NewGuid(), TenantId = tenantId, FullName = "FARIAS WALTER" };
        var status = new WorkOrderStatus { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Borrador", IsDefault = true };
        await SeedAsync(dbName, tenantId, campaignId, fieldId, farias, status);

        await ImportAsync(dbName, tenantId, campaignId);

        using var context = CreateContext(dbName, tenantId);
        var orders = await context.WorkOrders.Include(w => w.Labors).OrderBy(w => w.OTNumber).ToListAsync();
        Assert.Equal(2, orders.Count);

        var ot711 = orders[0];
        Assert.Equal("711", ot711.OTNumber);
        Assert.Equal(campaignId, ot711.CampaignId);
        Assert.Equal(fieldId, ot711.FieldId);
        Assert.Equal(farias.Id, ot711.ContactId);
        Assert.Equal("FARIAS WALTER", ot711.AssignedTo);
        Assert.Equal("Borrador", ot711.Status);
        Assert.Equal(new DateTime(2026, 4, 9), ot711.PlannedDate.Date);
        Assert.Equal(new DateTime(2026, 4, 10), ot711.ExpirationDate.Date);
        Assert.True(ot711.AcceptsMultipleDates);
        Assert.Equal(2, ot711.Labors.Count);
        Assert.All(ot711.Labors, l => Assert.Equal(LaborMode.Realized, l.Mode));

        // Fecha pasada pero la planilla dice Planeada: no se ejecutó todavía.
        var ot767 = orders[1];
        var planned = Assert.Single(ot767.Labors);
        Assert.Equal(LaborMode.Planned, planned.Mode);
        Assert.Equal(LaborStatus.Planned, planned.Status);
        Assert.Null(planned.RealizedDose);
        Assert.Null(ot767.ContactId);
        Assert.Equal("LAGOS FRANCO", ot767.AssignedTo);
    }

    [Fact]
    public async Task PreviewAsync_SameLotNameInTwoFieldsMatchesByField()
    {
        var dbName = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid();
        var campaignId = Guid.NewGuid();
        var manga = new Field { Id = Guid.NewGuid(), TenantId = tenantId, Name = "La Manga" };
        var federico = new Field { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Don Federico" };
        var lotManga = new Lot { Id = Guid.NewGuid(), TenantId = tenantId, FieldId = manga.Id, Name = "2" };
        var lotFederico = new Lot { Id = Guid.NewGuid(), TenantId = tenantId, FieldId = federico.Id, Name = "2" };

        using (var context = CreateContext(dbName, tenantId))
        {
            context.Campaigns.Add(new Campaign { Id = campaignId, TenantId = tenantId, Name = "2026-2027", IsActive = true });
            context.Fields.AddRange(manga, federico);
            context.Lots.AddRange(lotManga, lotFederico);
            context.CampaignLots.AddRange(
                new CampaignLot { Id = Guid.NewGuid(), TenantId = tenantId, CampaignId = campaignId, LotId = lotManga.Id },
                new CampaignLot { Id = Guid.NewGuid(), TenantId = tenantId, CampaignId = campaignId, LotId = lotFederico.Id });
            await context.SaveChangesAsync();
        }

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Labores e Insumos");
        string[] headers = ["Nro OT", "Fecha", "Establecimiento", "Lote", "Superficie (ha)", "Tipo", "Labor o Insumo"];
        for (int i = 0; i < headers.Length; i++) ws.Cell(1, i + 1).Value = headers[i];
        ws.Cell(2, 1).Value = "1"; ws.Cell(2, 2).Value = "2026-04-09"; ws.Cell(2, 3).Value = "Don Federico"; ws.Cell(2, 4).Value = "2"; ws.Cell(2, 5).Value = 10; ws.Cell(2, 6).Value = "Labor"; ws.Cell(2, 7).Value = "Siembra";
        ws.Cell(3, 1).Value = "1"; ws.Cell(3, 2).Value = "2026-04-09"; ws.Cell(3, 3).Value = "La Manga"; ws.Cell(3, 4).Value = "2"; ws.Cell(3, 5).Value = 10; ws.Cell(3, 6).Value = "Labor"; ws.Cell(3, 7).Value = "Siembra";
        ws.Cell(4, 1).Value = "1"; ws.Cell(4, 2).Value = "2026-04-09"; ws.Cell(4, 3).Value = "Otro"; ws.Cell(4, 4).Value = "2"; ws.Cell(4, 5).Value = 10; ws.Cell(4, 6).Value = "Labor"; ws.Cell(4, 7).Value = "Siembra";
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        ms.Position = 0;

        using (var context = CreateContext(dbName, tenantId))
        {
            var service = new LaborExcelImportService(context, NullLogger<LaborExcelImportService>.Instance);
            var preview = await service.PreviewAsync(campaignId, ms);
            Assert.Equal(lotFederico.Id, preview.Labors[0].LotId);
            Assert.Equal(lotManga.Id, preview.Labors[1].LotId);
            Assert.Null(preview.Labors[2].LotId);
            Assert.NotEmpty(preview.Labors[2].Errors);
        }
    }

    [Fact]
    public async Task ExecuteAsync_SameLotDateAndTypeInTwoWorkOrdersCreatesTwoLabors()
    {
        var dbName = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid();
        var campaignId = Guid.NewGuid();
        await SeedAsync(dbName, tenantId, campaignId, Guid.NewGuid());

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Labores e Insumos");
        string[] headers = ["Nro OT", "Fecha", "Establecimiento", "Lote", "Superficie (ha)", "Tipo", "Labor o Insumo", "Dosis", "Unidad", "Modo"];
        for (int i = 0; i < headers.Length; i++) ws.Cell(1, i + 1).Value = headers[i];
        object[][] rows =
        [
            ["755", "2026-08-25", "La Palma", "LP-P10", 4.6, "Labor", "Siembra", 1, "ha", "Realizada"],
            ["755", "2026-08-25", "La Palma", "LP-P10", 4.6, "Insumo", "MAP", 80, "kg", "Realizada"],
            ["760", "2026-08-25", "La Palma", "LP-P10", 4.6, "Labor", "Siembra", 1, "ha", "Realizada"],
            ["760", "2026-08-25", "La Palma", "LP-P10", 4.6, "Insumo", "MAP", 50, "kg", "Realizada"],
        ];
        for (int r = 0; r < rows.Length; r++)
            for (int c = 0; c < rows[r].Length; c++)
            {
                var v = rows[r][c];
                var cell = ws.Cell(r + 2, c + 1);
                if (v is int i) cell.Value = i; else if (v is double d) cell.Value = d; else cell.Value = v.ToString();
            }
        using var ms = new MemoryStream();
        wb.SaveAs(ms);

        using (var context = CreateContext(dbName, tenantId))
        {
            var service = new LaborExcelImportService(context, NullLogger<LaborExcelImportService>.Instance);
            ms.Position = 0;
            var preview = await service.PreviewAsync(campaignId, ms);
            ms.Position = 0;
            var result = await service.ExecuteAsync(campaignId, ms, preview.SupplyMappings, preview.LaborTypeMappings);
            Assert.Equal(2, result.LaborsCreated);
            Assert.Equal(0, result.LaborsUpdated);
        }

        using (var context = CreateContext(dbName, tenantId))
        {
            var labors = await context.Labors.Include(l => l.Supplies).Include(l => l.WorkOrder).ToListAsync();
            Assert.Equal(2, labors.Count);
            Assert.Equal(80, Assert.Single(labors.Single(l => l.WorkOrder!.OTNumber == "755").Supplies).PlannedDose);
            Assert.Equal(50, Assert.Single(labors.Single(l => l.WorkOrder!.OTNumber == "760").Supplies).PlannedDose);
        }
    }

    /// <summary>
    /// Es el camino de la pantalla: al subir, lo que no hizo full match queda como fila
    /// pendiente en la base y se importa después. La fila pendiente no guardaba el
    /// Nro OT, el responsable ni el modo: las labores entraban sin OT y como realizadas.
    /// </summary>
    [Fact]
    public async Task UploadThenImportPendingRows_KeepsWorkOrderResponsibleAndPlannedMode()
    {
        var dbName = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid();
        var campaignId = Guid.NewGuid();
        var status = new WorkOrderStatus { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Borrador", IsDefault = true };
        await SeedAsync(dbName, tenantId, campaignId, Guid.NewGuid(), status);

        // Sin el tipo de labor en el catálogo, ninguna fila es verde: todas quedan pendientes.
        using (var context = CreateContext(dbName, tenantId))
        {
            context.LaborTypes.RemoveRange(context.LaborTypes);
            await context.SaveChangesAsync();
        }

        Guid batchId;
        using (var context = CreateContext(dbName, tenantId))
        {
            var service = new LaborExcelImportService(context, NullLogger<LaborExcelImportService>.Instance);
            using var stream = CreateWorkbook();
            var upload = await service.UploadAsync(campaignId, stream, "operativos.xlsx", "tester");
            Assert.NotNull(upload.PendingBatchId);
            batchId = upload.PendingBatchId.Value;
            Assert.Equal(0, context.Labors.Count());
        }

        using (var context = CreateContext(dbName, tenantId))
        {
            var siembra = new LaborType { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Siembra ERP", ExternalErpId = "ERP-1" };
            context.LaborTypes.Add(siembra);
            await context.SaveChangesAsync();

            var service = new LaborExcelImportService(context, NullLogger<LaborExcelImportService>.Instance);
            var detail = await service.GetBatchDetailAsync(batchId);
            Assert.NotNull(detail);
            Assert.Equal("711", detail.Preview.Labors[0].WorkOrderNumber);
            Assert.Equal("Planned", detail.Preview.Labors[2].Mode);

            var typeMap = detail.Preview.LaborTypeMappings.Single(m => m.RawName == "Siembra");
            typeMap.MatchedLaborTypeId = siembra.Id;
            typeMap.MatchedLaborTypeName = siembra.Name;
            await service.SaveBatchMappingsAsync(batchId, new LaborImportBatchMappingsDto
            {
                SupplyMappings = detail.Preview.SupplyMappings,
                LaborTypeMappings = detail.Preview.LaborTypeMappings,
                SupplierMappings = detail.Preview.SupplierMappings
            });

            var resolve = await service.ImportBatchRowsAsync(batchId, null);
            Assert.True(resolve.Success);
            Assert.Equal(3, resolve.Imported);
        }

        using (var context = CreateContext(dbName, tenantId))
        {
            var orders = await context.WorkOrders.Include(w => w.Labors).ThenInclude(l => l.Supplies).OrderBy(w => w.OTNumber).ToListAsync();
            Assert.Equal(2, orders.Count);
            Assert.Equal("711", orders[0].OTNumber);
            Assert.Equal("FARIAS WALTER", orders[0].AssignedTo);
            Assert.Equal(campaignId, orders[0].CampaignId);
            Assert.Equal(2, orders[0].Labors.Count);
            Assert.All(orders[0].Labors, l => Assert.Single(l.Supplies));
            var planned = Assert.Single(orders[1].Labors);
            Assert.Equal(LaborMode.Planned, planned.Mode);
        }
    }

    [Fact]
    public async Task ExecuteAsync_ReimportReusesExistingWorkOrder()
    {
        var dbName = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid();
        var campaignId = Guid.NewGuid();
        await SeedAsync(dbName, tenantId, campaignId, Guid.NewGuid());

        await ImportAsync(dbName, tenantId, campaignId);
        await ImportAsync(dbName, tenantId, campaignId);

        using var context = CreateContext(dbName, tenantId);
        Assert.Equal(2, await context.WorkOrders.CountAsync());
        Assert.Equal(3, await context.Labors.CountAsync());
        Assert.All(await context.Labors.ToListAsync(), l => Assert.NotNull(l.WorkOrderId));
    }
}
