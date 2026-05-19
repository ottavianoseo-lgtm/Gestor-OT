using GestorOT.Shared;

namespace GestorOT.Tests.Regression;

public class StrategyDateHelperTests
{
    [Fact]
    public void CalculateDate_WithSeparation_WhenChangingMiddleItem_RecalculatesOthers()
    {
        var baseDate = new DateTime(2026, 5, 20);
        var changedDate = new DateTime(2026, 5, 25);

        var newDate = StrategyDateHelper.CalculateDate(
            changedDate,
            changedItemDayOffset: 5,
            targetItemDayOffset: 10,
            keepDateSeparation: true);

        Assert.Equal(new DateTime(2026, 5, 30), newDate);
    }

    [Fact]
    public void CalculateDate_WithSeparation_WhenChangingFirstItem_RecalculatesOthers()
    {
        var changedDate = new DateTime(2026, 5, 25);

        var newForItemB = StrategyDateHelper.CalculateDate(
            changedDate,
            changedItemDayOffset: 5,
            targetItemDayOffset: 10,
            keepDateSeparation: true);

        Assert.Equal(new DateTime(2026, 5, 30), newForItemB);
    }

    [Fact]
    public void CalculateDate_WithSeparation_WhenChangingBaseItem_RecalculatesCorrectly()
    {
        var changedDate = new DateTime(2026, 5, 22);

        var newForItemPlus3 = StrategyDateHelper.CalculateDate(
            changedDate,
            changedItemDayOffset: 0,
            targetItemDayOffset: 3,
            keepDateSeparation: true);

        Assert.Equal(new DateTime(2026, 5, 25), newForItemPlus3);
    }

    [Fact]
    public void CalculateDate_WithoutSeparation_ReturnsChangedDateOnly()
    {
        var changedDate = new DateTime(2026, 5, 25);

        var newDate = StrategyDateHelper.CalculateDate(
            changedDate,
            changedItemDayOffset: 5,
            targetItemDayOffset: 10,
            keepDateSeparation: false);

        Assert.Equal(changedDate, newDate);
    }

    [Fact]
    public void CalculateDate_WithSeparation_MultipleItemsMaintainCorrectOffsets()
    {
        var changedDate = new DateTime(2026, 5, 25);

        var offsets = new[] { 0, 5, 10 };
        var results = offsets.Select(o => StrategyDateHelper.CalculateDate(
            changedDate,
            changedItemDayOffset: 5,
            targetItemDayOffset: o,
            keepDateSeparation: true)).ToList();

        Assert.Equal(new DateTime(2026, 5, 20), results[0]);
        Assert.Equal(new DateTime(2026, 5, 25), results[1]);
        Assert.Equal(new DateTime(2026, 5, 30), results[2]);
    }

    [Fact]
    public void CalculateDate_AllSameOffset_AllDatesEqual()
    {
        var changedDate = new DateTime(2026, 6, 15);

        var result = StrategyDateHelper.CalculateDate(
            changedDate,
            changedItemDayOffset: 7,
            targetItemDayOffset: 7,
            keepDateSeparation: true);

        Assert.Equal(changedDate, result);
    }
}
