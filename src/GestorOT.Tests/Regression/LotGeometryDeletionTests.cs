using GestorOT.Api.Controllers;
using GestorOT.Application.Interfaces;
using GestorOT.Application.Services;
using GestorOT.Domain.Entities;
using GestorOT.Infrastructure.Data;
using GestorOT.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using NetTopologySuite.IO;
using Xunit;

namespace GestorOT.Tests.Regression;

/// <summary>
/// El borrado de geometría no existía: solo se podía limpiar el polígono recién dibujado en el
/// mapa, nunca quitarle la geometría a un lote guardado. Estos tests fijan el contrato del
/// endpoint nuevo, incluido el caso de campaña activa: con campaña el mapa prefiere el polígono
/// del año (OT-49), así que si no se borrara también ese, el borrado parecería no hacer nada.
/// </summary>
public class LotGeometryDeletionTests
{
    private static readonly WKTReader Reader = new();
    private const string Wkt = "POLYGON ((-63.5 -31.5, -63.5 -31.6, -63.6 -31.6, -63.6 -31.5, -63.5 -31.5))";

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    private static LotsController CreateController(ApplicationDbContext context, Guid? campaignId)
    {
        var campaignContext = new Mock<ICampaignContextService>();
        campaignContext.SetupGet(c => c.CurrentCampaignId).Returns(campaignId);

        var query = new Mock<ILotQueryService>();

        return new LotsController(
            context,
            query.Object,
            new Mock<IShapefileImportService>().Object,
            new Mock<ILotBulkLinkService>().Object,
            new Mock<IGeoJsonZipImportService>().Object,
            new CampaignGeometryService(context, query.Object),
            campaignContext.Object);
    }

    private static async Task<(ApplicationDbContext Context, Lot Lot, CampaignLot Cl, Guid CampaignId)> SeedAsync()
    {
        var context = CreateContext();
        var campaignId = Guid.NewGuid();

        var lot = new Lot
        {
            Id = Guid.NewGuid(),
            FieldId = Guid.NewGuid(),
            Name = "Lote 1",
            Status = "Active",
            Geometry = Reader.Read(Wkt),
            CadastralArea = 42m
        };

        var cl = new CampaignLot
        {
            Id = Guid.NewGuid(),
            CampaignId = campaignId,
            LotId = lot.Id,
            ProductiveArea = 10m,
            Geometry = Reader.Read(Wkt)
        };

        context.Lots.Add(lot);
        context.CampaignLots.Add(cl);
        await context.SaveChangesAsync();

        return (context, lot, cl, campaignId);
    }

    [Fact]
    public async Task DeleteLotGeometry_QuitaLaDelLoteYLaDeLaCampanaActiva()
    {
        var (context, lot, cl, campaignId) = await SeedAsync();
        var controller = CreateController(context, campaignId);

        var result = await controller.DeleteLotGeometry(lot.Id);

        Assert.IsType<OkObjectResult>(result);

        var persistedLot = await context.Lots.AsNoTracking().FirstAsync(l => l.Id == lot.Id);
        Assert.Null(persistedLot.Geometry);
        // La catastral se conserva: es el mejor dato disponible sin polígono.
        Assert.Equal(42m, persistedLot.CadastralArea);

        var persistedCl = await context.CampaignLots.AsNoTracking().FirstAsync(x => x.Id == cl.Id);
        Assert.Null(persistedCl.Geometry);
    }

    [Fact]
    public async Task DeleteLotGeometry_NoTocaLasOtrasCampanas()
    {
        var (context, lot, _, campaignId) = await SeedAsync();

        var otraCampana = Guid.NewGuid();
        var otroCl = new CampaignLot
        {
            Id = Guid.NewGuid(),
            CampaignId = otraCampana,
            LotId = lot.Id,
            ProductiveArea = 5m,
            Geometry = Reader.Read(Wkt)
        };
        context.CampaignLots.Add(otroCl);
        await context.SaveChangesAsync();

        var controller = CreateController(context, campaignId);
        await controller.DeleteLotGeometry(lot.Id);

        var persistido = await context.CampaignLots.AsNoTracking().FirstAsync(x => x.Id == otroCl.Id);
        Assert.NotNull(persistido.Geometry);
    }

    [Fact]
    public async Task DeleteLotGeometry_SinCampanaActiva_SoloQuitaLaDelLote()
    {
        var (context, lot, cl, _) = await SeedAsync();
        var controller = CreateController(context, campaignId: null);

        await controller.DeleteLotGeometry(lot.Id);

        var persistedLot = await context.Lots.AsNoTracking().FirstAsync(l => l.Id == lot.Id);
        Assert.Null(persistedLot.Geometry);

        var persistedCl = await context.CampaignLots.AsNoTracking().FirstAsync(x => x.Id == cl.Id);
        Assert.NotNull(persistedCl.Geometry);
    }

    [Fact]
    public async Task DeleteLotGeometry_LoteInexistente_DevuelveNotFound()
    {
        var context = CreateContext();
        var controller = CreateController(context, campaignId: null);

        var result = await controller.DeleteLotGeometry(Guid.NewGuid());

        Assert.IsType<NotFoundObjectResult>(result);
    }
}
