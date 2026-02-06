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
    string? LotName = null,
    List<LaborSupplyDto>? Supplies = null
)
{
    public LaborDto() : this(Guid.Empty, null, Guid.Empty, string.Empty, "Planned", null, 0, DateTime.MinValue, null, null) { }
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
    string? SupplyUnit = null
)
{
    public LaborSupplyDto() : this(Guid.Empty, Guid.Empty, Guid.Empty, 0, null, 0, null, string.Empty, null, null) { }
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
    List<LaborDto> Labors
)
{
    public WorkOrderDetailDto() : this(Guid.Empty, Guid.Empty, string.Empty, "Pending", string.Empty, DateTime.MinValue, null, null, new()) { }
}
