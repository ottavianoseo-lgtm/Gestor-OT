using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using NetTopologySuite.Operation.Union;
using Xunit;

namespace GestorOT.Tests.Regression;

public class PolygonOverlapTests
{
    [Fact]
    public void OverlappingPolygons_UnionArea_DeduplicatesOverlap()
    {
        var reader = new WKTReader();
        // Polygon 1: 10x10 square at origin (0,0) -> Area 100
        var poly1 = reader.Read("POLYGON ((0 0, 10 0, 10 10, 0 10, 0 0))");
        // Polygon 2: 10x10 square offset by 5 in X (5,0) -> Area 100, overlaps 5x10 (Area 50)
        var poly2 = reader.Read("POLYGON ((5 0, 15 0, 15 10, 5 10, 5 0))");

        var sumAreas = poly1.Area + poly2.Area; // 200
        var union = UnaryUnionOp.Union(new[] { poly1, poly2 }); // Net area 150

        Assert.Equal(200.0, sumAreas);
        Assert.Equal(150.0, union.Area);
    }

    [Fact]
    public void NonOverlappingPolygons_UnionArea_EqualsSumOfAreas()
    {
        var reader = new WKTReader();
        var poly1 = reader.Read("POLYGON ((0 0, 5 0, 5 5, 0 5, 0 0))"); // Area 25
        var poly2 = reader.Read("POLYGON ((10 10, 15 10, 15 15, 10 15, 10 10))"); // Area 25

        var union = UnaryUnionOp.Union(new[] { poly1, poly2 });

        Assert.Equal(50.0, union.Area);
    }

    [Fact]
    public void NetNewArea_CalculatesDifferenceWithExistingPolygons()
    {
        var reader = new WKTReader();
        var existing1 = reader.Read("POLYGON ((0 0, 10 0, 10 10, 0 10, 0 0))"); // 100
        var existing2 = reader.Read("POLYGON ((10 0, 20 0, 20 10, 10 10, 10 0))"); // 100

        // New polygon overlapping 5 units into existing1 (from 5 to 25 X, 0 to 10 Y) -> total 200 gross
        var newPoly = reader.Read("POLYGON ((5 0, 25 0, 25 10, 5 10, 5 0))");

        var existingUnion = UnaryUnionOp.Union(new[] { existing1, existing2 });
        var netNew = newPoly.Difference(existingUnion);

        // Net new should only be from X=20 to X=25 (5x10 = 50 area)
        Assert.Equal(50.0, netNew.Area);
    }
}
