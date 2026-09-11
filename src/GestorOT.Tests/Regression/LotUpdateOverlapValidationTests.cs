using GestorOT.Api.Controllers;
using GestorOT.Application.Services;
using GestorOT.Domain.Entities;
using GestorOT.Infrastructure.Data;
using GestorOT.Shared.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace GestorOT.Tests.Regression;

public class LotUpdateOverlapValidationTests
{
    private const string NewWkt = "POLYGON ((-63.5 -31.5, -63.5 -31.6, -63.6 -31.6, -63.6 -31.5, -63.5 -31.5))";

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    private static LotsController CreateController(ApplicationDbContext context, Mock<ILotQueryService> queryService)
    {
        return new LotsController(
            context,
            queryService.Object,
            new Mock<IShapefileImportService>().Object,
            new Mock<ILotBulkLinkService>().Object);
    }

    private static async Task<(ApplicationDbContext Context, Lot Lot)> SeedLotAsync()
    {
        var context = CreateContext();
        var lot = new Lot
        {
            Id = Guid.NewGuid(),
            FieldId = Guid.NewGuid(),
            Name = "Lote 1",
            Status = "Active",
            CadastralArea = 42m
        };

        context.Lots.Add(lot);
        await context.SaveChangesAsync();
        return (context, lot);
    }

    [Fact]
    public async Task UpdateLot_ConOverlapYSinOverride_DevuelveConflictYNoPersisteGeometria()
    {
        var (context, lot) = await SeedLotAsync();
        var query = new Mock<ILotQueryService>();
        query.Setup(s => s.CheckLotOverlapAsync(NewWkt, lot.FieldId, lot.Id, It.IsAny<CancellationToken>()))
             .ReturnsAsync(new LotOverlapCheckResultDto(true, false, 30, Guid.NewGuid(), "Otro lote"));

        var controller = CreateController(context, query);
        var dto = new LotDto(lot.Id, lot.FieldId, "Lote 1", "Active", NewWkt, null, 0, 0);

        var result = await controller.UpdateLot(lot.Id, dto);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        Assert.IsType<LotOverlapCheckResultDto>(conflict.Value);

        var persisted = await context.Lots.AsNoTracking().FirstAsync(l => l.Id == lot.Id);
        Assert.Null(persisted.Geometry);
        Assert.Equal(42m, persisted.CadastralArea);
    }

    [Fact]
    public async Task UpdateLot_ExcluyeElPropioLoteAlChequearElOverlap()
    {
        var (context, lot) = await SeedLotAsync();
        var query = new Mock<ILotQueryService>();
        query.Setup(s => s.CheckLotOverlapAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(new LotOverlapCheckResultDto(false, false, 0));

        var controller = CreateController(context, query);
        var dto = new LotDto(lot.Id, lot.FieldId, "Lote 1", "Active", NewWkt, null, 0, 0);

        await controller.UpdateLot(lot.Id, dto);

        query.Verify(s => s.CheckLotOverlapAsync(NewWkt, lot.FieldId, lot.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateLot_ConOverride_NoChequeaOverlapYPersisteLaGeometria()
    {
        var (context, lot) = await SeedLotAsync();
        var query = new Mock<ILotQueryService>();
        query.Setup(s => s.CalculateAreaFromWktAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(76.0);

        var controller = CreateController(context, query);
        var dto = new LotDto(lot.Id, lot.FieldId, "Lote 1", "Active", NewWkt, null, 0, 0);

        var result = await controller.UpdateLot(lot.Id, dto, overrideOverlap: true);

        Assert.IsType<NoContentResult>(result);
        query.Verify(s => s.CheckLotOverlapAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Never);

        var persisted = await context.Lots.AsNoTracking().FirstAsync(l => l.Id == lot.Id);
        Assert.NotNull(persisted.Geometry);
        Assert.Equal(76m, persisted.CadastralArea);
    }

    [Fact]
    public async Task CreateLot_ConOverlapYsinOverride_DevuelveConflict()
    {
        var context = CreateContext();
        var fieldId = Guid.NewGuid();
        var query = new Mock<ILotQueryService>();
        query.Setup(s => s.CheckLotOverlapAsync(NewWkt, fieldId, null, It.IsAny<CancellationToken>()))
             .ReturnsAsync(new LotOverlapCheckResultDto(true, true, 100, Guid.NewGuid(), "Lote existente"));

        var controller = CreateController(context, query);
        var dto = new LotDto(Guid.Empty, fieldId, "Nuevo", "Active", NewWkt, null, 0, 0);

        var result = await controller.CreateLot(dto, null);

        var conflict = Assert.IsType<ConflictObjectResult>(result.Result);
        Assert.IsType<LotOverlapCheckResultDto>(conflict.Value);
        Assert.Empty(await context.Lots.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task CreateLot_ConOverride_PermiteCrear()
    {
        var context = CreateContext();
        var fieldId = Guid.NewGuid();
        var query = new Mock<ILotQueryService>();
        query.Setup(s => s.CalculateAreaFromWktAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(30.0);

        var controller = CreateController(context, query);
        var dto = new LotDto(Guid.Empty, fieldId, "Nuevo", "Active", NewWkt, null, 0, 0);

        var result = await controller.CreateLot(dto, null, overrideOverlap: true);

        Assert.IsType<CreatedAtActionResult>(result.Result);
        query.Verify(s => s.CheckLotOverlapAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Single(await context.Lots.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task CalculateArea_DevuelveLaSuperficieQueInformaElServicio()
    {
        var context = CreateContext();
        var query = new Mock<ILotQueryService>();
        query.Setup(s => s.CalculateAreaFromWktAsync(NewWkt, It.IsAny<CancellationToken>()))
             .ReturnsAsync(12.5);

        var controller = CreateController(context, query);

        var result = await controller.CalculateArea(new CheckLotOverlapRequestDto(NewWkt, Guid.Empty));

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var area = Assert.IsType<LotAreaResult>(ok.Value);
        Assert.Equal(12.5, area.AreaHa);
    }

    [Fact]
    public async Task CalculateArea_SinGeometria_DevuelveBadRequest()
    {
        var context = CreateContext();
        var query = new Mock<ILotQueryService>();
        var controller = CreateController(context, query);

        var result = await controller.CalculateArea(new CheckLotOverlapRequestDto("", Guid.Empty));

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }
}
