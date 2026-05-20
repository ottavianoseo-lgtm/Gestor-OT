using GestorOT.Domain.Entities;

namespace GestorOT.Application.Services;

public static class SupplyDistributionHelper
{
    public static void DistribuirProporcionalmente(WorkOrder workOrder, Guid supplyId, decimal realTotalUsed)
    {
        var suppliesAcrossLabors = workOrder.Labors
            .SelectMany(l => l.Supplies)
            .Where(s => s.SupplyId == supplyId)
            .ToList();

        var totalPlanned = suppliesAcrossLabors.Sum(s => s.PlannedTotal);

        if (totalPlanned > 0)
        {
            foreach (var supply in suppliesAcrossLabors)
            {
                var proportion = supply.PlannedTotal / totalPlanned;
                supply.CalculatedTotal = Math.Round(realTotalUsed * proportion, 4);

                var parentLabor = workOrder.Labors
                    .First(l => l.Supplies.Contains(supply));
                var effectiveArea = supply.RealHectares ?? supply.PlannedHectares;
                if (effectiveArea > 0)
                {
                    supply.CalculatedDose = Math.Round(supply.CalculatedTotal.Value / effectiveArea, 4);
                }
            }
        }
        else if (suppliesAcrossLabors.Count > 0)
        {
            var perLabor = Math.Round(realTotalUsed / suppliesAcrossLabors.Count, 4);
            foreach (var supply in suppliesAcrossLabors)
            {
                supply.CalculatedTotal = perLabor;
            }
        }
    }

    public static void LimpiarCalculados(WorkOrder workOrder, Guid supplyId)
    {
        var suppliesAcrossLabors = workOrder.Labors
            .SelectMany(l => l.Supplies)
            .Where(s => s.SupplyId == supplyId)
            .ToList();

        foreach (var supply in suppliesAcrossLabors)
        {
            supply.CalculatedTotal = null;
            supply.CalculatedDose = null;
        }
    }
}
