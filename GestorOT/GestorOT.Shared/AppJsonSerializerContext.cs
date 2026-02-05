using System.Text.Json.Serialization;
using GestorOT.Shared.Dtos;

namespace GestorOT.Shared;

[JsonSerializable(typeof(LotDto))]
[JsonSerializable(typeof(List<LotDto>))]
[JsonSerializable(typeof(FieldDto))]
[JsonSerializable(typeof(List<FieldDto>))]
[JsonSerializable(typeof(WorkOrderDto))]
[JsonSerializable(typeof(List<WorkOrderDto>))]
[JsonSerializable(typeof(GeoJsonFeature))]
[JsonSerializable(typeof(GeoJsonGeometry))]
[JsonSerializable(typeof(GeoJsonFeatureCollection))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = false,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
public partial class AppJsonSerializerContext : JsonSerializerContext
{
}
