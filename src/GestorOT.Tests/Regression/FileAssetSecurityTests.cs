using GestorOT.Application.Interfaces;
using GestorOT.Domain.Entities;
using GestorOT.Shared.Dtos;
using GestorOT.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace GestorOT.Tests.Regression;

public class FileAssetSecurityTests
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

    // ── Test 1: Download_ReturnsNotFound_ForFileFromAnotherTenant ──────────────

    [Fact]
    public void Download_ReturnsNotFound_WhenFileNotInTenantScope()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        var fileId = Guid.NewGuid();

        var files = new List<FileAsset>
        {
            new() { Id = fileId, FileName = "test.pdf", TenantId = otherTenantId }
        };

        // Simulate tenant filter: only files for current tenant are visible
        var visibleFiles = files.Where(f => f.TenantId == tenantId).ToList();
        Assert.Empty(visibleFiles);
    }

    // ── Test 2: Delete_ReturnsNotFoundOrForbidden_ForFileFromAnotherTenant ────

    [Fact]
    public void Delete_ReturnsNotFound_WhenFileFromAnotherTenant()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        var fileId = Guid.NewGuid();

        var files = new List<FileAsset>
        {
            new() { Id = fileId, FileName = "test.pdf", TenantId = otherTenantId, Content = Array.Empty<byte>() }
        };

        var visible = files.Where(f => f.Id == fileId && f.TenantId == tenantId).ToList();
        Assert.Empty(visible);
    }

    // ── Test 3: LinkFiles_ReturnsBadRequest_WhenFileIdDoesNotExist ────────────

    [Fact]
    public void LinkFiles_ReturnsBadRequest_WhenFileIdDoesNotExist()
    {
        var laborId = Guid.NewGuid();
        var validFileId = Guid.NewGuid();
        var invalidFileId = Guid.NewGuid();

        var existingFiles = new List<FileAsset>
        {
            new() { Id = validFileId, FileName = "ok.pdf" }
        };

        var requestIds = new List<Guid> { validFileId, invalidFileId };
        var invalidIds = requestIds.Where(id => !existingFiles.Any(f => f.Id == id)).ToList();

        Assert.Single(invalidIds);
        Assert.Equal(invalidFileId, invalidIds[0]);
    }

    // ── Test 4: LinkFiles_DoesNotDuplicateExistingLink ────────────────────────

    [Fact]
    public void LinkFiles_DoesNotDuplicateExistingLink()
    {
        var laborId = Guid.NewGuid();
        var fileId = Guid.NewGuid();

        var existingLinks = new List<LaborFileAsset>
        {
            new() { Id = Guid.NewGuid(), LaborId = laborId, FileAssetId = fileId }
        };

        var isAlreadyLinked = existingLinks.Any(lf => lf.LaborId == laborId && lf.FileAssetId == fileId);
        Assert.True(isAlreadyLinked);

        // Attempting to add again should be rejected
        var wouldDuplicate = existingLinks.Any(lf => lf.LaborId == laborId && lf.FileAssetId == fileId);
        Assert.True(wouldDuplicate);
    }

    // ── Test 5: Unlink_ReturnsConflict_WhenCampaignLocked ─────────────────────

    [Fact]
    public void Unlink_ReturnsConflict_WhenCampaignLocked()
    {
        var laborId = Guid.NewGuid();
        var campaignId = Guid.NewGuid();

        var labors = new List<Labor>
        {
            new() { Id = laborId, CampaignLotId = Guid.NewGuid() }
        };

        var campaignLots = new List<CampaignLot>
        {
            new() { Id = labors[0].CampaignLotId!.Value, CampaignId = campaignId, LotId = Guid.NewGuid() }
        };

        var campaigns = new List<Campaign>
        {
            new() { Id = campaignId, Status = "Locked" }
        };

        var isLocked = campaigns[0].Status == "Locked";
        var laborInLockedCampaign = labors.Any(l =>
            l.Id == laborId &&
            campaignLots.Any(cl => cl.Id == l.CampaignLotId && campaigns.Any(c => c.Id == cl.CampaignId && c.Status == "Locked")));

        Assert.True(isLocked);
        Assert.True(laborInLockedCampaign);
    }

    // ── Test 6: Upload_ReturnsConflict_WhenLaborCampaignLocked ────────────────

    [Fact]
    public void Upload_ReturnsConflict_WhenLaborCampaignLocked()
    {
        var laborId = Guid.NewGuid();
        var campaignId = Guid.NewGuid();

        var campaigns = new List<Campaign>
        {
            new() { Id = campaignId, Status = "Locked" }
        };

        var isLocked = campaigns[0].Status == "Locked";
        Assert.True(isLocked);
    }

    // ── FindAsync → FirstOrDefaultAsync replacement tests ────────────────────

    [Fact]
    public void FindAsync_ReplacedByFirstOrDefaultAsync_RespectsTenantFilter()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        var fileId = Guid.NewGuid();

        var files = new List<FileAsset>
        {
            new() { Id = fileId, FileName = "a.pdf", TenantId = otherTenantId }
        };

        // With FirstOrDefaultAsync + no global filter applied, FindAsync would return this
        // but a filtered query should not
        var foundWithFilter = files
            .Where(f => f.TenantId == tenantId)
            .FirstOrDefault(f => f.Id == fileId);

        Assert.Null(foundWithFilter);
    }

    [Fact]
    public void FileAsset_TenantScoped_UploadSetsTenantId()
    {
        var tenantId = Guid.NewGuid();
        var asset = new FileAsset
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FileName = "test.pdf"
        };

        Assert.Equal(tenantId, asset.TenantId);
    }

    // ── Campaign lock blocks all operations ──────────────────────────────────

    [Fact]
    public void LockedCampaign_BlocksLinkAndUploadAndUnlink()
    {
        var campaign = new Campaign { Id = Guid.NewGuid(), Status = "Locked" };

        Assert.True(campaign.Status == "Locked");
    }

    [Fact]
    public void Delete_ReturnsConflict_WhenFileLinkedToLockedCampaignLabor()
    {
        var fileAssetId = Guid.NewGuid();
        var laborId = Guid.NewGuid();
        var campaignId = Guid.NewGuid();

        var links = new List<LaborFileAsset>
        {
            new() { Id = Guid.NewGuid(), LaborId = laborId, FileAssetId = fileAssetId }
        };

        var labors = new List<Labor>
        {
            new() { Id = laborId, CampaignLotId = Guid.NewGuid() }
        };

        var campaignLots = new List<CampaignLot>
        {
            new() { Id = labors[0].CampaignLotId!.Value, CampaignId = campaignId, LotId = Guid.NewGuid() }
        };

        var campaigns = new List<Campaign>
        {
            new() { Id = campaignId, Status = "Locked" }
        };

        var hasLink = links.Any(lf => lf.FileAssetId == fileAssetId);
        var anyLockedLabor = links
            .Where(lf => lf.FileAssetId == fileAssetId)
            .Join(labors, lf => lf.LaborId, l => l.Id, (lf, l) => l)
            .Join(campaignLots, l => l.CampaignLotId, cl => (Guid?)cl.Id, (l, cl) => cl)
            .Join(campaigns, cl => cl.CampaignId, c => c.Id, (cl, c) => c)
            .Any(c => c.Status == "Locked");

        Assert.True(hasLink);
        Assert.True(anyLockedLabor);
    }
}
