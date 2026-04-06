namespace GestorOT.Shared.Dtos;

public record LotDto(
    Guid Id,
    Guid FieldId,
    string Name,
    string Status,
    string? WktGeometry = null,
    string? FieldName = null,
    double Area = 0
)
{
    public LotDto() : this(Guid.Empty, Guid.Empty, string.Empty, "Active", null, null, 0) { }
}

public record WorkOrderDto
{
    public Guid Id { get; set; }
    public Guid LotId { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = "Draft";
    public string AssignedTo { get; set; } = string.Empty;
    public DateTime DueDate { get; set; }
    public string? LotName { get; set; }
    public string? OTNumber { get; set; }
    public DateTime? PlannedDate { get; set; }
    public DateTime? ExpirationDate { get; set; }
    public decimal EstimatedCostUSD { get; set; }
    public decimal AgreedRate { get; set; }
    public bool StockReserved { get; set; }
    public Guid? ContractorId { get; set; }
    public Guid? EmployeeId { get; set; }
    public Guid? CampaignId { get; set; }

    public WorkOrderDto() { }
    public WorkOrderDto(Guid id, Guid lotId, string description, string status, string assignedTo, DateTime dueDate, string? lotName, string? otNumber, DateTime? plannedDate, DateTime? expirationDate, decimal estimatedCost, decimal agreedRate, bool stockReserved, Guid? contractorId, Guid? employeeId, Guid? campaignId)
    {
        Id = id; LotId = lotId; Description = description; Status = status; AssignedTo = assignedTo; DueDate = dueDate; LotName = lotName; OTNumber = otNumber; PlannedDate = plannedDate; ExpirationDate = expirationDate; EstimatedCostUSD = estimatedCost; AgreedRate = agreedRate; StockReserved = stockReserved; ContractorId = contractorId; EmployeeId = employeeId; CampaignId = campaignId;
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
    double TotalArea
)
{
    public DashboardStatsDto() : this(0, 0, 0, 0, 0, 0, 0) { }
}

public record RecentWorkOrderDto(
    Guid Id,
    string Description,
    string Status,
    string AssignedTo,
    DateTime DueDate,
    string? LotName = null,
    string? FieldName = null
)
{
    public RecentWorkOrderDto() : this(Guid.Empty, string.Empty, "Pending", string.Empty, DateTime.MinValue, null, null) { }
}

public class LotAreaResult
{
    public Guid Id { get; set; }
    public double AreaHa { get; set; }
}
