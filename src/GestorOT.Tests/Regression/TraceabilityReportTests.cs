using GestorOT.Application.Interfaces;
using GestorOT.Domain.Entities;
using GestorOT.Domain.Enums;
using GestorOT.Infrastructure.Services;
using GestorOT.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace GestorOT.Tests.Regression;

public class TraceabilityReportTests
{
    private static Mock<IApplicationDbContext> BuildContextMock(
        List<Campaign> campaigns,
        List<CampaignLot> campaignLots,
        List<Labor> labors)
    {
        var mock = new Mock<IApplicationDbContext>();

        void SetupDbSet<T>(Mock<DbSet<T>> dbSetMock, IQueryable<T> data) where T : class
        {
            dbSetMock.As<IAsyncEnumerable<T>>()
                .Setup(m => m.GetAsyncEnumerator(It.IsAny<CancellationToken>()))
                .Returns(new TestAsyncEnumerator<T>(data.GetEnumerator()));
            dbSetMock.As<IQueryable<T>>().Setup(m => m.Provider)
                .Returns(new TestAsyncQueryProvider<T>(data.Provider));
            dbSetMock.As<IQueryable<T>>().Setup(m => m.Expression).Returns(data.Expression);
            dbSetMock.As<IQueryable<T>>().Setup(m => m.ElementType).Returns(data.ElementType);
            dbSetMock.As<IQueryable<T>>().Setup(m => m.GetEnumerator()).Returns(data.GetEnumerator());
        }

        var campaignsMock = new Mock<DbSet<Campaign>>();
        SetupDbSet(campaignsMock, campaigns.AsQueryable());
        mock.Setup(c => c.Campaigns).Returns(campaignsMock.Object);

        var campaignLotsMock = new Mock<DbSet<CampaignLot>>();
        SetupDbSet(campaignLotsMock, campaignLots.AsQueryable());
        mock.Setup(c => c.CampaignLots).Returns(campaignLotsMock.Object);

        var laborsMock = new Mock<DbSet<Labor>>();
        SetupDbSet(laborsMock, labors.AsQueryable());
        mock.Setup(c => c.Labors).Returns(laborsMock.Object);

        return mock;
    }

