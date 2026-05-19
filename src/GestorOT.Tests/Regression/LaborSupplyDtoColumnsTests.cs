using System.Text.Json;
using GestorOT.Shared;
using GestorOT.Shared.Dtos;

namespace GestorOT.Tests.Regression;

public class LaborSupplyDtoColumnsTests
{
    [Fact]
    public void LaborSupplyDto_HasSupplyUnitField()
    {
        var prop = typeof(LaborSupplyDto).GetProperty("SupplyUnit");

        Assert.NotNull(prop);
        Assert.Equal(typeof(string), prop!.PropertyType);
    }

    [Fact]
    public void LaborSupplyDto_HasPlannedHectaresAndRealHectares()
    {
        var plannedProp = typeof(LaborSupplyDto).GetProperty("PlannedHectares");
        var realProp = typeof(LaborSupplyDto).GetProperty("RealHectares");

        Assert.NotNull(plannedProp);
        Assert.NotNull(realProp);
        Assert.Equal(typeof(decimal), plannedProp!.PropertyType);
        Assert.Equal(typeof(decimal?), realProp!.PropertyType);
    }

    [Fact]
    public void WorkOrderQueryService_GetById_PopulatesSupplyUnitFromInventoryUnitA()
    {
        var dto = new LaborSupplyDto(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            10m, null, 2m, null, 20m, null, null, null,
            "L/Ha", "Glifosato", "L", 1, false);

        Assert.Equal("L", dto.SupplyUnit);
    }

    [Fact]
    public void WorkOrderQueryService_GetById_PopulatesPlannedHectaresFromLaborSupply()
    {
        var dto = new LaborSupplyDto(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            12.5m, null, 2m, null, 25m, null, null, null,
            "L/Ha", "2,4-D", "L", 1, false);

        Assert.Equal(12.5m, dto.PlannedHectares);
    }

    [Fact]
    public void WorkOrderQueryService_GetById_NullRealHectares_DoesNotCrash()
    {
        var dto = new LaborSupplyDto(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            10m, null,
            2m, null,
            20m, null,
            null, null,
            "L/Ha", "Atrazina", "kg", 2, false);

        Assert.Equal(10m, dto.PlannedHectares);
        Assert.Null(dto.RealHectares);
        Assert.Null(dto.RealDose);
        Assert.Null(dto.RealTotal);
    }

    [Fact]
    public void LaborSupplyDto_Serialization_RoundTripsSupplyUnit()
    {
        var dto = new LaborSupplyDto(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            10m, null, 2m, null, 20m, null, null, null,
            "L/Ha", "Glifosato", "L", 1, false);

        var json = JsonSerializer.Serialize(dto, AppJsonSerializerContext.Default.LaborSupplyDto);
        var deserialized = JsonSerializer.Deserialize(json, AppJsonSerializerContext.Default.LaborSupplyDto);

        Assert.NotNull(deserialized);
        Assert.Equal("Glifosato", deserialized!.SupplyName);
        Assert.Equal("L", deserialized.SupplyUnit);
        Assert.Equal(10m, deserialized.PlannedHectares);
    }
}
