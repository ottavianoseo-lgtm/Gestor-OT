namespace GestorOT.Shared.Dtos;

public record LaborDto(
    Guid Id,
    Guid? WorkOrderId,
    Guid LotId,
    string LaborType,
    string Status,
    DateTime? ExecutionDate,
    decimal Hectares,
    DateTime CreatedAt,
    decimal Rate = 0,
    string RateUnit = "ha",
    string? LotName = null,
    List<LaborSupplyDto>? Supplies = null,
    string? PrescriptionMapUrl = null,
    string? MachineryUsedId = null,
    string? WeatherLogJson = null,
    string? Notes = null,
    string? FieldName = null,
    DateTime? PlannedDate = null
)
{
    public LaborDto() : this(Guid.Empty, null, Guid.Empty, string.Empty, "Planned", null, 0, DateTime.MinValue, 0, "ha", null, null, null, null, null, null, null, null) { }
}

public record LaborSupplyDto(
    Guid Id,
    Guid LaborId,
    Guid SupplyId,
    decimal PlannedDose,
    decimal? RealDose,
    decimal PlannedTotal,
    decimal? RealTotal,
    string DoseUnit,
    string? SupplyName = null,
    string? SupplyUnit = null,
    int TankMixOrder = 0,
    bool IsSubstitute = false
)
{
    public LaborSupplyDto() : this(Guid.Empty, Guid.Empty, Guid.Empty, 0, null, 0, null, string.Empty, null, null, 0, false) { }
}

public record LaborCalendarDto(
    Guid Id,
    string Title,
    DateTime Date,
    string Status,
    string ColorHex,
    bool HasWorkOrder,
    string LaborType,
    decimal Hectares,
    string? LotName = null,
    string? WorkOrderDescription = null
)
{
    public LaborCalendarDto() : this(Guid.Empty, string.Empty, DateTime.MinValue, string.Empty, string.Empty, false, string.Empty, 0, null, null) { }
}

public record WorkOrderDetailDto(
    Guid Id,
    Guid LotId,
    string Description,
    string Status,
    string AssignedTo,
    DateTime DueDate,
    string? LotName,
    string? FieldName,
    List<LaborDto> Labors,
    ServiceSettlementDto? Settlement = null,
    string? OTNumber = null,
    DateTime? PlannedDate = null,
    DateTime? ExpirationDate = null,
    decimal EstimatedCostUSD = 0,
    bool StockReserved = false,
    Guid? CampaignId = null,
    Guid? ContractorId = null
)
{
    public WorkOrderDetailDto() : this(Guid.Empty, Guid.Empty, string.Empty, "Draft", string.Empty, DateTime.MinValue, null, null, new(), null, null, null, null, 0, false, null, null) { }
}
