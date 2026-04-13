namespace GestorOT.Shared.Dtos;

public record LaborDto
{
    public Guid Id { get; set; }
    public Guid? WorkOrderId { get; set; }
    public Guid LotId { get; set; }
    public Guid? CampaignLotId { get; set; }
    public Guid LaborTypeId { get; set; }
    public string Status { get; set; } = "Planned";
    public DateTime? ExecutionDate { get; set; }
    public DateTime? EstimatedDate { get; set; }
    public decimal Hectares { get; set; }
    public DateTime CreatedAt { get; set; }
    public decimal Rate { get; set; }
    public string RateUnit { get; set; } = "ha";
    public string? LotName { get; set; }
    public string? LaborTypeName { get; set; }
    public List<LaborSupplyDto> Supplies { get; set; } = new();
    public string? PrescriptionMapUrl { get; set; }
    public string? MachineryUsedId { get; set; }
    public string? WeatherLogJson { get; set; }
    public string? Notes { get; set; }
    public string? FieldName { get; set; }
    public Guid? ContactId { get; set; }
    public decimal PlannedDose { get; set; }
    public decimal? RealizedDose { get; set; }

    public LaborDto() { }
    public LaborDto(Guid id, Guid? workOrderId, Guid lotId, Guid? campaignLotId, Guid laborTypeId, string status, DateTime? executionDate, DateTime? estimatedDate, decimal hectares, DateTime createdAt, decimal rate, string rateUnit, string? lotName, string? laborTypeName, List<LaborSupplyDto> supplies, string? prescriptionMapUrl, string? machineryUsedId, string? weatherLogJson, string? notes, string? fieldName, decimal plannedDose, decimal? realizedDose, Guid? contactId)
    {
        Id = id; WorkOrderId = workOrderId; LotId = lotId; CampaignLotId = campaignLotId; LaborTypeId = laborTypeId; Status = status; ExecutionDate = executionDate; EstimatedDate = estimatedDate; Hectares = hectares; CreatedAt = createdAt; Rate = rate; RateUnit = rateUnit; LotName = lotName; LaborTypeName = laborTypeName; Supplies = supplies ?? new(); PrescriptionMapUrl = prescriptionMapUrl; MachineryUsedId = machineryUsedId; WeatherLogJson = weatherLogJson; Notes = notes; FieldName = fieldName; PlannedDose = plannedDose; RealizedDose = realizedDose; ContactId = contactId;
    }
}

public record LaborSupplyDto
{
    public Guid Id { get; set; }
    public Guid LaborId { get; set; }
    public Guid SupplyId { get; set; }
    public decimal PlannedDose { get; set; }
    public decimal? RealDose { get; set; }
    public decimal PlannedTotal { get; set; }
    public decimal? RealTotal { get; set; }
    public string UnitOfMeasure { get; set; } = string.Empty;
    public string? SupplyName { get; set; }
    public string? SupplyUnit { get; set; }
    public int TankMixOrder { get; set; }
    public bool IsSubstitute { get; set; }

    public LaborSupplyDto() { }
    public LaborSupplyDto(Guid id, Guid laborId, Guid supplyId, decimal plannedDose, decimal? realDose, decimal plannedTotal, decimal? realTotal, string unitOfMeasure, string? supplyName, string? supplyUnit, int tankMixOrder, bool isSubstitute)
    {
        Id = id; LaborId = laborId; SupplyId = supplyId; PlannedDose = plannedDose; RealDose = realDose; PlannedTotal = plannedTotal; RealTotal = realTotal; UnitOfMeasure = unitOfMeasure; SupplyName = supplyName; SupplyUnit = supplyUnit; TankMixOrder = tankMixOrder; IsSubstitute = isSubstitute;
    }
}

public record LaborCalendarDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string Status { get; set; } = string.Empty;
    public string ColorHex { get; set; } = string.Empty;
    public bool HasWorkOrder { get; set; }
    public string LaborType { get; set; } = string.Empty;
    public decimal Hectares { get; set; }
    public string? LotName { get; set; }
    public string? WorkOrderDescription { get; set; }
    
    public LaborCalendarDto() { }
    public LaborCalendarDto(Guid id, string title, DateTime date, string status, string colorHex, bool hasWorkOrder, string laborType, decimal hectares, string? lotName, string? woDesc)
    {
        Id = id; Title = title; Date = date; Status = status; ColorHex = colorHex; HasWorkOrder = hasWorkOrder; LaborType = laborType; Hectares = hectares; LotName = lotName; WorkOrderDescription = woDesc;
    }
}

public record WorkOrderDetailDto
{
    public Guid Id { get; set; }
    public Guid FieldId { get; set; } // Header level Field
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = "Draft";
    public string AssignedTo { get; set; } = string.Empty;
    public DateTime DueDate { get; set; }
    public string? FieldName { get; set; }
    public List<LaborDto> Labors { get; set; } = new();
    public string? OTNumber { get; set; }
    public DateTime? PlannedDate { get; set; }
    public DateTime? ExpirationDate { get; set; }
    public decimal EstimatedCostUSD { get; set; }
    public decimal AgreedRate { get; set; }
    public bool StockReserved { get; set; }
    public Guid? CampaignId { get; set; }
    public Guid? ContractorId { get; set; }
    public Guid? ContactId { get; set; }

    public WorkOrderDetailDto() { }
    public WorkOrderDetailDto(Guid id, Guid fieldId, string description, string status, string assignedTo, DateTime dueDate, string? fieldName, List<LaborDto> labors, string? otNumber, DateTime? plannedDate, DateTime? expirationDate, decimal estimatedCost, decimal agreedRate, bool stockReserved, Guid? campaignId, Guid? contractorId, Guid? contactId)
    {
        Id = id; FieldId = fieldId; Description = description; Status = status; AssignedTo = assignedTo; DueDate = dueDate; FieldName = fieldName; Labors = labors; OTNumber = otNumber; PlannedDate = plannedDate; ExpirationDate = expirationDate; EstimatedCostUSD = estimatedCost; AgreedRate = agreedRate; StockReserved = stockReserved; CampaignId = campaignId; ContractorId = contractorId; ContactId = contactId;
    }
}
