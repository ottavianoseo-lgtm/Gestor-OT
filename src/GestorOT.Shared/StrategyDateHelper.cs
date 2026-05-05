namespace GestorOT.Shared;

public static class StrategyDateHelper
{
    public static DateTime CalculateDate(
        DateTime changedDate,
        int changedItemDayOffset,
        int targetItemDayOffset,
        bool keepDateSeparation)
    {
        if (!keepDateSeparation)
            return changedDate;

        var baseDate = changedDate.AddDays(-changedItemDayOffset);
        return baseDate.AddDays(targetItemDayOffset);
    }
}
