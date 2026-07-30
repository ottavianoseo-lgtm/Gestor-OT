using GestorOT.Domain.Entities;
using GestorOT.Shared.Dtos;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using Xunit;

namespace GestorOT.Tests.Regression;

public class MultiPolygonLotTests
{
    [Fact]
    public void CombineWktGeometries_ShouldCombineTwoPolygonsIntoMultiPolygon()
    {
        var poly1Wkt = "POLYGON ((-63.0 -31.0, -63.0 -31.1, -63.1 -31.1, -63.1 -31.0, -63.0 -31.0))";
        var poly2Wkt = "POLYGON ((-63.2 -31.2, -63.2 -31.3, -63.3 -31.3, -63.3 -31.2, -63.2 -31.2))";

        var reader = new WKTReader();
        var writer = new WKTWriter();

        var g1 = reader.Read(poly1Wkt);
        var g2 = reader.Read(poly2Wkt);

        var polygons = new List<Polygon>();
        void CollectPolygons(Geometry geom)
        {
            if (geom is Polygon p) polygons.Add(p);
            else if (geom is GeometryCollection gc)
            {
                for (int i = 0; i < gc.NumGeometries; i++)
                {
                    CollectPolygons(gc.GetGeometryN(i));
                }
            }
        }

        CollectPolygons(g1);
        CollectPolygons(g2);

        var factory = g1.Factory;
        var multiPoly = factory.CreateMultiPolygon(polygons.ToArray());
        var combinedWkt = writer.Write(multiPoly);

        Assert.StartsWith("MULTIPOLYGON", combinedWkt);
        Assert.Equal(2, multiPoly.NumGeometries);
    }

    [Fact]
    public void LotEntity_Supports_MultiPolygon_Geometry()
    {
        var reader = new WKTReader();
        var multiWkt = "MULTIPOLYGON (((-63.0 -31.0, -63.0 -31.1, -63.1 -31.1, -63.1 -31.0, -63.0 -31.0)), ((-63.2 -31.2, -63.2 -31.3, -63.3 -31.3, -63.3 -31.2, -63.2 -31.2)))";
        var geom = reader.Read(multiWkt);

        var lot = new Lot
        {
            Id = Guid.NewGuid(),
            Name = "Lote Multi-Polígono",
            Geometry = geom,
            CadastralArea = 150.5m
        };

        Assert.NotNull(lot.Geometry);
        Assert.IsType<MultiPolygon>(lot.Geometry);
        Assert.Equal(2, ((MultiPolygon)lot.Geometry).NumGeometries);
    }
}
