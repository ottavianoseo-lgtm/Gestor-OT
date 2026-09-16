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

    /// <summary>
    /// Un cuadrado de 1x1 con un hueco de 0,2x0,2 adentro, pero escrito como dos polígonos
    /// sueltos del MultiPolygon en vez de shell + anillo interior. Así sale el export de
    /// shapefile de la planilla y OGC lo marca inválido ("nested shells").
    /// </summary>
    private const string AgujeroComoPoligonoSueltoJson =
        "{\"type\":\"MultiPolygon\",\"coordinates\":["
        + "[[[0,0],[1,0],[1,1],[0,1],[0,0]]],"
        + "[[[0.4,0.4],[0.6,0.4],[0.6,0.6],[0.4,0.6],[0.4,0.4]]]"
        + "]}";

    /// <summary>Anillo cruzado en moño: ningún re-anidado lo salva, lo tiene que arreglar el fixer.</summary>
    private const string AnilloCruzadoJson =
        "{\"type\":\"Polygon\",\"coordinates\":[[[0,0],[1,1],[1,0],[0,1],[0,0]]]}";

    [Fact]
    public void AgujeroExportadoComoPoligonoSuelto_SeReAnidaComoAnilloInterior()
    {
        Assert.True(
            GeoJsonGeometryParser.TryParse(AgujeroComoPoligonoSueltoJson, out var geometry, out var error, out var reparada),
            error);

        Assert.True(reparada);

        var polygon = Assert.IsType<Polygon>(geometry);
        Assert.True(polygon.IsValid);
        Assert.Equal(1, polygon.NumInteriorRings);

        // 1x1 menos el hueco de 0,2x0,2. Si se hubiera resuelto por unión en vez de por
        // agujero, el área daría 1 y el lote quedaría con más superficie de la que tiene.
        Assert.Equal(0.96, polygon.Area, 6);
    }

    [Fact]
    public void AnilloCruzado_LoCorrigeElFixerYSigueSiendoPoligonal()
    {
        Assert.True(
            GeoJsonGeometryParser.TryParse(AnilloCruzadoJson, out var geometry, out var error, out var reparada),
            error);

        Assert.True(reparada);
        Assert.NotNull(geometry);
        Assert.True(geometry!.IsValid);
        Assert.True(geometry is Polygon or MultiPolygon);
        Assert.Equal(4326, geometry.SRID);
    }

    [Fact]
    public void GeometriaSana_NoSeMarcaComoReparada()
    {
        Assert.True(GeoJsonGeometryParser.TryParse(PolygonJson, out _, out _, out var reparada));
        Assert.False(reparada);

        Assert.True(GeoJsonGeometryParser.TryParse(MultiPolygonJson, out _, out _, out reparada));
        Assert.False(reparada);
    }

    [Fact]
    public void PolygonConAgujeroBienFormado_ConservaElAnilloInterior()
    {
        // El re-anidado no tiene que romper lo que ya viene bien: los anillos 1..n de un
        // Polygon GeoJSON son agujeros por especificación y así deben quedar.
        var conAgujero =
            "{\"type\":\"Polygon\",\"coordinates\":["
            + "[[0,0],[1,0],[1,1],[0,1],[0,0]],"
            + "[[0.4,0.4],[0.6,0.4],[0.6,0.6],[0.4,0.6],[0.4,0.4]]"
            + "]}";

        Assert.True(GeoJsonGeometryParser.TryParse(conAgujero, out var geometry, out var error, out var reparada), error);

        Assert.False(reparada);
        var polygon = Assert.IsType<Polygon>(geometry);
        Assert.Equal(1, polygon.NumInteriorRings);
        Assert.Equal(0.96, polygon.Area, 6);
    }
}
