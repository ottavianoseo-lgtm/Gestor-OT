using GestorOT.Domain.Entities;
using GestorOT.Shared;
using GestorOT.Shared.Dtos;
using Xunit;

namespace GestorOT.Tests.Regression;

public class SupplyUnitFormattingTests
{
    [Theory]
    [InlineData("ha", true)]
    [InlineData("HA", true)]
    [InlineData("Ha", true)]
    [InlineData("hta", true)]
    [InlineData("HTA", true)]
    [InlineData("has", true)]
    [InlineData("HAS", true)]
    [InlineData("hectarea", true)]
    [InlineData("hectareas", true)]
    [InlineData("LT", false)]
    [InlineData("KG", false)]
    [InlineData("Bls", false)]
    [InlineData("TN", false)]
    [InlineData("Pac", false)]
    [InlineData("u", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void UnitHelper_IsSurfaceUnit_IdentifiesSurfaceUnitsCorrectly(string? unit, bool expected)
    {
        var result = UnitHelper.IsSurfaceUnit(unit);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("LT/ha", "LT")]
    [InlineData("kg/ha", "kg")]
    [InlineData("bls/ha", "bls")]
    [InlineData("l/ha", "l")]
    [InlineData("LT", "LT")]
    [InlineData("KG", "KG")]
    [InlineData("HA", null)]
    [InlineData("HTA", null)]
    [InlineData("ha", null)]
    [InlineData(null, null)]
    public void UnitHelper_CleanDoseUnit_ExtractsPhysicalUnit(string? doseUnit, string? expected)
    {
        var result = UnitHelper.CleanDoseUnit(doseUnit);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void UnitHelper_GetEffectiveSupplyUnit_PrioritizesUnitB_WhenUnitAIsSurfaceUnit()
    {
        // GestorMax ERP sends UnitA = "HA" (auxiliary reference) and UnitB = "LT" (physical)
        var unitA = "HA";
        var unitB = "LT";

        var effective = UnitHelper.GetEffectiveSupplyUnit(unitA, unitB);

        Assert.Equal("LT", effective);
    }

    [Fact]
    public void UnitHelper_GetEffectiveSupplyUnit_UsesUnitA_WhenUnitAIsPhysicalUnit()
    {
        var unitA = "KG";
        var unitB = "u";

        var effective = UnitHelper.GetEffectiveSupplyUnit(unitA, unitB);

        Assert.Equal("KG", effective);
    }

    [Fact]
    public void InventoryEntity_GetEffectiveUnit_ReturnsPhysicalUnit_WhenUnitAIsHectares()
    {
        var inventory = new Inventory
        {
            ItemName = "HERBIFEN SUPER",
            UnitA = "HA",
            UnitB = "LT",
            Unit = "HA"
        };

        var effective = inventory.GetEffectiveUnit();

        Assert.Equal("LT", effective);
    }

    [Fact]
    public void InventoryDto_GetEffectiveUnit_ReturnsPhysicalUnit_WhenUnitAIsHectares()
    {
        var dto = new InventoryDto(
            Guid.NewGuid(),
            "Herbicidas",
            "PIVOT",
            100,
            10,
            "HA",
            "LT",
            1
        );

        var effective = dto.GetEffectiveUnit();

        Assert.Equal("LT", effective);
    }

    [Fact]
    public void TotalAppliedCalculation_DoseTimesHectares_FormatsWithPhysicalUnit_NotHectares()
    {
        // Simulating the user reported case:
        // Dose: 5 LT/ha
        // Hectares: 500 ha
        // Total should be: 2500 LT (NOT 2500 HTA)
        var dose = 5.0m;
        var hectares = 500.0m;
        var total = dose * hectares;

        var supplyUnitA = "HA"; // ERP auxiliary
        var supplyUnitB = "LT"; // ERP physical
        var effectiveUnit = UnitHelper.GetEffectiveSupplyUnit(supplyUnitA, supplyUnitB);

        Assert.Equal(2500.0m, total);
        Assert.Equal("LT", effectiveUnit);
        Assert.NotEqual("HA", effectiveUnit);
        Assert.NotEqual("HTA", effectiveUnit);

        var displayString = $"{total:N2} {effectiveUnit}";
        Assert.Equal("2.500,00 LT", displayString);
    }

    [Fact]
    public void UnitHelper_FormatDoseRate_FormatsRateWithSurfaceDenominator()
    {
        Assert.Equal("LT/ha", UnitHelper.FormatDoseRate("LT"));
        Assert.Equal("KG/ha", UnitHelper.FormatDoseRate("KG"));
        Assert.Equal("u/ha", UnitHelper.FormatDoseRate("HA")); // guards against HA/ha
        Assert.Equal("u/ha", UnitHelper.FormatDoseRate(null));
    }

    [Fact]
    public void LaborSupplyDto_SupplyUnit_PopulatedWithCleanUnit_DoesNotContainHectares()
    {
        var dto = new LaborSupplyDto
        {
            Id = Guid.NewGuid(),
            SupplyName = "Atrazina 50%",
            PlannedDose = 2.5m,
            PlannedHectares = 100m,
            PlannedTotal = 250m,
            UnitOfMeasure = "LT",
            SupplyUnit = "LT"
        };

        Assert.False(UnitHelper.IsSurfaceUnit(dto.SupplyUnit));
        Assert.Equal("LT", dto.SupplyUnit);
    }
}
