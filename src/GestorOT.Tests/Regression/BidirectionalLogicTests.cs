using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GestorOT.Application.Interfaces;
using GestorOT.Infrastructure;
using GestorOT.Infrastructure.Data;
using GestorOT.Shared.Dtos;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace GestorOT.Tests.Regression;

public class BidirectionalLogicTests
{
    private DbContextOptions<ApplicationDbContext> CreateOptions()
    {
        return new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
    }

    [Fact]
    public async Task GetByIdAsync_SumsRealTotalsFromLabors()
    {
        // Arrange
        var options = CreateOptions();
        var workOrderId = Guid.NewGuid();
        var supplyId = Guid.NewGuid();
        var fieldId = Guid.NewGuid();
        var lotId = Guid.NewGuid();
        var laborTypeId = Guid.NewGuid();

        using (var ctx = new ApplicationDbContext(options))
        {
            // Create Field
            var field = new GestorOT.Domain.Entities.Field { Id = fieldId, Name = "Campo Test" };
            ctx.Fields.Add(field);

            // Create Lot
            var lot = new GestorOT.Domain.Entities.Lot { Id = lotId, FieldId = fieldId, Name = "Lote Test" };
            ctx.Lots.Add(lot);

            // Create LaborType
            var laborType = new GestorOT.Domain.Entities.LaborType { Id = laborTypeId, Name = "Labor Test" };
            ctx.LaborTypes.Add(laborType);

            // Create Supply (required for navigation)
            var supply = new GestorOT.Domain.Entities.Inventory { Id = supplyId, ItemName = "GLIFOSATO", UnitA = "L" };
            ctx.Inventories.Add(supply);

            // Create Labor with two supplies, each with RealTotal values
            var laborId = Guid.NewGuid();
            var labor = new GestorOT.Domain.Entities.Labor
            {
                Id = laborId,
                WorkOrderId = workOrderId,
                LotId = lotId,
                LaborTypeId = laborTypeId,
                Supplies = new List<GestorOT.Domain.Entities.LaborSupply>
                {
                    new() { Id = Guid.NewGuid(), LaborId = laborId, SupplyId = supplyId, RealTotal = 30m },
                    new() { Id = Guid.NewGuid(), LaborId = laborId, SupplyId = supplyId, RealTotal = 20m }
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
        }

        // Act & Assert
        using (var ctx = new ApplicationDbContext(options))
        {
            var campaignServiceMock = new Mock<ICampaignContextService>();
            campaignServiceMock.Setup(c => c.CurrentCampaignId).Returns((Guid?)null);
            var service = new GestorOT.Infrastructure.Services.WorkOrderQueryService(ctx, campaignServiceMock.Object);

            var result = await service.GetByIdAsync(workOrderId);

            // Assert
            Assert.NotNull(result);
            var dto = Assert.Single(result.SupplyApprovals);
            // Expect sum of 30 + 20 = 50
            Assert.Equal(50m, dto.SumOfLaborsRealTotal);
        }
    }
}
