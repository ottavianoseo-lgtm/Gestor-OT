namespace GestorOT.Domain.Entities;

public class FileAsset : TenantEntity
{
    public string FileName { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public byte[] Content { get; set; } = Array.Empty<byte>();
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public string? Hash { get; set; }
    public string? Tags { get; set; }
    public string? Visibility { get; set; }
    public string? UploadedBy { get; set; }
}
