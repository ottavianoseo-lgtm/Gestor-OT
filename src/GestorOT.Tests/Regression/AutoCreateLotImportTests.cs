using GestorOT.Domain.Entities;
using GestorOT.Shared.Dtos;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using Xunit;

namespace GestorOT.Tests.Regression;

public class AutoCreateLotImportTests
{
    [Fact]
    public void Lot_CanBeCreatedFromImportedWktGeometry()
    {
        var importedWkt = "POLYGON ((-63.5 -31.5, -63.5 -31.6, -63.6 -31.6, -63.6 -31.5, -63.5 -31.5))";
        var fieldId = Guid.NewGuid();
        var lotName = "Lote Importado Auto 1";

        var reader = new WKTReader();
        var geom = reader.Read(importedWkt);

        var lot = new Lot
        {
            Id = Guid.NewGuid(),
            FieldId = fieldId,
            Name = lotName,
            Status = "Active",
            Geometry = geom,
            CadastralArea = 120.75m
        };

        Assert.NotEqual(Guid.Empty, lot.Id);
        Assert.Equal(fieldId, lot.FieldId);
        Assert.Equal("Lote Importado Auto 1", lot.Name);
        Assert.NotNull(lot.Geometry);
        Assert.Equal(120.75m, lot.CadastralArea);
    }
}
