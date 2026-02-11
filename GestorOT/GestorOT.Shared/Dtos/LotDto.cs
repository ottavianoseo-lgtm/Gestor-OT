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

public record WorkOrderDto(
    Guid Id,
    Guid LotId,
    string Description,
    string Status,
    string AssignedTo,
    DateTime DueDate,
    string? LotName = null
)
{
    public WorkOrderDto() : this(Guid.Empty, Guid.Empty, string.Empty, "Draft", string.Empty, DateTime.MinValue, null) { }
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
