using System.Text.Json;
using GestorOT.Shared;
using GestorOT.Shared.Dtos;

namespace GestorOT.Tests.Regression;

public class SupplyApprovalDtoTests
{
    [Fact]
    public void WorkOrderSupplyApprovalDto_HasSupplyUnitField()
    {
        var prop = typeof(WorkOrderSupplyApprovalDto).GetProperty("SupplyUnit");
        Assert.NotNull(prop);
        Assert.Equal(typeof(string), prop!.PropertyType);
    }

    [Fact]
    public void WorkOrderSupplyApprovalDto_Constructor_SetsSupplyUnit()
    {
        var dto = new WorkOrderSupplyApprovalDto(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Glifosato", "L", 100m, 80m, "Depósito Norte", 75m);

        Assert.Equal("L", dto.SupplyUnit);
    }

    [Fact]
    public void WorkOrderSupplyApprovalDto_NullSupplyUnit_DoesNotCrash()
    {
        var dto = new WorkOrderSupplyApprovalDto(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Glifosato", null, 100m, 80m, null, 75m);

        Assert.Null(dto.SupplyUnit);
        Assert.Null(dto.WithdrawalCenter);
    }

    [Fact]
    public void UpdateApprovals_PersistsApprovedWithdrawalAndWithdrawalCenter()
    {
        var id = Guid.NewGuid();
        var woId = Guid.NewGuid();
        var supplyId = Guid.NewGuid();

        var dto = new WorkOrderSupplyApprovalDto
        {
            Id = id,
            WorkOrderId = woId,
            SupplyId = supplyId,
            SupplyName = "Atrazina",
            SupplyUnit = "L",
            TotalCalculated = 50m,
            ApprovedWithdrawal = 45m,
            WithdrawalCenter = "Depósito Central",
            RealTotalUsed = 42m
        };

        Assert.Equal(45m, dto.ApprovedWithdrawal);
        Assert.Equal("Depósito Central", dto.WithdrawalCenter);
        Assert.Equal(42m, dto.RealTotalUsed);
    }

    [Fact]
    public void WorkOrderSupplyApprovalDto_Serialization_RoundTripsSupplyUnit()
    {
        var dto = new WorkOrderSupplyApprovalDto(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "2,4-D", "L", 200m, 180m, "Depósito Sur", 175m);

        var json = JsonSerializer.Serialize(dto, AppJsonSerializerContext.Default.WorkOrderSupplyApprovalDto);
        var deserialized = JsonSerializer.Deserialize(json, AppJsonSerializerContext.Default.WorkOrderSupplyApprovalDto);

        Assert.NotNull(deserialized);
        Assert.Equal("2,4-D", deserialized!.SupplyName);
        Assert.Equal("L", deserialized.SupplyUnit);
        Assert.Equal(200m, deserialized.TotalCalculated);
        Assert.Equal(180m, deserialized.ApprovedWithdrawal);
        Assert.Equal("Depósito Sur", deserialized.WithdrawalCenter);
        Assert.Equal(175m, deserialized.RealTotalUsed);
    }

    [Fact]
    public void WorkOrderSupplyApprovalDto_ListSerialization_RoundTrips()
    {
        var list = new List<WorkOrderSupplyApprovalDto>
        {
            new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Ins A", "kg", 10, 10, "C1", 10),
            new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Ins B", "L", 20, 15, "C2", null)
        };

        var json = JsonSerializer.Serialize(list, AppJsonSerializerContext.Default.ListWorkOrderSupplyApprovalDto);
        var deserialized = JsonSerializer.Deserialize(json, AppJsonSerializerContext.Default.ListWorkOrderSupplyApprovalDto);

        Assert.NotNull(deserialized);
        Assert.Equal(2, deserialized!.Count);
        Assert.Equal("kg", deserialized[0].SupplyUnit);
        Assert.Equal("L", deserialized[1].SupplyUnit);
        Assert.Null(deserialized[1].RealTotalUsed);
    }
}
