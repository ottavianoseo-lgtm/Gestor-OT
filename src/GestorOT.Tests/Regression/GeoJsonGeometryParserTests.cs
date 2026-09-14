using GestorOT.Infrastructure.Services;
using NetTopologySuite.Geometries;
using Xunit;

namespace GestorOT.Tests.Regression;

/// <summary>
/// La columna GIS de la planilla de lotes llega como GeoJSON. Acá se fija que se convierta a una
/// geometría 4326 válida y que lo que no sirve se rechace en la vista previa en vez de reventar en
/// PostGIS al guardar.
/// </summary>
public class GeoJsonGeometryParserTests
{
    private const string PolygonJson =
        "{\"type\":\"Polygon\",\"coordinates\":[[[-62.45,-35.61],[-62.44,-35.61],[-62.44,-35.60],[-62.45,-35.60],[-62.45,-35.61]]]}";

    private const string MultiPolygonJson =
        "{\"type\":\"MultiPolygon\",\"coordinates\":[[[[-62.45,-35.61],[-62.44,-35.61],[-62.44,-35.60],[-62.45,-35.60],[-62.45,-35.61]]]]}";

    [Fact]
    public void Vacio_EsValidoYNoTieneGeometria()
    {
        Assert.True(GeoJsonGeometryParser.TryParse("", out var geometry, out var error));
        Assert.Null(geometry);
        Assert.Null(error);
    }

    [Fact]
    public void Polygon_SeConvierteConSrid4326()
    {
        Assert.True(GeoJsonGeometryParser.TryParse(PolygonJson, out var geometry, out var error), error);

        var polygon = Assert.IsType<Polygon>(geometry);
        Assert.Equal(4326, polygon.SRID);
        Assert.True(polygon.IsValid);
    }

    [Fact]
    public void MultiPolygon_SeConvierte()
    {
        Assert.True(GeoJsonGeometryParser.TryParse(MultiPolygonJson, out var geometry, out _));
        Assert.IsType<MultiPolygon>(geometry);
    }

    [Fact]
    public void Wkt_TambienSeAcepta()
    {
        var wkt = "POLYGON ((-62.45 -35.61, -62.44 -35.61, -62.44 -35.60, -62.45 -35.60, -62.45 -35.61))";

        Assert.True(GeoJsonGeometryParser.TryParse(wkt, out var geometry, out _));
        Assert.IsType<Polygon>(geometry);
    }

    [Fact]
    public void Punto_SeRechaza()
    {
        var punto = "{\"type\":\"Point\",\"coordinates\":[-62.2,-35.8]}";

        Assert.False(GeoJsonGeometryParser.TryParse(punto, out var geometry, out var error));
        Assert.Null(geometry);
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void TextoInvalido_SeRechazaConError()
    {
        Assert.False(GeoJsonGeometryParser.TryParse("esto no es un geojson", out _, out var error));
        Assert.False(string.IsNullOrWhiteSpace(error));
    }
}
