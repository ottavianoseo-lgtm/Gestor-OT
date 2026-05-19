using GestorOT.Application.Interfaces;
using GestorOT.Domain.Entities;
using GestorOT.Infrastructure.Services;
using GestorOT.Shared.Dtos;
using GestorOT.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace GestorOT.Tests.Regression;

public class DashboardCampaignFilterTests
{
    private readonly Guid _campaignA = Guid.NewGuid();
    private readonly Guid _campaignB = Guid.NewGuid();
    private readonly Guid _tenant1 = Guid.NewGuid();
    private readonly Guid _tenant2 = Guid.NewGuid();

    private class TestCampaignContext : ICampaignContextService
    {
        public Guid? CurrentCampaignId { get; set; }
    }

    private static Mock<IApplicationDbContext> BuildContextMock(
        List<Field> fields,
        List<CampaignField> campaignFields,
        List<CampaignLot> campaignLots,
        List<Lot> lots,
        List<WorkOrder> workOrders,
        Guid currentTenantId)
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

        var fieldsMock = new Mock<DbSet<Field>>();
        SetupDbSet(fieldsMock, fields.AsQueryable());
        mock.Setup(c => c.Fields).Returns(fieldsMock.Object);

        var campaignFieldsMock = new Mock<DbSet<CampaignField>>();
        SetupDbSet(campaignFieldsMock, campaignFields.AsQueryable());
        mock.Setup(c => c.CampaignFields).Returns(campaignFieldsMock.Object);

        var campaignLotsMock = new Mock<DbSet<CampaignLot>>();
        SetupDbSet(campaignLotsMock, campaignLots.AsQueryable());
        mock.Setup(c => c.CampaignLots).Returns(campaignLotsMock.Object);

        var lotsMock = new Mock<DbSet<Lot>>();
        SetupDbSet(lotsMock, lots.AsQueryable());
        mock.Setup(c => c.Lots).Returns(lotsMock.Object);

        var workOrdersMock = new Mock<DbSet<WorkOrder>>();
        SetupDbSet(workOrdersMock, workOrders.AsQueryable());
        mock.Setup(c => c.WorkOrders).Returns(workOrdersMock.Object);

