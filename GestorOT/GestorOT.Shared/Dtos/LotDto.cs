namespace GestorOT.Shared.Dtos;

public record LotDto(
    Guid Id,
    Guid FieldId,
    string Name,
    string Status,
    string? GeoJson = null,
    string? FieldName = null
)
{
    public LotDto() : this(Guid.Empty, Guid.Empty, string.Empty, "Active", null, null) { }
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
    public WorkOrderDto() : this(Guid.Empty, Guid.Empty, string.Empty, "Pending", string.Empty, DateTime.MinValue, null) { }
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
    int PendingWorkOrders,
    int CompletedWorkOrders
)
{
    public DashboardStatsDto() : this(0, 0, 0, 0) { }
}
