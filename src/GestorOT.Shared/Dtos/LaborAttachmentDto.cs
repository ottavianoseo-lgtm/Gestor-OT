namespace GestorOT.Shared.Dtos;

public record LaborAttachmentDto(
    Guid Id,
    Guid LaborId,
    string FileName,
    string MimeType,
    long FileSizeBytes,
    DateTime UploadedAt
)
{
    public LaborAttachmentDto() : this(Guid.Empty, Guid.Empty, string.Empty, string.Empty, 0, DateTime.MinValue) { }
}
