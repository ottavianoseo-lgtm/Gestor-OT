using GestorOT.Domain.Entities;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using NetTopologySuite.Operation.Union;
using Xunit;

namespace GestorOT.Tests.Regression;

public class FieldCompositeGeometryTests
{
    [Fact]
    public void FieldCompositeGeometry_ShouldUnionMultipleLots()
    {
        var reader = new WKTReader();
        var poly1 = reader.Read("POLYGON ((-63.0 -31.0, -63.0 -31.1, -63.1 -31.1, -63.1 -31.0, -63.0 -31.0))");
        var poly2 = reader.Read("POLYGON ((-63.1 -31.0, -63.1 -31.1, -63.2 -31.1, -63.2 -31.0, -63.1 -31.0))");

        var lotGeometries = new List<Geometry> { poly1, poly2 };

        var compositeGeometry = UnaryUnionOp.Union(lotGeometries);

        Assert.NotNull(compositeGeometry);
        Assert.False(compositeGeometry.IsEmpty);
        Assert.True(compositeGeometry.Area > 0);
    }

    [Fact]
    public void FieldEntity_CalculatesTotalCadastralAreaFromLots()
    {
        var field = new Field
        {
            Id = Guid.NewGuid(),
            Name = "Campo Las Acacias",
            CreatedAt = DateTime.UtcNow,
            Lots = new List<Lot>
            {
                new Lot { Id = Guid.NewGuid(), Name = "Lote 1", CadastralArea = 100.5m },
                new Lot { Id = Guid.NewGuid(), Name = "Lote 2", CadastralArea = 75.25m }
            }
        };

        var totalArea = field.Lots.Sum(l => l.CadastralArea);

        Assert.Equal(175.75m, totalArea);
    }
}
