namespace GestorOT.Shared.Dtos;

public class LotDto
{
    public Guid Id { get; set; }
    public Guid FieldId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = "Active";
    public string? GeoJson { get; set; }
    public string? FieldName { get; set; }
}

public class FieldDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public double TotalArea { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<LotDto> Lots { get; set; } = new();
}

public class WorkOrderDto
{
    public Guid Id { get; set; }
    public Guid LotId { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public string AssignedTo { get; set; } = string.Empty;
    public DateTime DueDate { get; set; }
    public string? LotName { get; set; }
}

public class GeoJsonFeature
{
    public string Type { get; set; } = "Feature";
    public Dictionary<string, object>? Properties { get; set; }
    public GeoJsonGeometry? Geometry { get; set; }
}

public class GeoJsonGeometry
{
    public string Type { get; set; } = "Polygon";
    public double[][][]? Coordinates { get; set; }
}

public class GeoJsonFeatureCollection
{
    public string Type { get; set; } = "FeatureCollection";
    public List<GeoJsonFeature> Features { get; set; } = new();
}

public class DashboardStatsDto
{
    public int FieldsCount { get; set; }
    public int LotsCount { get; set; }
    public int PendingWorkOrders { get; set; }
    public int CompletedWorkOrders { get; set; }
}
