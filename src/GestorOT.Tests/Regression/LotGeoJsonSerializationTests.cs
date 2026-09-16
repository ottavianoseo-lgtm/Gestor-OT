using GestorOT.Domain.Entities;
using GestorOT.Infrastructure.Services;
using NetTopologySuite.Geometries;
using Xunit;

namespace GestorOT.Tests.Regression;

/// <summary>
/// El GeoJSON que se le manda al mapa tiene que respetar el anidado de la especificación.
///
/// Antes se armaba con <c>polygon.Coordinates</c>, que devuelve el anillo exterior y los
/// interiores concatenados en una sola lista de puntos, y se emitía como un único anillo: el
/// dibujo saltaba del borde del lote a cada agujero y volvía, así que los lotes aparecían en el
/// mapa con astillas y líneas cruzándolos. Los MultiPolygon además salían con un nivel de
/// anidado de menos, y el cliente los leía como un polígono cuyas partes eran agujeros.
/// </summary>
public class LotGeoJsonSerializationTests
{
    private static readonly GeometryFactory Factory = new(new PrecisionModel(), 4326);

    private static LinearRing Anillo(params (double X, double Y)[] puntos)
        => Factory.CreateLinearRing(puntos.Select(p => new Coordinate(p.X, p.Y)).ToArray());

    /// <summary>Cuadrado de 1x1 con un agujero de 0,2x0,2 en el medio.</summary>
    private static Polygon ConAgujero() => Factory.CreatePolygon(
        Anillo((0, 0), (1, 0), (1, 1), (0, 1), (0, 0)),
        new[] { Anillo((0.4, 0.4), (0.6, 0.4), (0.6, 0.6), (0.4, 0.6), (0.4, 0.4)) });

    private static Polygon Simple(double dx) => Factory.CreatePolygon(
        Anillo((dx, 0), (dx + 1, 0), (dx + 1, 1), (dx, 1), (dx, 0)));

    private static Lot LoteCon(Geometry? geometry) => new()
    {
        Id = Guid.NewGuid(),
        FieldId = Guid.NewGuid(),
        Name = "Lote",
        Geometry = geometry
    };

    [Fact]
    public void PolygonConAgujero_SeSirveConDosAnillosSeparados()
    {
        var geo = LotQueryService.ResolverGeometria(LoteCon(ConAgujero()), geometryByLot: null);

        Assert.NotNull(geo);
        Assert.Equal("Polygon", geo!.Type);

        var anillos = Assert.IsType<double[][][]>(geo.Coordinates);
        Assert.Equal(2, anillos.Length);

        // Exterior y agujero, cada uno cerrado sobre sí mismo. Concatenados daban 10 puntos en
        // un solo anillo y de ahí salían las astillas.
        Assert.Equal(5, anillos[0].Length);
        Assert.Equal(5, anillos[1].Length);
        Assert.Equal(anillos[0][0], anillos[0][^1]);
        Assert.Equal(anillos[1][0], anillos[1][^1]);

        // Orden GeoJSON: [lon, lat].
        Assert.Equal(new double[] { 0, 0 }, anillos[0][0]);
    }

    [Fact]
    public void MultiPolygon_SeSirveConElNivelDeAnidadoDeLaEspecificacion()
    {
        var multi = Factory.CreateMultiPolygon(new[] { ConAgujero(), Simple(10) });

        var geo = LotQueryService.ResolverGeometria(LoteCon(multi), geometryByLot: null);

        Assert.NotNull(geo);
        Assert.Equal("MultiPolygon", geo!.Type);

        // polígonos -> anillos -> puntos -> [lon, lat]. Antes se emitía un nivel más chato y
        // las partes del MultiPolygon se dibujaban como agujeros de la primera.
        var poligonos = Assert.IsType<double[][][][]>(geo.Coordinates);
        Assert.Equal(2, poligonos.Length);
        Assert.Equal(2, poligonos[0].Length);   // exterior + agujero
        Assert.Single(poligonos[1]);            // solo exterior
        Assert.Equal(new double[] { 10, 0 }, poligonos[1][0][0]);
    }

    [Fact]
    public void PolygonSinAgujeros_SigueSaliendoConUnAnillo()
    {
        var geo = LotQueryService.ResolverGeometria(LoteCon(Simple(0)), geometryByLot: null);

        var anillos = Assert.IsType<double[][][]>(geo!.Coordinates);
        Assert.Single(anillos);
        Assert.Equal(5, anillos[0].Length);
    }

    [Fact]
    public void GeometriaNoPoligonal_NoSeDibuja()
    {
        // La columna es poligonal: si llegó otra cosa es un dato roto, y dibujar un polígono
        // con sus coordenadas sueltas es peor que no dibujar nada.
        var punto = Factory.CreatePoint(new Coordinate(-59.6, -37.3));

        Assert.Null(LotQueryService.ResolverGeometria(LoteCon(punto), geometryByLot: null));
    }
}
