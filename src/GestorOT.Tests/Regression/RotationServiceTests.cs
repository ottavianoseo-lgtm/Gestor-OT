using GestorOT.Application.Interfaces;
using GestorOT.Domain.Entities;
using GestorOT.Infrastructure.Services;
using GestorOT.Shared.Dtos;
using GestorOT.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace GestorOT.Tests.Regression;

/// <summary>
/// Regression tests for Bug #9 — allows saving a rotation with inverted dates (StartDate &gt;= EndDate).
/// </summary>
public class RotationServiceTests
{
    private static Mock<IApplicationDbContext> BuildContextMock()
    {
        var mock = new Mock<IApplicationDbContext>();

        // Empty rotations set so overlap check passes
        var rotations = new List<Rotation>().AsQueryable();
        var rotationsMock = new Mock<DbSet<Rotation>>();
        rotationsMock.As<IAsyncEnumerable<Rotation>>()
            .Setup(m => m.GetAsyncEnumerator(It.IsAny<CancellationToken>()))
            .Returns(new TestAsyncEnumerator<Rotation>(rotations.GetEnumerator()));
        rotationsMock.As<IQueryable<Rotation>>().Setup(m => m.Provider)
            .Returns(new TestAsyncQueryProvider<Rotation>(rotations.Provider));
        rotationsMock.As<IQueryable<Rotation>>().Setup(m => m.Expression).Returns(rotations.Expression);
        rotationsMock.As<IQueryable<Rotation>>().Setup(m => m.ElementType).Returns(rotations.ElementType);
        rotationsMock.As<IQueryable<Rotation>>().Setup(m => m.GetEnumerator()).Returns(rotations.GetEnumerator());
        mock.Setup(c => c.Rotations).Returns(rotationsMock.Object);

        // Empty CampaignLots for warning check
        var campaignLots = new List<CampaignLot>().AsQueryable();
        var campaignLotsMock = new Mock<DbSet<CampaignLot>>();
        campaignLotsMock.As<IAsyncEnumerable<CampaignLot>>()
            .Setup(m => m.GetAsyncEnumerator(It.IsAny<CancellationToken>()))
            .Returns(new TestAsyncEnumerator<CampaignLot>(campaignLots.GetEnumerator()));
        campaignLotsMock.As<IQueryable<CampaignLot>>().Setup(m => m.Provider)
            .Returns(new TestAsyncQueryProvider<CampaignLot>(campaignLots.Provider));
        campaignLotsMock.As<IQueryable<CampaignLot>>().Setup(m => m.Expression).Returns(campaignLots.Expression);
        campaignLotsMock.As<IQueryable<CampaignLot>>().Setup(m => m.ElementType).Returns(campaignLots.ElementType);
        campaignLotsMock.As<IQueryable<CampaignLot>>().Setup(m => m.GetEnumerator()).Returns(campaignLots.GetEnumerator());
        mock.Setup(c => c.CampaignLots).Returns(campaignLotsMock.Object);

        mock.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        mock.Setup(c => c.CurrentTenantId).Returns(Guid.NewGuid());
        return mock;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Bug #9: Rotation with StartDate >= EndDate must be rejected
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateRotation_Should_Fail_When_StartDate_Equals_EndDate()
    {
        var service = new RotationService(BuildContextMock().Object);
        var dto = new RotationDto(
            Guid.Empty,
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            new DateOnly(2026, 6, 1),
            new DateOnly(2026, 6, 1), // equal — invalid
            null);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateRotationAsync(dto));

        Assert.Contains("fecha de inicio", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateRotation_Should_Fail_When_StartDate_Greater_Than_EndDate()
    {
        var service = new RotationService(BuildContextMock().Object);
        var dto = new RotationDto(
            Guid.Empty,
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            new DateOnly(2026, 6, 1),
            new DateOnly(2026, 1, 1), // StartDate > EndDate
            null);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateRotationAsync(dto));

        Assert.Contains("fecha de inicio", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateRotation_Should_Fail_When_StartDate_Greater_Than_EndDate()
    {
        var ctxMock = BuildContextMock();
        var existingId = Guid.NewGuid();
        var existing = new Rotation
        {
            Id = existingId,
            CampaignLotId = Guid.NewGuid(),
            ErpActivityId = Guid.NewGuid(),
            StartDate = new DateOnly(2026, 1, 1),
            EndDate = new DateOnly(2026, 6, 1)
        };
        ctxMock.Setup(c => c.Rotations.FindAsync(
                new object[] { existingId }, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var service = new RotationService(ctxMock.Object);
        var dto = new RotationDto(
            existingId,
            existing.CampaignLotId,
            existing.ErpActivityId,
            null,
            new DateOnly(2026, 6, 1),  // StartDate > EndDate
            new DateOnly(2026, 1, 1),
            null);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.UpdateRotationAsync(existingId, dto));

        Assert.Contains("fecha de inicio", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateRotation_Should_Succeed_With_Valid_Dates()
    {
        var ctxMock = BuildContextMock();
        // Allow Add() to be called without throwing
        ctxMock.Setup(c => c.Rotations.Add(It.IsAny<Rotation>()));

        var service = new RotationService(ctxMock.Object);
        var dto = new RotationDto(
            Guid.Empty,
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 6, 1), // valid
            null);

        // Should not throw — date validation passes
        await service.CreateRotationAsync(dto);

        // Verify SaveChangesAsync was called (rotation was persisted)
        ctxMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
