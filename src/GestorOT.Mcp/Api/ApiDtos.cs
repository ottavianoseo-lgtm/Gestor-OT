namespace GestorOT.Mcp.Api;

// Copia mínima de los contratos de la API (GestorOT.Shared.Dtos), solo con lo que usan las tools.
// No se referencia GestorOT.Shared a propósito: arrastra Domain y NetTopologySuite, y el MCP tiene
// que poder vivir aparte. Lo que no se declara acá (ej. WktGeometry de los lotes) se descarta al
// deserializar, que es justo lo que queremos para no llenar el contexto del modelo.
// Los enums viajan como número porque la API no registra JsonStringEnumConverter.

public sealed record ApiUser(Guid UserId, string Email, string DisplayName, string Role);

public sealed record ApiCampaignSummary(Guid Id, string Name, int Status, bool IsActive, DateOnly StartDate, DateOnly EndDate);

public sealed record ApiFieldListItem(Guid Id, string Name, long? CodCentro, int LotCount, decimal CadastralArea);

public sealed record ApiLot(Guid Id, Guid FieldId, string Name, string Status, string? FieldName, decimal CadastralArea, long? CodCentro);

public sealed record ApiCampaignLot(
    Guid Id, Guid CampaignId, Guid LotId, Guid? FieldId, string? LotName, string? FieldName,
    decimal CadastralArea, decimal ProductiveArea, Guid? CropId, string? CampaignName, long? CodCentro);

public sealed record ApiLaborSupply(Guid SupplyId, string? SupplyName, decimal PlannedDose, decimal? RealDose, decimal PlannedTotal, decimal? RealTotal, string UnitOfMeasure);

public sealed record ApiLabor
{
    public Guid Id { get; init; }
    public Guid? WorkOrderId { get; init; }
    public string? OTNumber { get; init; }
    public Guid LotId { get; init; }
    public string? LotName { get; init; }
    public string? FieldName { get; init; }
    public Guid? CampaignId { get; init; }
    public Guid LaborTypeId { get; init; }
    public string? LaborTypeName { get; init; }
    public string Status { get; init; } = "";
    public string Mode { get; init; } = "";
    public int Priority { get; init; }
    public DateTime? EstimatedDate { get; init; }
    public DateTime? ExecutionDate { get; init; }
    public decimal Hectares { get; init; }
    public decimal EffectiveArea { get; init; }
    public decimal Rate { get; init; }
    public string RateUnit { get; init; } = "";
    public string? AssignedTo { get; init; }
    public string? Notes { get; init; }
    public bool IsOriginalPlan { get; init; }
    public List<ApiLaborSupply> Supplies { get; init; } = new();
}

public sealed record ApiWorkOrder
{
    public Guid Id { get; init; }
    public string? OTNumber { get; init; }
    public string? Name { get; init; }
    public string? Description { get; init; }
    public string Status { get; init; } = "";
    public string? AssignedTo { get; init; }
    public Guid? FieldId { get; init; }
    public string? FieldName { get; init; }
    public Guid? CampaignId { get; init; }
    public DateTime DueDate { get; init; }
    public DateTime? PlannedDate { get; init; }
    public DateTime? ExpirationDate { get; init; }
    public bool StockReserved { get; init; }
    public bool IsLocked { get; init; }
    // El detalle (GET api/workorders/{id}) también trae Labors; no se declara para que se descarte
    // y las labores se pidan paginadas a api/labors/search.
}

public sealed record ApiPaged<T>(List<T> Items, int Total, int Page, int PageSize);

// Catálogos. ExecutionMode: 0 = Propia, 1 = Contratista. Role: ver GestorOT.Domain.Enums.ContactRole.
public sealed record ApiLaborType(Guid Id, string Name, string? Description, string? ExternalErpId, int? ExecutionMode);

public sealed record ApiActivity(Guid Id, string Name, string? ExternalErpId, bool IsActive);

public sealed record ApiContact(Guid Id, string FullName, string? Email, string? Position, string? LegalName, int Role);

public sealed record ApiInventoryItem(Guid Id, string Category, string ItemName, double CurrentStock, string UnitA, string UnitB, string? GrupoConcepto, string? SubGrupoConcepto);

public sealed record ApiWorkOrderStatus(Guid Id, string Name, bool IsEditable, bool IsDefault, int SortOrder);

public sealed record ApiLaborSaveResponse(ApiLabor Labor, List<string> Warnings);
