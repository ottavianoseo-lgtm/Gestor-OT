namespace GestorOT.Domain.Entities;

public class LaborAttachment : TenantEntity
{
    public Guid LaborId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public byte[] Content { get; set; } = Array.Empty<byte>();
    public string MimeType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    public Labor? Labor { get; set; }
}
