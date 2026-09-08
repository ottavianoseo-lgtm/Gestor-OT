namespace GestorOT.Shared.Dtos;

public record LotSupplyTraceDto(
    Guid SupplyId,
    string SupplyName,
    decimal PlannedDose,
    decimal? RealDose,
    decimal PlannedTotal,
    decimal? RealTotal,
    string Unit
);

public record LotLaborTraceDto(
    Guid LaborId,
    Guid? WorkOrderId,
    string? OtNumber,
    DateTime? Date,
    string LaborTypeName,
    string Status,
    string Mode,
    decimal Hectares,
    string? ContractorOrResponsible,
    string? CropOrActivity,
    List<LotSupplyTraceDto> Supplies
);

public record SupplyTotalSummaryDto(
    Guid SupplyId,
    string SupplyName,
    decimal TotalQuantity,
    decimal AverageDose,
    string Unit,
    int ApplicationsCount
);

public record LaborTypeTotalSummaryDto(
    Guid LaborTypeId,
    string LaborTypeName,
    int TotalCount,
    decimal AccumulatedHectares
);

public record LotTraceabilityReportDto(
    Guid LotId,
    string LotName,
    Guid FieldId,
    string FieldName,
    decimal CadastralArea,
    decimal ProductiveArea,
    string? CropName,
    List<LaborTypeTotalSummaryDto> LaborTotals,
    List<SupplyTotalSummaryDto> SupplyTotals,
    List<LotLaborTraceDto> Timeline
);

public record FieldTraceabilityReportDto(
    Guid FieldId,
    string FieldName,
    int LotsCount,
    decimal TotalArea,
    List<LaborTypeTotalSummaryDto> LaborTotals,
    List<SupplyTotalSummaryDto> SupplyTotals,
    List<LotTraceabilityReportDto> LotsBreakdown
);

public record CampaignTraceabilityReportDto(
    Guid CampaignId,
    string CampaignName,
    List<FieldTraceabilityReportDto> Fields
);
