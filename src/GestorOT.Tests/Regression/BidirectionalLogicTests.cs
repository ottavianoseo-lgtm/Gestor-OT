using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GestorOT.Application.Interfaces;
using GestorOT.Infrastructure;
using GestorOT.Shared.Dtos;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace GestorOT.Tests.Regression;

public class BidirectionalLogicTests
{
    private IApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var context = new ApplicationDbContext(options);
        return context;
    }

    [Fact]
    public async Task GetByIdAsync_SumsRealTotalsFromLabors()
    {
        // Arrange
        var ctx = CreateContext();
        var workOrderId = Guid.NewGuid();
        var supplyId = Guid.NewGuid();

        // Create Supply (required for navigation)
        var supply = new GestorOT.Domain.Entities.Supply { Id = supplyId, ItemName = "GLIFOSATO", UnitA = "L" };
        ctx.Supplies.Add(supply);

        // Create Labor with two supplies, each with RealTotal values
        var labor = new GestorOT.Domain.Entities.Labor
        {
            Id = Guid.NewGuid(),
            WorkOrderId = workOrderId,
            Supplies = new List<GestorOT.Domain.Entities.LaborSupply>
            {
                new() { Id = Guid.NewGuid(), SupplyId = supplyId, RealTotal = 30m },
                new() { Id = Guid.NewGuid(), SupplyId = supplyId, RealTotal = 20m }
            }
        };
        ctx.Labors.Add(labor);

        // SupplyApproval without RealTotalUsed (manual override absent)
        var approval = new GestorOT.Domain.Entities.WorkOrderSupplyApproval
        {
            Id = Guid.NewGuid(),
            WorkOrderId = workOrderId,
            SupplyId = supplyId,
            RealTotalUsed = null,
            TotalCalculated = 0m,
            ApprovedWithdrawal = 0m,
            WithdrawalCenter = null
        };
        ctx.WorkOrderSupplyApprovals.Add(approval);

        // WorkOrder linking everything
        var workOrder = new GestorOT.Domain.Entities.WorkOrder
        {
            Id = workOrderId,
            Labors = new List<GestorOT.Domain.Entities.Labor> { labor },
            SupplyApprovals = new List<GestorOT.Domain.Entities.WorkOrderSupplyApproval> { approval }
        };
        ctx.WorkOrders.Add(workOrder);

        await ctx.SaveChangesAsync();

        var campaignServiceMock = new Mock<ICampaignContextService>();
        campaignServiceMock.Setup(c => c.CurrentCampaignId).Returns((Guid?)null);
        var service = new GestorOT.Infrastructure.Services.WorkOrderQueryService(ctx, campaignServiceMock.Object);

        // Act
        var result = await service.GetByIdAsync(workOrderId);

        // Assert
        Assert.NotNull(result);
        var dto = Assert.Single(result.SupplyApprovals);
        // Expect sum of 30 + 20 = 50
        Assert.Equal(50m, dto.SumOfLaborsRealTotal);
    }
}
