namespace GestorOT.Shared.Dtos;

public record TenantDto()
{
    public TenantDto(Guid id, string name, string? gestorMaxApiKeyEncrypted, string? gestorMaxDatabaseId, DateTime createdAt)
        : this()
    {
        Id = id; Name = name; GestorMaxApiKeyEncrypted = gestorMaxApiKeyEncrypted; GestorMaxDatabaseId = gestorMaxDatabaseId; CreatedAt = createdAt;
    }

    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? GestorMaxApiKeyEncrypted { get; init; }
    public string? GestorMaxDatabaseId { get; init; }
    public DateTime CreatedAt { get; init; }
}

public record CreateTenantRequest(string Name, string? GestorMaxApiKey, string? GestorMaxDatabaseId)
{
    public CreateTenantRequest() : this(string.Empty, null, null) { }
}

public record UpdateTenantRequest(string Name, string? GestorMaxApiKey, string? GestorMaxDatabaseId)
{
    public UpdateTenantRequest() : this(string.Empty, null, null) { }
}

public record ShareLinkDto(
    string Url,
    DateTime ExpiresAt
)
{
    public ShareLinkDto() : this(string.Empty, DateTime.MinValue) { }
}

public record PublicWorkOrderDto(
    Guid Id,
    string Description,
    string Status,
    string AssignedTo,
    DateTime DueDate,
    string? LotName,
    string? FieldName,
    List<PublicLaborDto>? Labors = null
)
{
    public PublicWorkOrderDto() : this(Guid.Empty, string.Empty, string.Empty, string.Empty, DateTime.MinValue, null, null, null) { }
}

public record PublicLaborDto(
    Guid Id,
    string LaborType,
    string Status,
    decimal Hectares,
    Guid LotId,
    string? LotName = null,
    List<PublicLaborSupplyDto>? Supplies = null
)
{
    public PublicLaborDto() : this(Guid.Empty, string.Empty, string.Empty, 0, Guid.Empty, null, null) { }
}

public record PublicLaborSupplyDto(
    Guid Id,
    Guid SupplyId,
    string SupplyName,
    decimal PlannedDose,
    decimal? RealDose,
    decimal PlannedTotal,
    decimal? RealTotal,
    string DoseUnit,
    string? SupplyUnit = null
)
{
    public PublicLaborSupplyDto() : this(Guid.Empty, Guid.Empty, string.Empty, 0, null, 0, null, string.Empty, null) { }
}
