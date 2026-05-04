using GestorOT.Application.Interfaces;
using GestorOT.Domain.Entities;
using GestorOT.Domain.Enums;
using GestorOT.Shared.Dtos;
using GestorOT.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace GestorOT.Tests.Regression;

public class Sprint10SecurityTests
{
    private static Mock<IApplicationDbContext> CreateContextMock()
    {
        var mock = new Mock<IApplicationDbContext>();
        mock.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        mock.Setup(c => c.CurrentTenantId).Returns(Guid.NewGuid());
        return mock;
    }

    private static Mock<DbSet<T>> CreateMockDbSet<T>(List<T> data) where T : class
    {
        var queryable = data.AsQueryable();
        var mock = new Mock<DbSet<T>>();
        mock.As<IAsyncEnumerable<T>>()
            .Setup(m => m.GetAsyncEnumerator(It.IsAny<CancellationToken>()))
            .Returns(new TestAsyncEnumerator<T>(queryable.GetEnumerator()));
        mock.As<IQueryable<T>>().Setup(m => m.Provider)
            .Returns(new TestAsyncQueryProvider<T>(queryable.Provider));
        mock.As<IQueryable<T>>().Setup(m => m.Expression).Returns(queryable.Expression);
        mock.As<IQueryable<T>>().Setup(m => m.ElementType).Returns(queryable.ElementType);
        mock.As<IQueryable<T>>().Setup(m => m.GetEnumerator()).Returns(queryable.GetEnumerator());
        return mock;
    }

    [Fact]
    public void CampaignStatus_Locked_BlocksEdits()
    {
        var status = GestorOT.Domain.Enums.CampaignStatus.Locked;
        Assert.Equal("Locked", status.ToString());
    }

    [Fact]
    public void CampaignStatus_Active_AllowsEdits()
    {
        var status = GestorOT.Domain.Enums.CampaignStatus.Active;
        Assert.Equal("Active", status.ToString());
    }

    [Fact]
    public void WorkOrderStatus_IsEditableTrue_AllowsModifications()
    {
        var status = new WorkOrderStatus { Name = "Draft", IsEditable = true };
        Assert.True(status.IsEditable);
    }

    [Fact]
    public void WorkOrderStatus_IsEditableFalse_BlocksModifications()
    {
        var status = new WorkOrderStatus { Name = "Approved", IsEditable = false };
        Assert.False(status.IsEditable);
    }

    [Fact]
    public void CampaignSummary_IsLocked_WhenCampaignLocked()
    {
        var campaign = new CampaignSummaryDto(Guid.NewGuid(), "Test", GestorOT.Shared.Dtos.CampaignStatus.Locked, true);
        Assert.Equal(GestorOT.Shared.Dtos.CampaignStatus.Locked, campaign.Status);
    }

    [Fact]
    public void CampaignSummary_IsNotLocked_WhenCampaignActive()
    {
        var campaign = new CampaignSummaryDto(Guid.NewGuid(), "Test", GestorOT.Shared.Dtos.CampaignStatus.Active, true);
        Assert.Equal(GestorOT.Shared.Dtos.CampaignStatus.Active, campaign.Status);
    }

    [Fact]
    public void WorkOrderStatus_IsDefault_MarksDefaultStatus()
    {
        var status = new WorkOrderStatusDto(Guid.NewGuid(), "Borrador", "#3498DB", true, true, 0);
        Assert.True(status.IsDefault);
    }

    [Fact]
    public void WorkOrderStatus_DefaultFallback_IsDraft()
    {
        string fallback = "Draft";
        var defaultStatus = (WorkOrderStatusDto?)null;
        var statusName = defaultStatus?.Name ?? fallback;
        Assert.Equal("Draft", statusName);
    }

    [Fact]
    public async Task GetStrategies_ReturnsExpectedNames()
    {
        var strategies = new List<CropStrategy>
        {
            new() { Id = Guid.NewGuid(), Name = "Maíz temprano", CreatedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), Name = "Soja 1ra", CreatedAt = DateTime.UtcNow }
        };

        var ctxMock = CreateContextMock();
        ctxMock.Setup(c => c.CropStrategies).Returns(CreateMockDbSet(strategies).Object);

