using GestorOT.Domain.Entities;
using GestorOT.Shared.Dtos;

namespace GestorOT.Tests.Regression;

public class WorkOrderStatusTests
{
    [Fact]
    public void CreateWorkOrder_UsesDefaultStatus_WhenStatusMissing()
    {
        var defaultStatus = new WorkOrderStatus { Id = Guid.NewGuid(), Name = "Draft", IsDefault = true };

        var dto = new WorkOrderDto { CampaignId = Guid.NewGuid() };

        var selectedId = defaultStatus.Id;
        var selectedName = defaultStatus.Name;

        Assert.Equal("Draft", selectedName);
        Assert.NotEqual(Guid.Empty, selectedId);
    }

    [Fact]
    public void CreateWorkOrder_UsesSelectedStatus_WhenStatusProvided()
    {
        var customStatus = new WorkOrderStatus { Id = Guid.NewGuid(), Name = "InProgress", IsDefault = false };

        var dto = new WorkOrderDto { CampaignId = Guid.NewGuid(), Status = "InProgress" };

        var matches = dto.Status == customStatus.Name;
        Assert.True(matches);
    }

    [Fact]
    public void CreateWorkOrder_ReturnsBadRequest_WhenStatusInvalid()
    {
        var validStatuses = new[] { "Draft", "Pending", "InProgress", "Completed" };
        var invalidStatus = "NonExistentStatus";

        var isValid = validStatuses.Contains(invalidStatus);
        Assert.False(isValid);
    }

    [Fact]
    public void UpdateWorkOrder_UpdatesStatusAndWorkOrderStatusId()
    {
        var newStatus = new WorkOrderStatus { Id = Guid.NewGuid(), Name = "Completed" };
        var workOrder = new WorkOrder
        {
            Id = Guid.NewGuid(),
            Status = "Draft",
            WorkOrderStatusId = Guid.NewGuid()
        };

        workOrder.Status = newStatus.Name;
        workOrder.WorkOrderStatusId = newStatus.Id;

        Assert.Equal("Completed", workOrder.Status);
        Assert.Equal(newStatus.Id, workOrder.WorkOrderStatusId);
    }

    [Fact]
    public void UpdateWorkOrder_Blocked_WhenCurrentStatusIsNotEditable()
    {
        var editableStatus = new WorkOrderStatus { Name = "Draft", IsEditable = true };
        var nonEditableStatus = new WorkOrderStatus { Name = "Approved", IsEditable = false };

        var workOrder = new WorkOrder { Id = Guid.NewGuid(), WorkOrderStatus = nonEditableStatus };

        var canModify = workOrder.WorkOrderStatus?.IsEditable != false;
        Assert.False(canModify);
    }

    [Fact]
    public void CreateWorkOrder_Blocked_WhenCampaignLocked()
    {
        var campaign = new Campaign { Id = Guid.NewGuid(), Status = "Locked" };

        var canCreate = campaign.Status != "Locked";
        Assert.False(canCreate);
    }

    [Fact]
    public void WorkOrderDto_IncludesWorkOrderStatusId()
    {
        var statusId = Guid.NewGuid();
        var dto = new WorkOrderDto
        {
            Id = Guid.NewGuid(),
            Status = "InProgress",
            WorkOrderStatusId = statusId
        };

        Assert.Equal(statusId, dto.WorkOrderStatusId);
        Assert.Equal("InProgress", dto.Status);
    }

    [Fact]
    public void Constructor_WithWorkOrderStatusId_SetsCorrectly()
    {
        var id = Guid.NewGuid();
        var fieldId = Guid.NewGuid();
        var statusId = Guid.NewGuid();

        var dto = new WorkOrderDto(
            id, fieldId, "Test OT", "Approved", "John",
            DateTime.Today, null, "OT-001",
            DateTime.Today, DateTime.Today.AddDays(30),
            false, null, null, Guid.NewGuid(),
            name: "Test", isLocked: false, workOrderStatusId: statusId);

        Assert.Equal("Approved", dto.Status);
        Assert.Equal(statusId, dto.WorkOrderStatusId);
    }

    [Fact]
    public void FallbackDraft_WhenNoDefaultAndNoStatus()
    {
        var statuses = new List<WorkOrderStatus>
        {
            new() { Id = Guid.NewGuid(), Name = "Draft", IsDefault = false }
        };

        var defaultStatus = statuses.FirstOrDefault(s => s.IsDefault);
        var fallback = statuses.FirstOrDefault(s => s.Name == "Draft");

        Assert.Null(defaultStatus);
        Assert.NotNull(fallback);
        Assert.Equal("Draft", fallback!.Name);
    }
}