    [Fact]
    public async Task GetLotTraceabilityAsync_AccumulatesRepeatedSuppliesAndLaborsCorrectly()
    {
        var campaignId = Guid.NewGuid();
        var fieldId = Guid.NewGuid();
        var lotId = Guid.NewGuid();

        var campaign = new Campaign { Id = campaignId, Name = "Campaña 2025/2026" };
        var field = new Field { Id = fieldId, Name = "La Esperanza" };
        var lot = new Lot { Id = lotId, Name = "Lote 1", FieldId = fieldId, Field = field, CadastralArea = 50m };
        var campaignLot = new CampaignLot { Id = Guid.NewGuid(), CampaignId = campaignId, LotId = lotId, Lot = lot, ProductiveArea = 50m };

        var laborType = new LaborType { Id = Guid.NewGuid(), Name = "Fumigación Terrestre" };
        var contact = new Contact { Id = Guid.NewGuid(), FullName = "Contratista Pérez" };

        var wo1 = new WorkOrder { Id = Guid.NewGuid(), OTNumber = "OT-101", CampaignId = campaignId, Status = "Completed" };
        var wo2 = new WorkOrder { Id = Guid.NewGuid(), OTNumber = "OT-102", CampaignId = campaignId, Status = "Completed" };

        var invGlifosato = new Inventory { Id = Guid.NewGuid(), ItemName = "Glifosato 66%", UnitA = "L" };
        var invEnlist = new Inventory { Id = Guid.NewGuid(), ItemName = "2,4-D Enlist", UnitA = "L" };

        var labor1 = new Labor
        {
            Id = Guid.NewGuid(),
            LotId = lotId,
            Lot = lot,
            WorkOrderId = wo1.Id,
            WorkOrder = wo1,
            LaborTypeId = laborType.Id,
            Type = laborType,
            Hectares = 50m,
            ExecutionDate = new DateTime(2026, 1, 15),
            Status = LaborStatus.Realized,
            ContactId = contact.Id,
            Contact = contact
        };

        var labor2 = new Labor
        {
            Id = Guid.NewGuid(),
            LotId = lotId,
            Lot = lot,
            WorkOrderId = wo2.Id,
            WorkOrder = wo2,
            LaborTypeId = laborType.Id,
            Type = laborType,
            Hectares = 50m,
            ExecutionDate = new DateTime(2026, 2, 20),
            Status = LaborStatus.Realized,
            ContactId = contact.Id,
            Contact = contact
        };

        // Same supply applied in both labors: Glifosato (2.0 L/ha first, 3.0 L/ha second)
        var supply1 = new LaborSupply
        {
            Id = Guid.NewGuid(),
            LaborId = labor1.Id,
            SupplyId = invGlifosato.Id,
            Supply = invGlifosato,
            UnitOfMeasure = "L",
            PlannedDose = 2.0m,
            RealDose = 2.0m,
            PlannedTotal = 100m,
            RealTotal = 100m
        };

        var supply2 = new LaborSupply
        {
            Id = Guid.NewGuid(),
            LaborId = labor2.Id,
            SupplyId = invGlifosato.Id,
            Supply = invGlifosato,
            UnitOfMeasure = "L",
            PlannedDose = 3.0m,
            RealDose = 3.0m,
            PlannedTotal = 150m,
            RealTotal = 150m
        };

        // Unique supply in second labor: 2,4-D
        var supply3 = new LaborSupply
        {
            Id = Guid.NewGuid(),
            LaborId = labor2.Id,
            SupplyId = invEnlist.Id,
            Supply = invEnlist,
            UnitOfMeasure = "L",
            PlannedDose = 1.5m,
            RealDose = 1.5m,
            PlannedTotal = 75m,
            RealTotal = 75m
        };

        labor1.Supplies = new List<LaborSupply> { supply1 };
        labor2.Supplies = new List<LaborSupply> { supply2, supply3 };

        var mockCtx = BuildContextMock(
            new List<Campaign> { campaign },
            new List<CampaignLot> { campaignLot },
            new List<Labor> { labor1, labor2 });

        var logger = new Mock<ILogger<TraceabilityReportService>>();
        var service = new TraceabilityReportService(mockCtx.Object, logger.Object);

        var result = await service.GetLotTraceabilityAsync(campaignId, lotId);

        Assert.NotNull(result);
        Assert.Equal("Lote 1", result.LotName);
        Assert.Equal("La Esperanza", result.FieldName);
        Assert.Equal(50m, result.ProductiveArea);

        // Verify repetition totals for supplies
        Assert.Equal(2, result.SupplyTotals.Count);
        var glifosato = result.SupplyTotals.FirstOrDefault(s => s.SupplyName == "Glifosato 66%");
        Assert.NotNull(glifosato);
        Assert.Equal(250m, glifosato.TotalQuantity); // 100 + 150
        Assert.Equal(2, glifosato.ApplicationsCount); // 2 passes
        Assert.Equal(2.5m, glifosato.AverageDose); // 250 / 100 ha

        var enlist = result.SupplyTotals.FirstOrDefault(s => s.SupplyName == "2,4-D Enlist");
        Assert.NotNull(enlist);
        Assert.Equal(75m, enlist.TotalQuantity);
        Assert.Equal(1, enlist.ApplicationsCount);

        // Verify repetition totals for labors
        Assert.Single(result.LaborTotals);
        var laborSummary = result.LaborTotals.First();
        Assert.Equal("Fumigación Terrestre", laborSummary.LaborTypeName);
        Assert.Equal(2, laborSummary.TotalCount);
        Assert.Equal(100m, laborSummary.AccumulatedHectares);

        // Verify timeline
        Assert.Equal(2, result.Timeline.Count);
        Assert.Equal("OT-101", result.Timeline[0].OtNumber);
        Assert.Single(result.Timeline[0].Supplies);
        Assert.Equal("OT-102", result.Timeline[1].OtNumber);
        Assert.Equal(2, result.Timeline[1].Supplies.Count);
    }

