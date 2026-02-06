using System.Text.Json.Serialization;
using GestorOT.Shared.Dtos;

namespace GestorOT.Shared;

[JsonSerializable(typeof(FieldDto))]
[JsonSerializable(typeof(List<FieldDto>))]
[JsonSerializable(typeof(LotDto))]
[JsonSerializable(typeof(List<LotDto>))]
[JsonSerializable(typeof(LotSummaryDto))]
[JsonSerializable(typeof(List<LotSummaryDto>))]
[JsonSerializable(typeof(WorkOrderDto))]
[JsonSerializable(typeof(List<WorkOrderDto>))]
[JsonSerializable(typeof(RecentWorkOrderDto))]
[JsonSerializable(typeof(List<RecentWorkOrderDto>))]
[JsonSerializable(typeof(GeoJsonFeature))]
[JsonSerializable(typeof(List<GeoJsonFeature>))]
[JsonSerializable(typeof(GeoJsonGeometry))]
[JsonSerializable(typeof(GeoJsonFeatureCollection))]
[JsonSerializable(typeof(DashboardStatsDto))]
[JsonSerializable(typeof(Dictionary<string, object>))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = false,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
public partial class AppJsonSerializerContext : JsonSerializerContext
{
}
