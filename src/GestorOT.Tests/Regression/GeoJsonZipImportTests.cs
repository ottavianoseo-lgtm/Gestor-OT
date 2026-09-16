using System.IO.Compression;
using System.Security.Claims;
using System.Text;
using GestorOT.Domain.Entities;
using GestorOT.Infrastructure.Data;
using GestorOT.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GestorOT.Tests.Regression;

public class GeoJsonZipImportTests
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

    [Fact]
    public async Task Amand_ExcelYLuegoGisZip_VinculaGeometriasCorrectamente()
    {
        var excelPath = Path.Combine(Directory.GetCurrentDirectory(), "Gestor-OT_Importacion_AMAND_26-27_UUID.xlsx");
        var zipPath = Path.Combine(Directory.GetCurrentDirectory(), "Gestor-OT_GIS_AMAND_26-27_UUID.zip");

        if (!File.Exists(excelPath) || !File.Exists(zipPath))
            return; // Skip if run in environment without local sample files

        var dbName = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid();
        var campaignId = Guid.NewGuid();

        // 1. Setup tenant & campaign, import Excel
        using (var context = CreateContext(dbName, tenantId))
        {
            context.Tenants.Add(new Tenant { Id = tenantId, Name = "Tenant AMAND" });
            context.Campaigns.Add(new Campaign { Id = campaignId, Name = "AMAND 26-27", TenantId = tenantId });
            await context.SaveChangesAsync();

            var excelService = new LotExcelImportService(context, NullLogger<LotExcelImportService>.Instance);
            using var excelStream = File.OpenRead(excelPath);
            var excelResult = await excelService.ExecuteAsync(campaignId, excelStream);
            Assert.True(excelResult.Success);
            Assert.Equal(83, excelResult.LotsCreated);
        }

        // 2. Preview GeoJSON ZIP
        using (var context = CreateContext(dbName, tenantId))
        {
            var zipService = new GeoJsonZipImportService(context, NullLogger<GeoJsonZipImportService>.Instance);
            using var zipStream = File.OpenRead(zipPath);

            var preview = await zipService.PreviewZipAsync(zipStream);

            Assert.Equal(58, preview.TotalInZip);
            Assert.Equal(58, preview.MatchedCount);
            Assert.Equal(0, preview.UnmatchedCount);
            Assert.Equal(8, preview.FieldsCovered.Count);

            // Verify items have geometry and area
            Assert.All(preview.Items, item =>
            {
                Assert.True(item.LotFound);
                Assert.NotNull(item.LotId);
                Assert.True(item.GisAreaHa > 0);
            });
        }

        // 3. Apply GeoJSON ZIP
        using (var context = CreateContext(dbName, tenantId))
        {
            var zipService = new GeoJsonZipImportService(context, NullLogger<GeoJsonZipImportService>.Instance);
            using var zipStream = File.OpenRead(zipPath);

            var preview = await zipService.PreviewZipAsync(zipStream);
            var applyRequest = new GestorOT.Shared.Dtos.GeoJsonZipApplyRequestDto(
                preview.Items.Where(i => i.LotFound && i.LotId.HasValue)
                             .Select(i => new GestorOT.Shared.Dtos.GeoJsonZipApplyItemDto(i.LotId!.Value, i.Wkt, i.GisAreaHa))
                             .ToList(),
                campaignId
            );

            var applyResult = await zipService.ApplyAsync(applyRequest);

            Assert.True(applyResult.Success);
            Assert.Equal(58, applyResult.LinkedCount);
        }

        // 4. Assert lots in database have geometries with SRID 4326
        using (var context = CreateContext(dbName, tenantId))
        {
            var lotsWithGeometry = await context.Lots.Where(l => l.Geometry != null).ToListAsync();
            Assert.Equal(58, lotsWithGeometry.Count);

            var lotsWithoutGeometry = await context.Lots.Where(l => l.Geometry == null).ToListAsync();
            Assert.Equal(25, lotsWithoutGeometry.Count); // 83 - 58 = 25 lots intentionally without GIS

            Assert.All(lotsWithGeometry, lot =>
            {
                Assert.Equal(4326, lot.Geometry!.SRID);
                Assert.True(lot.Geometry.IsValid);
            });
        }
    }

    [Fact]
    public async Task ZipConLoteNoExistente_ReportaUnmatched()
    {
        var dbName = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid();

        // Create an in-memory ZIP with a geojson that has an unknown lote_id
        using var memoryZipStream = new MemoryStream();
        using (var archive = new ZipArchive(memoryZipStream, ZipArchiveMode.Create, true))
        {
            var entry = archive.CreateEntry("unknown.geojson");
            using var entryStream = entry.Open();
            var json = """
            {
              "type": "Feature",
              "properties": {
                "lote_id": "11111111-2222-3333-4444-555555555555",
                "campo": "Campo Test",
                "lote": "Lote Test"
              },
              "geometry": {
                "type": "Polygon",
                "coordinates": [
                  [ [-60.0, -35.0], [-60.0, -35.1], [-60.1, -35.1], [-60.1, -35.0], [-60.0, -35.0] ]
                ]
              }
            }
            """;
            var bytes = Encoding.UTF8.GetBytes(json);
            entryStream.Write(bytes, 0, bytes.Length);
        }
        memoryZipStream.Position = 0;

        using (var context = CreateContext(dbName, tenantId))
        {
            var zipService = new GeoJsonZipImportService(context, NullLogger<GeoJsonZipImportService>.Instance);
            var preview = await zipService.PreviewZipAsync(memoryZipStream);

            Assert.Equal(1, preview.TotalInZip);
            Assert.Equal(0, preview.MatchedCount);
            Assert.Equal(1, preview.UnmatchedCount);
            Assert.False(preview.Items[0].LotFound);
            Assert.Contains("No se encontró ningún lote", preview.Items[0].Warning);
        }
    }
}