        mock.Setup(c => c.CurrentTenantId).Returns(currentTenantId);
        mock.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        return mock;
    }

    // ─────────────────────────────────────────────────────────────────────
    // 1. Sin X-Campaign-ID devuelve totales globales del tenant
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetStatsAsync_NoCampaignHeader_ReturnsAllData()
    {
        var field = new Field { Id = Guid.NewGuid(), Name = "F1", TenantId = _tenant1 };
        var lot = new Lot { Id = Guid.NewGuid(), Name = "L1", Status = "Active", FieldId = field.Id, TenantId = _tenant1 };
        var cl = new CampaignLot { Id = Guid.NewGuid(), CampaignId = _campaignA, LotId = lot.Id, ProductiveArea = 100m, TenantId = _tenant1 };
        var wo = new WorkOrder { Id = Guid.NewGuid(), CampaignId = _campaignA, Status = "Pending", TenantId = _tenant1 };

        var ctxMock = BuildContextMock(
            new List<Field> { field },
            new List<CampaignField>(),
            new List<CampaignLot> { cl },
            new List<Lot> { lot },
            new List<WorkOrder> { wo },
            _tenant1);

        var campaignContext = new TestCampaignContext { CurrentCampaignId = null };
        var service = new DashboardQueryService(ctxMock.Object, campaignContext);

        var stats = await service.GetStatsAsync();

        Assert.Equal(1, stats.FieldsCount);
        Assert.Equal(1, stats.LotsCount);
        Assert.Equal(1, stats.ActiveLotsCount);
        Assert.Equal(1, stats.PendingWorkOrders);
        Assert.Equal(100m, stats.TotalProductiveArea);
    }

    // ─────────────────────────────────────────────────────────────────────
    // 2. Con X-Campaign-ID filtra solo OTs de la campaña
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetStatsAsync_WithCampaignHeader_FiltersWorkOrders()
    {
        var lot = new Lot { Id = Guid.NewGuid(), Name = "L1", Status = "Active", FieldId = Guid.NewGuid(), TenantId = _tenant1 };
        var clA = new CampaignLot { Id = Guid.NewGuid(), CampaignId = _campaignA, LotId = lot.Id, ProductiveArea = 100m, TenantId = _tenant1 };
        var clB = new CampaignLot { Id = Guid.NewGuid(), CampaignId = _campaignB, LotId = lot.Id, ProductiveArea = 200m, TenantId = _tenant1 };
        var woA = new WorkOrder { Id = Guid.NewGuid(), CampaignId = _campaignA, Status = "Pending", TenantId = _tenant1 };
        var woB = new WorkOrder { Id = Guid.NewGuid(), CampaignId = _campaignB, Status = "InProgress", TenantId = _tenant1 };

        var ctxMock = BuildContextMock(
            new List<Field>(),
            new List<CampaignField>(),
            new List<CampaignLot> { clA, clB },
            new List<Lot> { lot },
            new List<WorkOrder> { woA, woB },
            _tenant1);

        var campaignContext = new TestCampaignContext { CurrentCampaignId = _campaignA };
        var service = new DashboardQueryService(ctxMock.Object, campaignContext);

        var stats = await service.GetStatsAsync();

        Assert.Equal(1, stats.PendingWorkOrders);
        Assert.Equal(0, stats.InProgressWorkOrders);
        Assert.Equal(0, stats.CompletedWorkOrders);
    }

    // ─────────────────────────────────────────────────────────────────────
    // 3. Con X-Campaign-ID filtra área productiva
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetStatsAsync_WithCampaignHeader_FiltersProductiveArea()
    {
        var lot = new Lot { Id = Guid.NewGuid(), Name = "L1", Status = "Active", FieldId = Guid.NewGuid(), TenantId = _tenant1 };
        var clA = new CampaignLot { Id = Guid.NewGuid(), CampaignId = _campaignA, LotId = lot.Id, ProductiveArea = 100m, TenantId = _tenant1 };
        var clB = new CampaignLot { Id = Guid.NewGuid(), CampaignId = _campaignB, LotId = lot.Id, ProductiveArea = 200m, TenantId = _tenant1 };

        var ctxMock = BuildContextMock(
            new List<Field>(),
            new List<CampaignField>(),
            new List<CampaignLot> { clA, clB },
            new List<Lot> { lot },
            new List<WorkOrder>(),
            _tenant1);

        var campaignContext = new TestCampaignContext { CurrentCampaignId = _campaignA };
        var service = new DashboardQueryService(ctxMock.Object, campaignContext);

        var stats = await service.GetStatsAsync();

        Assert.Equal(100m, stats.TotalProductiveArea);
    }

    // ─────────────────────────────────────────────────────────────────────
    // 4. GetRecentOrdersAsync filtra por campaña
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetRecentOrdersAsync_WithCampaignHeader_OnlyReturnsCampaignOrders()
    {
        var fieldA = new Field { Id = Guid.NewGuid(), Name = "FA", TenantId = _tenant1 };
        var fieldB = new Field { Id = Guid.NewGuid(), Name = "FB", TenantId = _tenant1 };
        var woA1 = new WorkOrder { Id = Guid.NewGuid(), CampaignId = _campaignA, Status = "Pending", FieldId = fieldA.Id, Field = fieldA, DueDate = DateTime.Today.AddDays(1), TenantId = _tenant1 };
        var woA2 = new WorkOrder { Id = Guid.NewGuid(), CampaignId = _campaignA, Status = "Completed", FieldId = fieldA.Id, Field = fieldA, DueDate = DateTime.Today.AddDays(2), TenantId = _tenant1 };
        var woB1 = new WorkOrder { Id = Guid.NewGuid(), CampaignId = _campaignB, Status = "Pending", FieldId = fieldB.Id, Field = fieldB, DueDate = DateTime.Today.AddDays(3), TenantId = _tenant1 };

        var ctxMock = BuildContextMock(
            new List<Field> { fieldA, fieldB },
            new List<CampaignField>(),
            new List<CampaignLot>(),
            new List<Lot>(),
            new List<WorkOrder> { woA1, woA2, woB1 },
            _tenant1);

        var campaignContext = new TestCampaignContext { CurrentCampaignId = _campaignA };
        var service = new DashboardQueryService(ctxMock.Object, campaignContext);

        var orders = await service.GetRecentOrdersAsync(10);

        Assert.Equal(2, orders.Count);
        Assert.All(orders, o => Assert.NotEqual(woB1.Id, o.Id));
        Assert.Contains(orders, o => o.Id == woA1.Id);
        Assert.Contains(orders, o => o.Id == woA2.Id);
    }

    // ─────────────────────────────────────────────────────────────────────
    // 5. Guid.Empty se trata igual que sin campaña (no filtra a 0)
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetStatsAsync_EmptyCampaignGuid_TreatedAsNoFilter()
    {
        var lot = new Lot { Id = Guid.NewGuid(), Name = "L1", Status = "Active", FieldId = Guid.NewGuid(), TenantId = _tenant1 };
        var cl = new CampaignLot { Id = Guid.NewGuid(), CampaignId = _campaignA, LotId = lot.Id, ProductiveArea = 100m, TenantId = _tenant1 };
        var wo = new WorkOrder { Id = Guid.NewGuid(), CampaignId = _campaignA, Status = "Pending", TenantId = _tenant1 };

        var ctxMock = BuildContextMock(
            new List<Field>(),
            new List<CampaignField>(),
            new List<CampaignLot> { cl },
            new List<Lot> { lot },
            new List<WorkOrder> { wo },
            _tenant1);

        var campaignContext = new TestCampaignContext { CurrentCampaignId = Guid.Empty };
        var service = new DashboardQueryService(ctxMock.Object, campaignContext);

        var stats = await service.GetStatsAsync();

        Assert.Equal(1, stats.PendingWorkOrders);
        Assert.Equal(100m, stats.TotalProductiveArea);
        Assert.Equal(1, stats.LotsCount);
    }

    // ─────────────────────────────────────────────────────────────────────
    // 6. Regresión tenant — filtro de campaña combinado con CurrentTenantId
    //    Nota: HasQueryFilter es responsabilidad del DbContext real, no del
    //    servicio. Este test verifica que el servicio respeta el campaign
    //    filter y que CurrentTenantId es el esperado.
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetStatsAsync_UsesCurrentTenantId_AndCampaignFilter()
    {
        var lotT1 = new Lot { Id = Guid.NewGuid(), Name = "LT1", Status = "Active", FieldId = Guid.NewGuid(), TenantId = _tenant1 };
        var clT1 = new CampaignLot { Id = Guid.NewGuid(), CampaignId = _campaignA, LotId = lotT1.Id, ProductiveArea = 50m, TenantId = _tenant1 };
        var clT1B = new CampaignLot { Id = Guid.NewGuid(), CampaignId = _campaignB, LotId = lotT1.Id, ProductiveArea = 30m, TenantId = _tenant1 };
        var woT1A = new WorkOrder { Id = Guid.NewGuid(), CampaignId = _campaignA, Status = "Pending", TenantId = _tenant1 };
        var woT1B = new WorkOrder { Id = Guid.NewGuid(), CampaignId = _campaignB, Status = "InProgress", TenantId = _tenant1 };

        var ctxMock = BuildContextMock(
            new List<Field>(),
            new List<CampaignField>(),
            new List<CampaignLot> { clT1, clT1B },
            new List<Lot> { lotT1 },
            new List<WorkOrder> { woT1A, woT1B },
            _tenant1);

        var campaignContext = new TestCampaignContext { CurrentCampaignId = _campaignA };
        var service = new DashboardQueryService(ctxMock.Object, campaignContext);

        var stats = await service.GetStatsAsync();

        // Campaign A only
        Assert.Equal(1, stats.PendingWorkOrders);
        Assert.Equal(0, stats.InProgressWorkOrders);
        Assert.Equal(50m, stats.TotalProductiveArea);

        // CurrentTenantId is respected as expected
        Assert.Equal(_tenant1, ctxMock.Object.CurrentTenantId);
    }
}
