namespace GestorOT.Shared.Dtos;

public record LotDto(
    Guid Id,
    Guid FieldId,
    string Name,
    string Status,
    string? WktGeometry = null,
    string? FieldName = null,
    double Area = 0,
    decimal CadastralArea = 0
)
{
    public LotDto() : this(Guid.Empty, Guid.Empty, string.Empty, "Active", null, null, 0, 0) { }
}

public record WorkOrderDto
{
    public Guid Id { get; set; }
    public Guid FieldId { get; set; } // OTs now target a Field
    [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "La descripción es obligatoria")]
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = "Draft";
    public string AssignedTo { get; set; } = string.Empty;
    public DateTime DueDate { get; set; }
    public string? FieldName { get; set; }
    public string? OTNumber { get; set; }
    public DateTime? PlannedDate { get; set; }
    public DateTime? ExpirationDate { get; set; }
    public bool StockReserved { get; set; }
    public Guid? ContractorId { get; set; }
    public Guid? ContactId { get; set; }
    public Guid? CampaignId { get; set; }
    public bool AcceptsMultiplePeople { get; set; }
    public bool AcceptsMultipleDates { get; set; }

    public WorkOrderDto() { }
    public WorkOrderDto(Guid id, Guid fieldId, string description, string status, string assignedTo, DateTime dueDate, string? fieldName, string? otNumber, DateTime? plannedDate, DateTime? expirationDate, bool stockReserved, Guid? contractorId, Guid? contactId, Guid? campaignId, bool acceptsMultiplePeople = false, bool acceptsMultipleDates = false)
    {
        Id = id; FieldId = fieldId; Description = description; Status = status; AssignedTo = assignedTo; DueDate = dueDate; FieldName = fieldName; OTNumber = otNumber; PlannedDate = plannedDate; ExpirationDate = expirationDate; StockReserved = stockReserved; ContractorId = contractorId; ContactId = contactId; CampaignId = campaignId; AcceptsMultiplePeople = acceptsMultiplePeople; AcceptsMultipleDates = acceptsMultipleDates;
    }
}

public record GeoJsonFeature(
    string Type,
    Dictionary<string, object>? Properties,
    GeoJsonGeometry? Geometry
)
{
    public GeoJsonFeature() : this("Feature", null, null) { }
}

public record GeoJsonGeometry(
    string Type,
    double[][][]? Coordinates
)
{
    public GeoJsonGeometry() : this("Polygon", null) { }
}

public record GeoJsonFeatureCollection(
    string Type,
    List<GeoJsonFeature> Features
)
{
    public GeoJsonFeatureCollection() : this("FeatureCollection", new List<GeoJsonFeature>()) { }
}

public record DashboardStatsDto(
    int FieldsCount,
    int LotsCount,
    int ActiveLotsCount,
    int PendingWorkOrders,
    int InProgressWorkOrders,
    int CompletedWorkOrders,
    decimal TotalProductiveArea
)
{
    public DashboardStatsDto() : this(0, 0, 0, 0, 0, 0, 0m) { }
}

public record RecentWorkOrderDto(
    Guid Id,
    string Description,
    string Status,
    string AssignedTo,
    DateTime DueDate,
    string? FieldName = null // Changed from LotName
)
{
    public RecentWorkOrderDto() : this(Guid.Empty, string.Empty, "Pending", string.Empty, DateTime.MinValue, null) { }
}

public record SurfaceHistoryDto(
    string CampaignName,
    DateOnly StartDate,
    decimal ProductiveArea,
    decimal CadastralArea,
    decimal Variation
)
{
    public SurfaceHistoryDto() : this(string.Empty, DateOnly.MinValue, 0, 0, 0) { }
}

public class LotAreaResult
{
    public Guid Id { get; set; }
    public double AreaHa { get; set; }
}