    [Fact]
    public async Task GetFieldTraceabilityAsync_ReturnsFieldSummaryAndAggregatesLots()
    {
        var campaignId = Guid.NewGuid();
        var fieldId = Guid.NewGuid();
        var lot1Id = Guid.NewGuid();
        var lot2Id = Guid.NewGuid();

        var campaign = new Campaign { Id = campaignId, Name = "Campaña 2025/2026" };
        var field = new Field { Id = fieldId, Name = "Estancia Grande" };
        var lot1 = new Lot { Id = lot1Id, Name = "Lote Norte", FieldId = fieldId, Field = field, CadastralArea = 100m };
        var lot2 = new Lot { Id = lot2Id, Name = "Lote Sur", FieldId = fieldId, Field = field, CadastralArea = 80m };

        var cl1 = new CampaignLot { Id = Guid.NewGuid(), CampaignId = campaignId, LotId = lot1Id, Lot = lot1, ProductiveArea = 100m };
        var cl2 = new CampaignLot { Id = Guid.NewGuid(), CampaignId = campaignId, LotId = lot2Id, Lot = lot2, ProductiveArea = 80m };

        var laborType = new LaborType { Id = Guid.NewGuid(), Name = "Siembra" };
        var wo = new WorkOrder { Id = Guid.NewGuid(), OTNumber = "OT-200", CampaignId = campaignId, Status = "Completed" };

        var laborLot1 = new Labor
        {
            Id = Guid.NewGuid(),
            LotId = lot1Id,
            Lot = lot1,
            WorkOrderId = wo.Id,
            WorkOrder = wo,
            LaborTypeId = laborType.Id,
            Type = laborType,
            Hectares = 100m,
            ExecutionDate = new DateTime(2026, 3, 1),
            Status = LaborStatus.Realized
        };

        var laborLot2 = new Labor
        {
            Id = Guid.NewGuid(),
            LotId = lot2Id,
            Lot = lot2,
            WorkOrderId = wo.Id,
            WorkOrder = wo,
            LaborTypeId = laborType.Id,
            Type = laborType,
            Hectares = 80m,
            ExecutionDate = new DateTime(2026, 3, 2),
            Status = LaborStatus.Realized
        };

        var invSemilla = new Inventory { Id = Guid.NewGuid(), ItemName = "Semilla Soja DM 46E21", UnitA = "Bolsa" };

        var supply = new LaborSupply
        {
            Id = Guid.NewGuid(),
            LaborId = laborLot1.Id,
            SupplyId = invSemilla.Id,
            Supply = invSemilla,
            UnitOfMeasure = "Bolsa",
            PlannedDose = 2m,
            RealDose = 2m,
            PlannedTotal = 200m,
            RealTotal = 200m
        };

        laborLot1.Supplies = new List<LaborSupply> { supply };
        laborLot2.Supplies = new List<LaborSupply>();

        var mockCtx = BuildContextMock(
            new List<Campaign> { campaign },
            new List<CampaignLot> { cl1, cl2 },
            new List<Labor> { laborLot1, laborLot2 });

        var logger = new Mock<ILogger<TraceabilityReportService>>();
        var service = new TraceabilityReportService(mockCtx.Object, logger.Object);

        var result = await service.GetFieldTraceabilityAsync(campaignId, fieldId);

        Assert.NotNull(result);
        Assert.Equal("Estancia Grande", result.FieldName);
        Assert.Equal(180m, result.TotalArea);
        Assert.Equal(2, result.LotsCount);

        // 2 lots in breakdown
        Assert.Equal(2, result.LotsBreakdown.Count);
        Assert.Contains(result.LotsBreakdown, l => l.LotName == "Lote Norte" && l.SupplyTotals.Count == 1);
        Assert.Contains(result.LotsBreakdown, l => l.LotName == "Lote Sur" && l.SupplyTotals.Count == 0);

        // Field level aggregated supplies
        Assert.Single(result.SupplyTotals);
        Assert.Equal("Semilla Soja DM 46E21", result.SupplyTotals[0].SupplyName);
        Assert.Equal(200m, result.SupplyTotals[0].TotalQuantity);

        // Field level aggregated labors: Siembra across 2 lots (100 ha + 80 ha = 180 ha, 2 passes)
        Assert.Single(result.LaborTotals);
        Assert.Equal(2, result.LaborTotals[0].TotalCount);
        Assert.Equal(180m, result.LaborTotals[0].AccumulatedHectares);
    }
}
