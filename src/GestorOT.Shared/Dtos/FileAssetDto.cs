namespace GestorOT.Shared.Dtos;

public record FileAssetDto(
    Guid Id,
    string FileName,
    string MimeType,
    long SizeBytes,
    DateTime UploadedAt,
    string? Hash = null,
    string? Tags = null,
    int LinkCount = 0
)
{
    public FileAssetDto() : this(Guid.Empty, string.Empty, string.Empty, 0, DateTime.MinValue) { }
}

public record LinkFilesRequest(
    Guid LaborId,
    List<Guid> FileAssetIds
)
{
    public LinkFilesRequest() : this(Guid.Empty, new()) { }
}

public record UnlinkFileRequest(
    Guid LaborId,
    Guid FileAssetId
)
{
    public UnlinkFileRequest() : this(Guid.Empty, Guid.Empty) { }
}

public record LinkPendingResult(
    bool Success,
    int LinkedCount = 0,
    List<string>? Errors = null
)
{
    public LinkPendingResult() : this(false) { }
}

public record BulkDeleteUnlinkedRequest(
    List<Guid> FileAssetIds
);
