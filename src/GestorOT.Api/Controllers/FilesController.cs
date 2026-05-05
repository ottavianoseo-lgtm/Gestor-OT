using System.Security.Cryptography;
using System.Security.Claims;
using GestorOT.Application.Interfaces;
using GestorOT.Domain.Entities;
using GestorOT.Shared.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestorOT.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FilesController : ControllerBase
{
    private readonly IApplicationDbContext _context;

    public FilesController(IApplicationDbContext context)
    {
        _context = context;
    }

    private async Task<bool> IsLaborInLockedCampaignAsync(Guid laborId)
    {
        var labor = await _context.Labors
            .AsNoTracking()
            .Include(l => l.CampaignLot)
                .ThenInclude(cl => cl!.Campaign)
            .FirstOrDefaultAsync(l => l.Id == laborId);
        return labor?.CampaignLot?.Campaign?.Status == "Locked";
    }

    private async Task<bool> IsFileLinkedToLockedCampaignAsync(Guid fileAssetId)
    {
        var links = await _context.LaborFileAssets
            .AsNoTracking()
            .Include(lf => lf.Labor)
                .ThenInclude(l => l!.CampaignLot)
                    .ThenInclude(cl => cl!.Campaign)
            .Where(lf => lf.FileAssetId == fileAssetId && lf.Labor!.CampaignLot!.Campaign!.Status == "Locked")
            .AnyAsync();
        return links;
    }

    private void WriteAuditLog(string action, string entityType, string? entityId, string? newValue = null)
    {
        var audit = new AuditLog
        {
            Id = Guid.NewGuid(),
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            NewValue = newValue,
            UserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value,
            UserEmail = User.FindFirst(ClaimTypes.Email)?.Value,
            Timestamp = DateTime.UtcNow
        };
        _context.AuditLogs.Add(audit);
    }

    [HttpPost("upload")]
    [DisableRequestSizeLimit]
    public async Task<ActionResult<FileAssetDto>> Upload(IFormFile file, [FromQuery] Guid? laborId = null)
    {
        if (file == null || file.Length == 0)
            return BadRequest("No se proporcionó ningún archivo.");

        if (laborId.HasValue && laborId.Value != Guid.Empty && await IsLaborInLockedCampaignAsync(laborId.Value))
            return Conflict("No se pueden adjuntar archivos a labores de una campaña bloqueada.");

        if (file.Length > 10 * 1024 * 1024)
            return BadRequest("El archivo supera el límite de 10 MB.");

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        var content = ms.ToArray();

        var hash = Convert.ToHexString(SHA256.HashData(content));

        var existing = await _context.FileAssets
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Hash == hash);

        if (existing != null)
        {
            if (laborId.HasValue && laborId.Value != Guid.Empty)
            {
                var alreadyLinked = await _context.LaborFileAssets
                    .AnyAsync(lf => lf.LaborId == laborId.Value && lf.FileAssetId == existing.Id);
                if (!alreadyLinked)
                {
                    _context.LaborFileAssets.Add(new LaborFileAsset
                    {
                        Id = Guid.NewGuid(),
                        LaborId = laborId.Value,
                        FileAssetId = existing.Id,
                        LinkedAt = DateTime.UtcNow
                    });
                    WriteAuditLog("FileLinked", "LaborFileAsset", $"{laborId.Value}|{existing.Id}");
                    await _context.SaveChangesAsync();
                }
            }

            return Ok(new FileAssetDto(
                existing.Id, existing.FileName, existing.MimeType,
                existing.SizeBytes, existing.UploadedAt, existing.Hash, existing.Tags));
        }

        var asset = new FileAsset
        {
            Id = Guid.NewGuid(),
            FileName = file.FileName,
            MimeType = file.ContentType ?? "application/octet-stream",
            SizeBytes = file.Length,
            Content = content,
            Hash = hash,
            UploadedAt = DateTime.UtcNow
        };

        _context.FileAssets.Add(asset);

        if (laborId.HasValue && laborId.Value != Guid.Empty)
        {
            _context.LaborFileAssets.Add(new LaborFileAsset
            {
                Id = Guid.NewGuid(),
                LaborId = laborId.Value,
                FileAssetId = asset.Id,
                LinkedAt = DateTime.UtcNow
            });
        }

        await _context.SaveChangesAsync();
        WriteAuditLog("FileUpload", "FileAsset", asset.Id.ToString(), file.FileName);

        return Ok(new FileAssetDto(
            asset.Id, asset.FileName, asset.MimeType,
            asset.SizeBytes, asset.UploadedAt, asset.Hash, asset.Tags));
    }

    [HttpGet]
    public async Task<ActionResult<List<FileAssetDto>>> List([FromQuery] string? search = null)
    {
        var query = _context.FileAssets.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.ToLower();
            query = query.Where(f => f.FileName.ToLower().Contains(term)
                || (f.Tags != null && f.Tags.ToLower().Contains(term)));
        }

        var files = await query
            .OrderByDescending(f => f.UploadedAt)
            .Take(50)
            .Select(f => new FileAssetDto(
                f.Id, f.FileName, f.MimeType, f.SizeBytes,
                f.UploadedAt, f.Hash, f.Tags,
                _context.LaborFileAssets.Count(lf => lf.FileAssetId == f.Id)))
            .ToListAsync();

        return Ok(files);
    }

    [HttpGet("{id:guid}/download")]
    public async Task<IActionResult> Download(Guid id)
    {
        var asset = await _context.FileAssets
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == id);
        if (asset == null)
            return NotFound();

        return File(asset.Content, asset.MimeType, asset.FileName);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var linkCount = await _context.LaborFileAssets
            .CountAsync(lf => lf.FileAssetId == id);

        if (linkCount > 0)
            return BadRequest($"El archivo está vinculado a {linkCount} labor(es). Desvincúlelo primero.");

        var asset = await _context.FileAssets
            .FirstOrDefaultAsync(f => f.Id == id);
        if (asset == null)
            return NotFound();

        _context.FileAssets.Remove(asset);
        await _context.SaveChangesAsync();
        WriteAuditLog("FileDelete", "FileAsset", id.ToString());

        return NoContent();
    }

    [HttpGet("labor/{laborId:guid}")]
    public async Task<ActionResult<List<FileAssetDto>>> GetByLabor(Guid laborId)
    {
        return await _context.LaborFileAssets
            .AsNoTracking()
            .Include(lf => lf.FileAsset)
            .Where(lf => lf.LaborId == laborId)
            .OrderByDescending(lf => lf.LinkedAt)
            .Select(lf => new FileAssetDto(
                lf.FileAsset!.Id,
                lf.FileAsset.FileName,
                lf.FileAsset.MimeType,
                lf.FileAsset.SizeBytes,
                lf.FileAsset.UploadedAt,
                lf.FileAsset.Hash,
                lf.FileAsset.Tags))
            .ToListAsync();
    }

    [HttpPost("link")]
    public async Task<IActionResult> LinkFiles([FromBody] LinkFilesRequest request)
    {
        if (request.FileAssetIds == null || request.FileAssetIds.Count == 0)
            return BadRequest("Debe especificar al menos un archivo para vincular.");

        var labor = await _context.Labors
            .FirstOrDefaultAsync(l => l.Id == request.LaborId);
        if (labor == null)
            return NotFound("Labor no encontrada.");

        if (await IsLaborInLockedCampaignAsync(request.LaborId))
            return Conflict("No se pueden vincular archivos a labores de una campaña bloqueada.");

        var linked = 0;
        var errors = new List<string>();

        foreach (var fileId in request.FileAssetIds)
        {
            var fileExists = await _context.FileAssets
                .AnyAsync(f => f.Id == fileId);
            if (!fileExists)
            {
                errors.Add($"El archivo {fileId} no existe o no es accesible.");
                continue;
            }

            var alreadyLinked = await _context.LaborFileAssets
                .AnyAsync(lf => lf.LaborId == request.LaborId && lf.FileAssetId == fileId);

            if (alreadyLinked)
            {
                errors.Add($"El archivo {fileId} ya está vinculado a esta labor.");
                continue;
            }

            _context.LaborFileAssets.Add(new LaborFileAsset
            {
                Id = Guid.NewGuid(),
                LaborId = request.LaborId,
                FileAssetId = fileId,
                LinkedAt = DateTime.UtcNow
            });
            linked++;
        }

        if (linked > 0)
        {
            WriteAuditLog("FileLink", "LaborFileAsset", $"{request.LaborId}|{string.Join(",", request.FileAssetIds)}", $"{linked} linked");
            await _context.SaveChangesAsync();
        }

        return Ok(new { Linked = linked, Errors = errors });
    }

    [HttpDelete("labor/{laborId:guid}/{fileAssetId:guid}")]
    public async Task<IActionResult> Unlink(Guid laborId, Guid fileAssetId)
    {
        var link = await _context.LaborFileAssets
            .FirstOrDefaultAsync(lf => lf.LaborId == laborId && lf.FileAssetId == fileAssetId);

        if (link == null)
            return NotFound();

        if (await IsLaborInLockedCampaignAsync(laborId))
            return Conflict("No se puede desvincular archivos de labores en una campaña bloqueada.");

        _context.LaborFileAssets.Remove(link);
        await _context.SaveChangesAsync();
        WriteAuditLog("FileUnlink", "LaborFileAsset", $"{laborId}|{fileAssetId}");

        return NoContent();
    }

    [HttpPost("delete-unlinked")]
    public async Task<IActionResult> DeleteUnlinked([FromBody] BulkDeleteUnlinkedRequest request)
    {
        if (request.FileAssetIds == null || request.FileAssetIds.Count == 0)
            return BadRequest("No se proporcionaron archivos para eliminar.");

        var deleted = 0;
        var errors = new List<string>();

        foreach (var id in request.FileAssetIds)
        {
            var linkCount = await _context.LaborFileAssets
                .CountAsync(lf => lf.FileAssetId == id);

            if (linkCount > 0)
            {
                errors.Add($"El archivo {id} está vinculado a {linkCount} labor(es) y no se puede eliminar.");
                continue;
            }

            var asset = await _context.FileAssets
                .FirstOrDefaultAsync(f => f.Id == id);

            if (asset == null)
            {
                errors.Add($"Archivo {id} no encontrado.");
                continue;
            }

            _context.FileAssets.Remove(asset);
            deleted++;
        }

        await _context.SaveChangesAsync();
        if (deleted > 0)
            WriteAuditLog("FileDeleteUnlinked", "FileAsset", string.Join(",", request.FileAssetIds), $"{deleted} deleted");
        return Ok(new { Deleted = deleted, Errors = errors });
    }

    [HttpGet("validate-size")]
    public ActionResult<object> ValidateSize()
    {
        return Ok(new { MaxSizeBytes = 10 * 1024 * 1024, MaxSizeMB = 10 });
    }
}
