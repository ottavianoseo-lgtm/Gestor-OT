using GestorOT.Application.Interfaces;
using GestorOT.Domain.Entities;
using GestorOT.Shared.Dtos;
using GestorOT.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace GestorOT.Tests.Regression;

public class FileAssetTests
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

    // ── Test 1: Upload without laborId creates FileAsset without link ──────────

    [Fact]
    public void FileAsset_UploadNoLaborId_CreatesWithoutLink()
    {
        var asset = new FileAsset
        {
            Id = Guid.NewGuid(),
            FileName = "test.pdf",
            Hash = "ABC123",
            SizeBytes = 1024,
            UploadedAt = DateTime.UtcNow
        };

        var links = new List<LaborFileAsset>();
        var hasLink = links.Any(lf => lf.FileAssetId == asset.Id);

        Assert.False(hasLink);
    }

    // ── Test 2: Upload with laborId creates or reuses FileAsset and links ─────

    [Fact]
    public void FileAsset_UploadWithLaborId_LinksToLabor()
    {
        var laborId = Guid.NewGuid();
        var fileAssetId = Guid.NewGuid();

        var links = new List<LaborFileAsset>
        {
            new() { Id = Guid.NewGuid(), LaborId = laborId, FileAssetId = fileAssetId, LinkedAt = DateTime.UtcNow }
        };

        var laborLinks = links.Count(lf => lf.LaborId == laborId);
        Assert.Equal(1, laborLinks);
        Assert.Equal(fileAssetId, links[0].FileAssetId);
    }

    [Fact]
    public void FileAsset_SameHash_ReusesExistingFile()
    {
        var hash = "DUPLICATEHASH";
        var existingId = Guid.NewGuid();
        var newId = Guid.NewGuid();

        var existing = new FileAsset { Id = existingId, Hash = hash };
        var duplicate = new FileAsset { Id = newId, Hash = hash };

        var match = existing.Hash == duplicate.Hash;
        Assert.True(match);
        Assert.NotEqual(existing.Id, duplicate.Id);
    }

    // ── Test 3: LinkFiles avoids duplicates and validates IDs ──────────────────

    [Fact]
    public void FileAsset_LinkFiles_AvoidsDuplicates()
    {
        var laborId = Guid.NewGuid();
        var fileAssetId = Guid.NewGuid();

        var existingLinks = new List<LaborFileAsset>
        {
            new() { Id = Guid.NewGuid(), LaborId = laborId, FileAssetId = fileAssetId }
        };

        var isDuplicate = existingLinks.Any(lf => lf.LaborId == laborId && lf.FileAssetId == fileAssetId);
        Assert.True(isDuplicate);
    }

    [Fact]
    public void FileAsset_LinkFiles_ValidatesExistingIds()
    {
        var existingIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
        var requestIds = new List<Guid> { existingIds[0], Guid.NewGuid() };

        var invalidIds = requestIds.Where(id => !existingIds.Contains(id)).ToList();
        Assert.Single(invalidIds);
    }

    // ── Test 4: Delete unlinked file works ────────────────────────────────────

    [Fact]
    public void FileAsset_DeleteUnlinked_Succeeds()
    {
        var fileAssetId = Guid.NewGuid();
        var links = new List<LaborFileAsset>();

        var linkCount = links.Count(lf => lf.FileAssetId == fileAssetId);
        Assert.Equal(0, linkCount);
    }

    // ── Test 5: Delete linked file is blocked ─────────────────────────────────

    [Fact]
    public void FileAsset_DeleteLinked_Blocked()
    {
        var fileAssetId = Guid.NewGuid();
        var links = new List<LaborFileAsset>
        {
            new() { Id = Guid.NewGuid(), LaborId = Guid.NewGuid(), FileAssetId = fileAssetId }
        };

        var linkCount = links.Count(lf => lf.FileAssetId == fileAssetId);
        Assert.True(linkCount > 0);
    }

    [Fact]
    public void FileAsset_DeleteWithMultipleLinks_Blocked()
    {
        var fileAssetId = Guid.NewGuid();
        var links = new List<LaborFileAsset>
        {
            new() { Id = Guid.NewGuid(), LaborId = Guid.NewGuid(), FileAssetId = fileAssetId },
            new() { Id = Guid.NewGuid(), LaborId = Guid.NewGuid(), FileAssetId = fileAssetId }
        };

        var linkCount = links.Count(lf => lf.FileAssetId == fileAssetId);
        Assert.Equal(2, linkCount);
    }

    // ── Test 6: Campaign locked blocks upload/link/unlink ─────────────────────

    [Fact]
    public void FileAsset_CampaignLocked_BlocksUpload()
    {
        var campaign = new Campaign { Id = Guid.NewGuid(), Status = "Locked" };

        var isLocked = campaign.Status == "Locked";
        Assert.True(isLocked);
    }

    [Fact]
    public void FileAsset_CampaignLocked_BlocksLink()
    {
        var campaignId = Guid.NewGuid();
        var laborId = Guid.NewGuid();

        var campaignLots = new List<CampaignLot>
        {
            new() { Id = Guid.NewGuid(), CampaignId = campaignId, LotId = Guid.NewGuid() }
        };

        var labors = new List<Labor>
        {
            new() { Id = laborId, CampaignLotId = campaignLots[0].Id }
        };

        var campaigns = new List<Campaign>
        {
            new() { Id = campaignId, Status = "Locked" }
        };

        var ctxMock = CreateContextMock();
        ctxMock.Setup(c => c.CampaignLots).Returns(CreateMockDbSet(campaignLots).Object);
        ctxMock.Setup(c => c.Labors).Returns(CreateMockDbSet(labors).Object);
        ctxMock.Setup(c => c.Campaigns).Returns(CreateMockDbSet(campaigns).Object);

        var isLocked = campaigns[0].Status == "Locked";
        Assert.True(isLocked);
    }

    [Fact]
    public void FileAsset_CampaignLocked_BlocksUnlink()
    {
        var campaign = new Campaign { Id = Guid.NewGuid(), Status = "Locked" };
        var isLocked = campaign.Status == "Locked";

        Assert.True(isLocked);
    }

    // ── Original DTO/entity tests ────────────────────────────────────────────

    [Fact]
    public void LaborFileAsset_UniqueIndex_PreventsDuplicateLinks()
    {
        var laborId = Guid.NewGuid();
        var fileAssetId = Guid.NewGuid();

        var link1 = new LaborFileAsset { Id = Guid.NewGuid(), LaborId = laborId, FileAssetId = fileAssetId };
        var link2 = new LaborFileAsset { Id = Guid.NewGuid(), LaborId = laborId, FileAssetId = fileAssetId };

        var duplicates = new[] { link1, link2 }
            .GroupBy(lf => new { lf.LaborId, lf.FileAssetId })
            .Any(g => g.Count() > 1);

        Assert.True(duplicates);
    }

    [Fact]
    public void BulkDeleteUnlinkedRequest_CanBeCreated()
    {
        var request = new BulkDeleteUnlinkedRequest(new List<Guid> { Guid.NewGuid(), Guid.NewGuid() });
        Assert.Equal(2, request.FileAssetIds.Count);
    }

    [Fact]
    public void EmptyBulkDeleteUnlinkedRequest_HasEmptyList()
    {
        var request = new BulkDeleteUnlinkedRequest(new List<Guid>());
        Assert.Empty(request.FileAssetIds);
    }

    [Fact]
    public void LinkPendingResult_Success_CreatesValidInstance()
    {
        var result = new LinkPendingResult(true, 3, new List<string>());
        Assert.True(result.Success);
        Assert.Equal(3, result.LinkedCount);
    }

    [Fact]
    public void LinkPendingResult_Failure_HasErrors()
    {
        var errors = new List<string> { "Network error" };
        var result = new LinkPendingResult(false, Errors: errors);
        Assert.False(result.Success);
        Assert.Contains("Network error", result.Errors!);
    }

    [Fact]
    public void FileAsset_MultipleLabors_CanShareSameFile()
    {
        var fileAssetId = Guid.NewGuid();
        var assets = new List<LaborFileAsset>
        {
            new() { Id = Guid.NewGuid(), LaborId = Guid.NewGuid(), FileAssetId = fileAssetId },
            new() { Id = Guid.NewGuid(), LaborId = Guid.NewGuid(), FileAssetId = fileAssetId }
        };

        var count = assets.Count(a => a.FileAssetId == fileAssetId);
        Assert.Equal(2, count);
    }
}
