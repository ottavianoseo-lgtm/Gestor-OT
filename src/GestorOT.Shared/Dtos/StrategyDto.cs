namespace GestorOT.Shared.Dtos;

public record CropStrategyDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CropType { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public List<StrategyItemDto> Items { get; set; } = new();

    public CropStrategyDto() { }
    public CropStrategyDto(Guid id, string name, string cropType, DateTime createdAt, List<StrategyItemDto> items)
    {
        Id = id; Name = name; CropType = cropType; CreatedAt = createdAt; Items = items;
    }
}

public record StrategyItemDto
{
    public Guid Id { get; set; }
    public Guid CropStrategyId { get; set; }
    public Guid LaborTypeId { get; set; }
    public string? LaborTypeName { get; set; }
    public int DayOffset { get; set; }
    public List<StrategySupplyDefault> DefaultSupplies { get; set; } = new();

    public StrategyItemDto() { }
    public StrategyItemDto(Guid id, Guid cropStrategyId, Guid laborTypeId, string? laborTypeName, int dayOffset, List<StrategySupplyDefault> defaultSupplies)
    {
        Id = id; CropStrategyId = cropStrategyId; LaborTypeId = laborTypeId; LaborTypeName = laborTypeName; DayOffset = dayOffset; DefaultSupplies = defaultSupplies;
    }
}

public record StrategySupplyDefault
{
    public Guid SupplyId { get; set; }
    public decimal Dose { get; set; }
    public string DoseUnit { get; set; } = string.Empty;
    public string? SupplyName { get; set; }

    public StrategySupplyDefault() { }
    public StrategySupplyDefault(Guid supplyId, decimal dose, string doseUnit, string? supplyName = null)
    {
        SupplyId = supplyId; Dose = dose; DoseUnit = doseUnit; SupplyName = supplyName;
    }
}

public record ApplyStrategyRequest
{
    public Guid StrategyId { get; set; }
    public List<Guid> LotIds { get; set; } = new();
    public DateTime StartDate { get; set; }
    public string AssignedTo { get; set; } = string.Empty;

    public ApplyStrategyRequest() { }
    public ApplyStrategyRequest(Guid strategyId, List<Guid> lotIds, DateTime startDate, string assignedTo)
    {
        StrategyId = strategyId; LotIds = lotIds; StartDate = startDate; AssignedTo = assignedTo;
    }
}

public record ApplyStrategyResult
{
    public int WorkOrdersCreated { get; set; }
    public int LaborsCreated { get; set; }
    public List<Guid> WorkOrderIds { get; set; } = new();

    public ApplyStrategyResult() { }
    public ApplyStrategyResult(int workOrdersCreated, int laborsCreated, List<Guid> workOrderIds)
    {
        WorkOrdersCreated = workOrdersCreated; LaborsCreated = laborsCreated; WorkOrderIds = workOrderIds;
    }
}

public record DiscrepancyReportDto
{
    public Guid WorkOrderId { get; set; }
    public string Description { get; set; } = string.Empty;
    public List<LaborDiscrepancyDto> Labors { get; set; } = new();

    public DiscrepancyReportDto() { }
    public DiscrepancyReportDto(Guid workOrderId, string description, List<LaborDiscrepancyDto> labors)
    {
        WorkOrderId = workOrderId; Description = description; Labors = labors;
    }
}

public record LaborDiscrepancyDto
{
    public Guid LaborId { get; set; }
    public string LaborTypeName { get; set; } = string.Empty;
    public decimal Hectares { get; set; }
    public List<SupplyDiscrepancyDto> Supplies { get; set; } = new();

    public LaborDiscrepancyDto() { }
    public LaborDiscrepancyDto(Guid laborId, string laborTypeName, decimal hectares, List<SupplyDiscrepancyDto> supplies)
    {
        LaborId = laborId; LaborTypeName = laborTypeName; Hectares = hectares; Supplies = supplies;
    }
}

public record SupplyDiscrepancyDto
{
    public string SupplyName { get; set; } = string.Empty;
    public decimal PlannedDose { get; set; }
    public decimal RealDose { get; set; }
    public decimal DiscrepancyPercent { get; set; }

    public SupplyDiscrepancyDto() { }
    public SupplyDiscrepancyDto(string supplyName, decimal plannedDose, decimal realDose, decimal discrepancyPercent)
    {
        SupplyName = supplyName; PlannedDose = plannedDose; RealDose = realDose; DiscrepancyPercent = discrepancyPercent;
    }
}
