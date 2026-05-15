using GestorOT.Shared.Dtos;

namespace GestorOT.Tests.Regression;

public class RuleOfThreeProjectionTests
{
    [Fact]
    public void GetById_ProjectsCalculatedDoseAndTotal_WhenRealTotalUsedIsSet()
    {
        var supplyId = Guid.NewGuid();

        var labor1 = CreateLabor("Lote 1", 10m, supplyId, plannedDose: 2m);
        var labor2 = CreateLabor("Lote 2", 5m, supplyId, plannedDose: 2m);

        var labors = new List<LaborDto> { labor1, labor2 };
        var approval = new WorkOrderSupplyApprovalDto
        {
            SupplyId = supplyId,
            SupplyName = "Glifosato",
            RealTotalUsed = 29m
        };

        ProjectCalculated(labors, supplyId, approval.RealTotalUsed!.Value);

        var s1 = labor1.Supplies[0];
        var s2 = labor2.Supplies[0];

        Assert.NotNull(s1.CalculatedTotal);
        Assert.NotNull(s2.CalculatedTotal);
        Assert.Equal(19.33m, Math.Round(s1.CalculatedTotal!.Value, 2));
        Assert.Equal(9.67m, Math.Round(s2.CalculatedTotal!.Value, 2));
    }

    [Fact]
    public void GetById_DoesNotProjectCalculated_WhenRealTotalUsedIsNull()
    {
        var supplyId = Guid.NewGuid();
        var labor = CreateLabor("Lote 1", 10m, supplyId, plannedDose: 2m);
        var labors = new List<LaborDto> { labor };
        var approval = new WorkOrderSupplyApprovalDto
        {
            SupplyId = supplyId,
            RealTotalUsed = null
        };

        // No projection because RealTotalUsed is null
        Assert.Null(labor.Supplies[0].CalculatedTotal);
        Assert.Null(labor.Supplies[0].CalculatedDose);
    }

    [Fact]
    public void GetById_HandlesZeroPlannedTotal_Gracefully()
    {
        var supplyId = Guid.NewGuid();

        var labor1 = CreateLabor("Lote 1", 5m, supplyId, plannedDose: 0m, plannedTotal: 0m);
        var labor2 = CreateLabor("Lote 2", 10m, supplyId, plannedDose: 2m, plannedTotal: 20m);

        var labors = new List<LaborDto> { labor1, labor2 };
        var approval = new WorkOrderSupplyApprovalDto
        {
            SupplyId = supplyId,
            RealTotalUsed = 10m
        };

        ProjectCalculated(labors, supplyId, approval.RealTotalUsed!.Value);

        var s1 = labor1.Supplies[0];
        var s2 = labor2.Supplies[0];

        // Labor with 0 planned gets 0 proportion
        Assert.Equal(0m, s1.CalculatedTotal);
        // Labor with 20 of 20 planned gets 100% = 10
        Assert.Equal(10m, s2.CalculatedTotal);
    }

    [Fact]
    public void ProjectCalculated_UsesRealHectares_WhenAvailable()
    {
        var supplyId = Guid.NewGuid();

        var labor = CreateLabor("Lote 1", 10m, supplyId, plannedDose: 2m, plannedTotal: 20m, realHectares: 8m);

        var labors = new List<LaborDto> { labor };
        var approval = new WorkOrderSupplyApprovalDto
        {
            SupplyId = supplyId,
            RealTotalUsed = 16m
        };

        ProjectCalculated(labors, supplyId, approval.RealTotalUsed!.Value);

        var s = labor.Supplies[0];
        Assert.Equal(16m, s.CalculatedTotal);
        Assert.Equal(2m, s.CalculatedDose); // 16 / 8 = 2
    }

    [Fact]
    public void ProjectCalculated_NoLaborsWithSupply_DoesNotCrash()
    {
        var labors = new List<LaborDto>();
        var approval = new WorkOrderSupplyApprovalDto
        {
            SupplyId = Guid.NewGuid(),
            RealTotalUsed = 100m
        };

        // Should not throw
        ProjectCalculated(labors, approval.SupplyId, approval.RealTotalUsed!.Value);
    }

    [Fact]
    public void SupplyBreakdownRow_CalculatesCorrectPercentages()
    {
        var supplyId = Guid.NewGuid();
        var labor1 = CreateLabor("Lote A", 10m, supplyId, plannedDose: 2m, plannedTotal: 20m);
        var labor2 = CreateLabor("Lote B", 5m, supplyId, plannedDose: 2m, plannedTotal: 10m);

        var totalPlanned = 20m + 10m;

        var pct1 = totalPlanned > 0 ? (20m / totalPlanned) * 100 : 0;
        var pct2 = totalPlanned > 0 ? (10m / totalPlanned) * 100 : 0;

        Assert.Equal(66.67m, Math.Round(pct1, 2));
        Assert.Equal(33.33m, Math.Round(pct2, 2));
    }

    // ── Helpers ──

    private static LaborDto CreateLabor(
        string lotName,
        decimal hectares,
        Guid supplyId,
        decimal plannedDose,
        decimal? plannedTotal = null,
        decimal? realHectares = null,
        decimal? realDose = null)
    {
        var total = plannedTotal ?? (plannedDose * hectares);

        var supply = new LaborSupplyDto(
            Guid.NewGuid(), Guid.NewGuid(), supplyId,
            hectares, realHectares,
            plannedDose, realDose,
            total, null, null, null,
            "L/Ha", "Glifosato", "L", 1, false);

        return new LaborDto
        {
            Id = Guid.NewGuid(),
            LotName = lotName,
            Hectares = hectares,
            Supplies = new List<LaborSupplyDto> { supply }
        };
    }

    private static void ProjectCalculated(
        List<LaborDto> labors,
        Guid supplyId,
        decimal realTotalUsed)
    {
        var laborsWithSupply = labors
            .Where(l => l.Supplies.Any(s => s.SupplyId == supplyId))
            .ToList();

        var totalPlannedForSupply = laborsWithSupply
            .Sum(l => l.Supplies.Where(s => s.SupplyId == supplyId).Sum(s => s.PlannedTotal));

        if (totalPlannedForSupply <= 0) return;

        foreach (var labor in laborsWithSupply)
        {
            foreach (var supply in labor.Supplies.Where(s => s.SupplyId == supplyId))
            {
                var proportion = supply.PlannedTotal / totalPlannedForSupply;
                supply.CalculatedTotal = realTotalUsed * proportion;

                var area = supply.RealHectares ?? labor.Hectares;
                if (area > 0)
                {
                    supply.CalculatedDose = supply.CalculatedTotal / area;
                }
            }
        }
    }
}
