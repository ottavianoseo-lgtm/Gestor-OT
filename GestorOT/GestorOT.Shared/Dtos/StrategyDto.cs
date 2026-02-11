namespace GestorOT.Shared.Dtos;

public record CropStrategyDto(
    Guid Id,
    string Name,
    string CropType,
    DateTime CreatedAt,
    List<StrategyItemDto>? Items = null
)
{
    public CropStrategyDto() : this(Guid.Empty, string.Empty, string.Empty, DateTime.MinValue, null) { }
}

public record StrategyItemDto(
    Guid Id,
    Guid CropStrategyId,
    string LaborType,
    int DayOffset,
    List<StrategySupplyDefault>? DefaultSupplies = null
)
{
    public StrategyItemDto() : this(Guid.Empty, Guid.Empty, string.Empty, 0, null) { }
}

public record StrategySupplyDefault(
    Guid SupplyId,
    decimal Dose,
    string DoseUnit,
    string? SupplyName = null
)
{
    public StrategySupplyDefault() : this(Guid.Empty, 0, string.Empty, null) { }
}

public record ApplyStrategyRequest(
    Guid StrategyId,
    List<Guid> LotIds,
    DateTime StartDate,
    string AssignedTo
)
{
    public ApplyStrategyRequest() : this(Guid.Empty, new(), DateTime.MinValue, string.Empty) { }
}

public record ApplyStrategyResult(
    int WorkOrdersCreated,
    int LaborsCreated,
    List<Guid> WorkOrderIds
)
{
    public ApplyStrategyResult() : this(0, 0, new()) { }
}

public record ServiceSettlementDto(
    Guid Id,
    Guid WorkOrderId,
    decimal TotalHectares,
    decimal TotalAmount,
    DateTime GeneratedAt,
    string ErpSyncStatus,
    List<LaborSettlementLineDto>? Lines = null,
    string? WorkOrderDescription = null,
    string? LotName = null,
    string? AssignedTo = null
)
{
    public ServiceSettlementDto() : this(Guid.Empty, Guid.Empty, 0, 0, DateTime.MinValue, "Pending", null, null, null, null) { }
}

public record LaborSettlementLineDto(
    string LaborType,
    decimal Quantity,
    string RateUnit,
    decimal Rate,
    decimal Subtotal
)
{
    public LaborSettlementLineDto() : this(string.Empty, 0, "ha", 0, 0) { }
}

public record DiscrepancyReportDto(
    Guid WorkOrderId,
    string Description,
    List<LaborDiscrepancyDto> Labors
)
{
    public DiscrepancyReportDto() : this(Guid.Empty, string.Empty, new()) { }
}

public record LaborDiscrepancyDto(
    Guid LaborId,
    string LaborType,
    decimal Hectares,
    List<SupplyDiscrepancyDto> Supplies
)
{
    public LaborDiscrepancyDto() : this(Guid.Empty, string.Empty, 0, new()) { }
}

public record SupplyDiscrepancyDto(
    string SupplyName,
    decimal PlannedDose,
    decimal RealDose,
    decimal DiscrepancyPercent
)
{
    public SupplyDiscrepancyDto() : this(string.Empty, 0, 0, 0) { }
}
