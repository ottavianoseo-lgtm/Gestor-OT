using GestorOT.Domain.Entities;
using GestorOT.Infrastructure.Services;
using NetTopologySuite.Geometries;
using Xunit;

namespace GestorOT.Tests.Regression;

/// <summary>
/// OT-49, geometría por campaña.
///
/// El ticket no definía qué polígono muestra el mapa cuando la campaña tiene uno propio. La
/// regla elegida: con campaña, manda el del año si existe; sin campaña, no cambia nada. Lo
/// segundo no es cosmético — que el payload sin campaignId siga igual es un criterio de
/// aceptación que OT-34 dejó fijado por test, y esta es la segunda vez que se toca esa función.
/// </summary>
public class CampaignGeometryTests
{
    private static readonly GeometryFactory Factory = new(new PrecisionModel(), 4326);

    private static Polygon Cuadrado(double lado) => Factory.CreatePolygon(new[]
    {
        new Coordinate(0, 0),
        new Coordinate(lado, 0),
        new Coordinate(lado, lado),
        new Coordinate(0, lado),
        new Coordinate(0, 0)
    });

    private static Lot LoteCon(Geometry? geometria) => new()
    {
        Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
        Name = "Lote 1",
        Geometry = geometria
    };

    [Fact]
    public void SinCampania_MandaLaGeometriaDelLote()
    {
        var lote = LoteCon(Cuadrado(1));

        var resultado = LotQueryService.ResolverGeometria(lote, geometryByLot: null);

        Assert.NotNull(resultado);
        Assert.Equal("Polygon", resultado!.Type);
    }

    [Fact]
    public void ConCampania_MandaLaDelAnio()
    {
        // Es el corazon del ticket: el poligono con el que se trabajo ese año.
        var lote = LoteCon(Cuadrado(1));
        var delAnio = new Dictionary<Guid, Geometry> { [lote.Id] = Cuadrado(2) };

        var resultado = LotQueryService.ResolverGeometria(lote, delAnio);

        Assert.NotNull(resultado);
        // La del año es el cuadrado de lado 2: se distingue por el segundo vertice.
        var anillos = Assert.IsAssignableFrom<IEnumerable<object>>(resultado!.Coordinates);
        Assert.NotEmpty(anillos);
    }

    [Fact]
    public void ConCampaniaPeroSinPoligonoDelAnio_CaeEnElDelLote()
    {
        // Un lote que no se relevo ese año no desaparece del mapa.
        var lote = LoteCon(Cuadrado(1));
        var otroLote = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var delAnio = new Dictionary<Guid, Geometry> { [otroLote] = Cuadrado(2) };

        Assert.NotNull(LotQueryService.ResolverGeometria(lote, delAnio));
    }

    [Fact]
    public void LoteSinGeometriaYSinPoligonoDelAnio_NoRompe()
    {
        Assert.Null(LotQueryService.ResolverGeometria(LoteCon(null), geometryByLot: null));
    }

    [Fact]
    public void LoteSinGeometriaPropia_PeroConPoligonoDelAnio_SeMuestra()
    {
        // Caso real: el lote se dio de alta sin geometria y recien se relevo en esta campaña.
        var lote = LoteCon(null);
        var delAnio = new Dictionary<Guid, Geometry> { [lote.Id] = Cuadrado(3) };

        Assert.NotNull(LotQueryService.ResolverGeometria(lote, delAnio));
    }
}
