using GestorOT.Api.Controllers;
using GestorOT.Application.Interfaces;
using GestorOT.Domain.Entities;
using GestorOT.Shared.Dtos;
using GestorOT.Tests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using System.Security.Claims;

namespace GestorOT.Tests.Regression;

public class FileControllerLogicTests
{
    private Mock<IApplicationDbContext> CreateContextMock()
    {
        var mock = new Mock<IApplicationDbContext>();
        mock.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        mock.Setup(c => c.AuditLogs).Returns(CreateMockDbSet(new List<AuditLog>()).Object);
        return mock;
    }

    private Mock<DbSet<T>> CreateMockDbSet<T>(List<T> data) where T : class
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

    private void MockUser(FilesController controller)
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(new Claim[]
        {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Email, "test@example.com")
        }, "mock"));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };
    }

    [Fact]
    public async Task Upload_Fails_WhenCampaignLocked()
    {
        var laborId = Guid.NewGuid();
        var campaign = new Campaign { Id = Guid.NewGuid(), Status = "Locked" };
        var campaignLot = new CampaignLot { Id = Guid.NewGuid(), Campaign = campaign };
        var labor = new Labor { Id = laborId, CampaignLot = campaignLot };

        var context = CreateContextMock();
        context.Setup(c => c.Labors).Returns(CreateMockDbSet(new List<Labor> { labor }).Object);

        var controller = new FilesController(context.Object);
        MockUser(controller);
        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.Length).Returns(100);
        fileMock.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(new byte[100]));

        var result = await controller.Upload(fileMock.Object, laborId);

        var conflictResult = Assert.IsType<ConflictObjectResult>(result.Result);
        Assert.Contains("bloqueada", conflictResult.Value!.ToString());
    }

    [Fact]
    public async Task LinkFiles_Fails_WhenCampaignLocked()
    {
        var laborId = Guid.NewGuid();
        var campaign = new Campaign { Id = Guid.NewGuid(), Status = "Locked" };
        var campaignLot = new CampaignLot { Id = Guid.NewGuid(), Campaign = campaign };
        var labor = new Labor { Id = laborId, CampaignLot = campaignLot };

        var context = CreateContextMock();
        context.Setup(c => c.Labors).Returns(CreateMockDbSet(new List<Labor> { labor }).Object);
        context.Setup(c => c.FileAssets).Returns(CreateMockDbSet(new List<FileAsset>()).Object);

        var controller = new FilesController(context.Object);
        MockUser(controller);
        var request = new LinkFilesRequest(laborId, new List<Guid> { Guid.NewGuid() });

        var result = await controller.LinkFiles(request);

        var conflictResult = Assert.IsType<ConflictObjectResult>(result);
        Assert.Contains("bloqueada", conflictResult.Value!.ToString());
    }

    [Fact]
    public async Task Upload_ReusesExistingFile_WhenHashMatches()
    {
        var content = new byte[] { 1, 2, 3 };
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(content));
        var existingAsset = new FileAsset { Id = Guid.NewGuid(), Hash = hash, FileName = "old.pdf" };

        var context = CreateContextMock();
        context.Setup(c => c.FileAssets).Returns(CreateMockDbSet(new List<FileAsset> { existingAsset }).Object);

        var controller = new FilesController(context.Object);
        MockUser(controller);
        var fileMock = new Mock<IFormFile>();
        var sourceStream = new MemoryStream(content);
        fileMock.Setup(f => f.Length).Returns(content.Length);
        fileMock.Setup(f => f.OpenReadStream()).Returns(sourceStream);
        fileMock.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Callback<Stream, CancellationToken>((target, ct) => sourceStream.CopyToAsync(target, 81920, ct).GetAwaiter().GetResult())
            .Returns(Task.CompletedTask);
        fileMock.Setup(f => f.FileName).Returns("new.pdf");
        fileMock.Setup(f => f.ContentType).Returns("application/pdf");

        var result = await controller.Upload(fileMock.Object);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<FileAssetDto>(okResult.Value);
        Assert.Equal(existingAsset.Id, dto.Id);
    }

    [Fact]
    public async Task DeleteUnlinked_OnlyDeletesUnlinkedFiles()
    {
        var unlinkedId = Guid.NewGuid();
        var linkedId = Guid.NewGuid();
        var assets = new List<FileAsset>
        {
            new() { Id = unlinkedId },
            new() { Id = linkedId }
        };
        var links = new List<LaborFileAsset>
        {
            new() { FileAssetId = linkedId }
        };

        var context = CreateContextMock();
        var assetSet = CreateMockDbSet(assets);
        context.Setup(c => c.FileAssets).Returns(assetSet.Object);
        context.Setup(c => c.LaborFileAssets).Returns(CreateMockDbSet(links).Object);
        context.Setup(c => c.AuditLogs).Returns(CreateMockDbSet(new List<AuditLog>()).Object);

        var controller = new FilesController(context.Object);
        MockUser(controller);
        var request = new BulkDeleteUnlinkedRequest(new List<Guid> { unlinkedId, linkedId });

        var result = await controller.DeleteUnlinked(request);

        var okResult = Assert.IsType<OkObjectResult>(result);
        // Linked one should be skipped, unlinked should be removed
        assetSet.Verify(m => m.Remove(It.Is<FileAsset>(f => f.Id == unlinkedId)), Times.Once);
        assetSet.Verify(m => m.Remove(It.Is<FileAsset>(f => f.Id == linkedId)), Times.Never);
    }
}
