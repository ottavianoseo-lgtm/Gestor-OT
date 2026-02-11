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
[JsonSerializable(typeof(InventoryDto))]
[JsonSerializable(typeof(List<InventoryDto>))]
[JsonSerializable(typeof(LaborDto))]
[JsonSerializable(typeof(List<LaborDto>))]
[JsonSerializable(typeof(LaborSupplyDto))]
[JsonSerializable(typeof(List<LaborSupplyDto>))]
[JsonSerializable(typeof(WorkOrderDetailDto))]
[JsonSerializable(typeof(List<WorkOrderDetailDto>))]
[JsonSerializable(typeof(CropStrategyDto))]
[JsonSerializable(typeof(List<CropStrategyDto>))]
[JsonSerializable(typeof(StrategyItemDto))]
[JsonSerializable(typeof(List<StrategyItemDto>))]
[JsonSerializable(typeof(StrategySupplyDefault))]
[JsonSerializable(typeof(List<StrategySupplyDefault>))]
[JsonSerializable(typeof(ApplyStrategyRequest))]
[JsonSerializable(typeof(ApplyStrategyResult))]
[JsonSerializable(typeof(ServiceSettlementDto))]
[JsonSerializable(typeof(List<ServiceSettlementDto>))]
[JsonSerializable(typeof(LaborSettlementLineDto))]
[JsonSerializable(typeof(List<LaborSettlementLineDto>))]
[JsonSerializable(typeof(DiscrepancyReportDto))]
[JsonSerializable(typeof(LaborDiscrepancyDto))]
[JsonSerializable(typeof(SupplyDiscrepancyDto))]
[JsonSerializable(typeof(Dictionary<string, object>))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = false,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
public partial class AppJsonSerializerContext : JsonSerializerContext
{
}
