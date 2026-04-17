using GestorOT.Application.Interfaces;
using GestorOT.Domain.Entities;
using GestorOT.Shared.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestorOT.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LaborAttachmentsController : ControllerBase
{
    private readonly IApplicationDbContext _context;

    public LaborAttachmentsController(IApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet("labor/{laborId:guid}")]
    public async Task<ActionResult<List<LaborAttachmentDto>>> GetAttachmentsByLabor(Guid laborId)
    {
        return await _context.LaborAttachments
            .AsNoTracking()
            .Where(a => a.LaborId == laborId)
            .OrderByDescending(a => a.UploadedAt)
            .Select(a => new LaborAttachmentDto(
                a.Id, a.LaborId, a.FileName, a.MimeType, a.FileSizeBytes, a.UploadedAt))
            .ToListAsync();
    }

    [HttpPost("labor/{laborId:guid}/upload")]
    [DisableRequestSizeLimit]
    public async Task<ActionResult<LaborAttachmentDto>> Upload(Guid laborId, IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest("No se proporcionó ningún archivo.");

        var labor = await _context.Labors.FindAsync(laborId);
        if (labor == null)
            return NotFound("Labor no encontrada.");

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);

        var attachment = new LaborAttachment
        {
            Id = Guid.NewGuid(),
            LaborId = laborId,
            FileName = file.FileName,
            MimeType = file.ContentType,
            FileSizeBytes = file.Length,
            Content = ms.ToArray(),
            UploadedAt = DateTime.UtcNow
        };

        _context.LaborAttachments.Add(attachment);
        await _context.SaveChangesAsync();

        return Ok(new LaborAttachmentDto(
            attachment.Id, attachment.LaborId, attachment.FileName, attachment.MimeType, attachment.FileSizeBytes, attachment.UploadedAt));
    }

    [HttpGet("{id:guid}/download")]
    public async Task<IActionResult> Download(Guid id)
    {
        var attachment = await _context.LaborAttachments.FindAsync(id);
        if (attachment == null)
            return NotFound();

        return File(attachment.Content, attachment.MimeType, attachment.FileName);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var attachment = await _context.LaborAttachments.FindAsync(id);
        if (attachment == null)
            return NotFound();

        _context.LaborAttachments.Remove(attachment);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}