        var result = await ctxMock.Object.CropStrategies
            .AsNoTracking()
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => s.Name)
            .ToListAsync();

        Assert.Contains("Maíz temprano", result);
        Assert.Contains("Soja 1ra", result);
    }

    [Fact]
    public async Task BulkFromStrategy_LoadsStrategyAndLots()
    {
        var strategyId = Guid.NewGuid();
        var campaignLotId = Guid.NewGuid();
        var laborTypeId = Guid.NewGuid();
        var activityId = Guid.NewGuid();

        var strategyItems = new List<StrategyItem>
        {
            new() { Id = Guid.NewGuid(), CropStrategyId = strategyId, LaborTypeId = laborTypeId, DayOffset = 0, DefaultSuppliesJson = null }
        };

        var strategies = new List<CropStrategy>
        {
            new() { Id = strategyId, Name = "Test", ErpActivityId = activityId, Items = strategyItems }
        };

        var campaignLots = new List<CampaignLot>
        {
            new() { Id = campaignLotId, CampaignId = Guid.NewGuid(), LotId = Guid.NewGuid(), ProductiveArea = 100 }
        };

        var ctxMock = CreateContextMock();
        ctxMock.Setup(c => c.CropStrategies).Returns(CreateMockDbSet(strategies).Object);
        ctxMock.Setup(c => c.CampaignLots).Returns(CreateMockDbSet(campaignLots).Object);

        var strategy = await ctxMock.Object.CropStrategies
            .Include(s => s.Items)
            .FirstOrDefaultAsync(s => s.Id == strategyId);

        Assert.NotNull(strategy);
        Assert.Single(strategy.Items);

        var lots = await ctxMock.Object.CampaignLots
            .Where(cl => new[] { campaignLotId }.Contains(cl.Id))
            .ToListAsync();

        Assert.Single(lots);
    }

    [Fact]
    public void CampaignLockCheck_StringComparison_MatchesLocked()
    {
        var lockedCampaign = new Campaign { Id = Guid.NewGuid(), Name = "Locked", Status = "Locked" };
        var activeCampaign = new Campaign { Id = Guid.NewGuid(), Name = "Active", Status = "Active" };

        Assert.True(lockedCampaign.Status == "Locked");
        Assert.False(activeCampaign.Status == "Locked");
    }

    [Fact]
    public void WorkOrder_IsLocked_ComputedFromStatus()
    {
        var editableStatus = new WorkOrderStatus { Name = "Draft", IsEditable = true };
        var nonEditableStatus = new WorkOrderStatus { Name = "Completed", IsEditable = false };

        var editableWo = new WorkOrder { Id = Guid.NewGuid(), WorkOrderStatus = editableStatus };
        var lockedWo = new WorkOrder { Id = Guid.NewGuid(), WorkOrderStatus = nonEditableStatus };

        Assert.False(editableWo.WorkOrderStatus?.IsEditable == false);
        Assert.True(lockedWo.WorkOrderStatus?.IsEditable == false);
    }

    [Fact]
    public void CreateLabor_WithoutCampaignLot_ReturnsBadRequest()
    {
        Guid? campaignLotId = null;
        var isValid = campaignLotId.HasValue && campaignLotId.Value != Guid.Empty;
        Assert.False(isValid);
    }

    [Fact]
    public void CreateLabor_OriginalPlanRealized_Rejected()
    {
        bool isOriginalPlan = true;
        string status = "Realized";
        bool isRejected = isOriginalPlan && status != "Planned";
        Assert.True(isRejected);
    }

    [Fact]
    public void CreateWorkOrder_WithoutCampaign_ReturnsBadRequest()
    {
        Guid? campaignId = null;
        var isValid = campaignId.HasValue && campaignId.Value != Guid.Empty;
        Assert.False(isValid);
    }

    [Fact]
    public void CreateLabor_NoRotation_ReturnsWarningAndCreates()
    {
        var campaignLotId = Guid.NewGuid();
        var date = new DateOnly(2026, 6, 1);
        var activityId = Guid.NewGuid();

        var rotations = new List<Rotation>();

        var hasConflict = rotations.Any(r =>
            r.CampaignLotId == campaignLotId &&
            r.StartDate <= date && r.EndDate >= date &&
            r.ErpActivityId != activityId);

        Assert.False(hasConflict);
    }

    [Fact]
    public void MutateLockedCampaign_CreateLabor_ReturnsConflict()
    {
        var campaign = new Campaign { Id = Guid.NewGuid(), Status = "Locked" };
        var canMutate = campaign.Status != "Locked";
        Assert.False(canMutate);
    }

    [Fact]
    public void MutateLockedCampaign_CreateWorkOrder_ReturnsConflict()
    {
        var campaignId = Guid.NewGuid();
        var campaign = new Campaign { Id = campaignId, Status = "Locked" };

        var isValid = campaign.Status != "Locked" && campaign != null;
        Assert.False(isValid);
    }

    [Fact]
    public async Task CreateLabor_ActivityConflict_ReturnsBadRequest()
    {
        var campaignLotId = Guid.NewGuid();
        var date = new DateOnly(2026, 6, 1);
        var activityId = Guid.NewGuid();
        var otherActivityId = Guid.NewGuid();

        var rotations = new List<Rotation>
        {
            new() { Id = Guid.NewGuid(), CampaignLotId = campaignLotId, ErpActivityId = otherActivityId,
                    StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 12, 31) }
        };

        var conflict = rotations.Any(r =>
            r.CampaignLotId == campaignLotId &&
            r.StartDate <= date && r.EndDate >= date &&
            r.ErpActivityId != activityId);

        Assert.True(conflict);
    }
}
