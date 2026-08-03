using GestorOT.Domain.Entities;
using GestorOT.Infrastructure.Data;
using GestorOT.Infrastructure.Services;
using GestorOT.Shared.Dtos;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.IO;
using Xunit;

namespace GestorOT.Tests.Regression;

public class LotSpatialOverlapTests
{
    private ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task CheckLotOverlapAsync_DetectsExactDuplicateGeometry()
    {
        var context = CreateContext();
        var service = new LotQueryService(context);

        var fieldId = Guid.NewGuid();
        var wkt = "POLYGON ((-63.5 -31.5, -63.5 -31.6, -63.6 -31.6, -63.6 -31.5, -63.5 -31.5))";

        var reader = new WKTReader();
        var existingLot = new Lot
        {
            Id = Guid.NewGuid(),
            FieldId = fieldId,
            Name = "Lote Existente Norte",
            Status = "Active",
            Geometry = reader.Read(wkt),
            CadastralArea = 100m
        };

        context.Lots.Add(existingLot);
        await context.SaveChangesAsync();

        var result = await service.CheckLotOverlapAsync(wkt, fieldId);

        Assert.True(result.HasOverlap);
        Assert.True(result.IsExactDuplicate);
        Assert.Equal(100.0, result.OverlapPercentage);
        Assert.Equal(existingLot.Id, result.ExistingLotId);
        Assert.Equal("Lote Existente Norte", result.ExistingLotName);
    }

    [Fact]
    public async Task CheckLotOverlapAsync_ReturnsNoOverlapForDisjointGeometries()
    {
        var context = CreateContext();
        var service = new LotQueryService(context);

        var fieldId = Guid.NewGuid();
        var existingWkt = "POLYGON ((-63.5 -31.5, -63.5 -31.6, -63.6 -31.6, -63.6 -31.5, -63.5 -31.5))";
        var disjointWkt = "POLYGON ((-64.5 -32.5, -64.5 -32.6, -64.6 -32.6, -64.6 -32.5, -64.5 -32.5))";

        var reader = new WKTReader();
        context.Lots.Add(new Lot
        {
            Id = Guid.NewGuid(),
            FieldId = fieldId,
            Name = "Lote Existente",
            Status = "Active",
            Geometry = reader.Read(existingWkt),
            CadastralArea = 100m
        });
        await context.SaveChangesAsync();

        var result = await service.CheckLotOverlapAsync(disjointWkt, fieldId);

        Assert.False(result.HasOverlap);
        Assert.False(result.IsExactDuplicate);
        Assert.Equal(0, result.OverlapPercentage);
    }

    [Fact]
    public async Task CheckLotOverlapAsync_IgnoresExcludedLotId()
    {
        var context = CreateContext();
        var service = new LotQueryService(context);

        var fieldId = Guid.NewGuid();
        var lotId = Guid.NewGuid();
        var wkt = "POLYGON ((-63.5 -31.5, -63.5 -31.6, -63.6 -31.6, -63.6 -31.5, -63.5 -31.5))";

        var reader = new WKTReader();
        context.Lots.Add(new Lot
        {
            Id = lotId,
            FieldId = fieldId,
            Name = "Lote Propio",
            Status = "Active",
            Geometry = reader.Read(wkt),
            CadastralArea = 100m
        });
        await context.SaveChangesAsync();

        var result = await service.CheckLotOverlapAsync(wkt, fieldId, excludeLotId: lotId);

        Assert.False(result.HasOverlap);
    }
}
