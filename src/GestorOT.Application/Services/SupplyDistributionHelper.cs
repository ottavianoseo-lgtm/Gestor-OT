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
                var calculatedTotal = Math.Round(realTotalUsed * proportion, 4);

                // CalculatedTotal/Dose: para el drawer de desglose en Insumos Consolidados
                supply.CalculatedTotal = calculatedTotal;

                var effectiveArea = supply.RealHectares ?? supply.PlannedHectares;
                if (effectiveArea > 0)
                {
                    var calculatedDose = Math.Round(calculatedTotal / effectiveArea, 4);
                    supply.CalculatedDose = calculatedDose;

                    // RealTotal/RealDose: lo que muestra "Detalle de Insumos" en cada labor
                    supply.RealTotal = calculatedTotal;
                    supply.RealDose = calculatedDose;
                    supply.RealHectares ??= supply.PlannedHectares;
                }
                else
                {
                    supply.RealTotal = calculatedTotal;
                }
            }
        }
        else if (suppliesAcrossLabors.Count > 0)
        {
            var perLabor = Math.Round(realTotalUsed / suppliesAcrossLabors.Count, 4);
            foreach (var supply in suppliesAcrossLabors)
            {
                supply.CalculatedTotal = perLabor;
                supply.RealTotal = perLabor;
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
            supply.RealTotal = null;
            supply.RealDose = null;
        }
    }
}
