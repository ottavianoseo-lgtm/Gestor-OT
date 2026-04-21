namespace GestorOT.Shared.Dtos;

public record LaborDto
{
    public Guid Id { get; set; }
    public Guid? WorkOrderId { get; set; }
    public Guid LotId { get; set; }
    public Guid? CampaignLotId { get; set; }
    public Guid LaborTypeId { get; set; }
    public Guid? ErpActivityId { get; set; }
    public string Status { get; set; } = "Planned";
    public string Mode { get; set; } = "Planned"; // Added Mode
    public Guid? PlannedLaborId { get; set; }
    public DateTime? ExecutionDate { get; set; }
    public DateTime? EstimatedDate { get; set; }
    public decimal Hectares { get; set; }
    public DateTime CreatedAt { get; set; }
    public decimal Rate { get; set; }
    public string RateUnit { get; set; } = "ha";
    public string? LotName { get; set; }
    public string? LaborTypeName { get; set; }
    public string? ErpActivityName { get; set; }
    public List<LaborSupplyDto> Supplies { get; set; } = new();
    public string? PrescriptionMapUrl { get; set; }
    public string? MachineryUsedId { get; set; }
    public string? WeatherLogJson { get; set; }
    public string? Notes { get; set; }
    public string? FieldName { get; set; }
    public Guid? ContactId { get; set; }
    public decimal PlannedDose { get; set; }
    public decimal? RealizedDose { get; set; }
    public bool IsExternalBilling { get; set; }

    public LaborDto() { }
    public LaborDto(Guid id, Guid? workOrderId, Guid lotId, Guid? campaignLotId, Guid laborTypeId, Guid? erpActivityId, string status, string mode, DateTime? executionDate, DateTime? estimatedDate, decimal hectares, DateTime createdAt, decimal rate, string rateUnit, string? lotName, string? laborTypeName, string? erpActivityName, List<LaborSupplyDto> supplies, string? prescriptionMapUrl, string? machineryUsedId, string? weatherLogJson, string? notes, string? fieldName, decimal plannedDose, decimal? realizedDose, Guid? contactId, bool isExternalBilling = false, Guid? plannedLaborId = null)
    {
        Id = id; WorkOrderId = workOrderId; LotId = lotId; CampaignLotId = campaignLotId; LaborTypeId = laborTypeId; ErpActivityId = erpActivityId; Status = status; Mode = mode; ExecutionDate = executionDate; EstimatedDate = estimatedDate; Hectares = hectares; CreatedAt = createdAt; Rate = rate; RateUnit = rateUnit; LotName = lotName; LaborTypeName = laborTypeName; ErpActivityName = erpActivityName; Supplies = supplies ?? new(); PrescriptionMapUrl = prescriptionMapUrl; MachineryUsedId = machineryUsedId; WeatherLogJson = weatherLogJson; Notes = notes; FieldName = fieldName; PlannedDose = plannedDose; RealizedDose = realizedDose; ContactId = contactId; IsExternalBilling = isExternalBilling; PlannedLaborId = plannedLaborId;
    }
}

public record LaborSaveResponse(
    LaborDto Labor,
    List<string> Warnings
);

public record LaborSupplyDto
{
    public Guid Id { get; set; }
    public Guid LaborId { get; set; }
    public Guid SupplyId { get; set; }
    public decimal PlannedHectares { get; set; }
    public decimal? RealHectares { get; set; }
    public decimal PlannedDose { get; set; }
    public decimal? RealDose { get; set; }
    public decimal PlannedTotal { get; set; }
    public decimal? RealTotal { get; set; }
    public decimal? CalculatedDose { get; set; }
    public decimal? CalculatedTotal { get; set; }
    public string UnitOfMeasure { get; set; } = string.Empty;
    public string? SupplyName { get; set; }
    public string? SupplyUnit { get; set; }
    public int TankMixOrder { get; set; }
    public bool IsSubstitute { get; set; }

    public LaborSupplyDto() { }
    public LaborSupplyDto(Guid id, Guid laborId, Guid supplyId, decimal plannedHectares, decimal? realHectares, decimal plannedDose, decimal? realDose, decimal plannedTotal, decimal? realTotal, decimal? calculatedDose, decimal? calculatedTotal, string unitOfMeasure, string? supplyName, string? supplyUnit, int tankMixOrder, bool isSubstitute)
    {
        Id = id; LaborId = laborId; SupplyId = supplyId; PlannedHectares = plannedHectares; RealHectares = realHectares; PlannedDose = plannedDose; RealDose = realDose; PlannedTotal = plannedTotal; RealTotal = realTotal; CalculatedDose = calculatedDose; CalculatedTotal = calculatedTotal; UnitOfMeasure = unitOfMeasure; SupplyName = supplyName; SupplyUnit = supplyUnit; TankMixOrder = tankMixOrder; IsSubstitute = isSubstitute;
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

public class WorkOrderSupplyApprovalDto
{
    public Guid Id { get; set; }
    public Guid WorkOrderId { get; set; }
    public Guid SupplyId { get; set; }
    public string? SupplyName { get; set; }
    public decimal TotalCalculated { get; set; }
    public decimal ApprovedWithdrawal { get; set; }
    public string? WithdrawalCenter { get; set; }
    public decimal? RealTotalUsed { get; set; }

    public WorkOrderSupplyApprovalDto() { }
    public WorkOrderSupplyApprovalDto(Guid id, Guid workOrderId, Guid supplyId, string? supplyName, decimal totalCalculated, decimal approvedWithdrawal, string? withdrawalCenter, decimal? realTotalUsed)
    {
        Id = id; WorkOrderId = workOrderId; SupplyId = supplyId; SupplyName = supplyName; TotalCalculated = totalCalculated; ApprovedWithdrawal = approvedWithdrawal; WithdrawalCenter = withdrawalCenter; RealTotalUsed = realTotalUsed;
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
    public bool StockReserved { get; set; }
    public Guid? CampaignId { get; set; }
    public Guid? ContractorId { get; set; }
    public Guid? ContactId { get; set; }
    public bool AcceptsMultiplePeople { get; set; }
    public bool AcceptsMultipleDates { get; set; }
    public List<WorkOrderSupplyApprovalDto> SupplyApprovals { get; set; } = new();

    public WorkOrderDetailDto() { }
    public WorkOrderDetailDto(Guid id, Guid fieldId, string description, string status, string assignedTo, DateTime dueDate, string? fieldName, List<LaborDto> labors, string? otNumber, DateTime? plannedDate, DateTime? expirationDate, bool stockReserved, Guid? campaignId, Guid? contractorId, Guid? contactId, List<WorkOrderSupplyApprovalDto> approvals, bool acceptsMultiplePeople = false, bool acceptsMultipleDates = false)
    {
        Id = id; FieldId = fieldId; Description = description; Status = status; AssignedTo = assignedTo; DueDate = dueDate; FieldName = fieldName; Labors = labors; OTNumber = otNumber; PlannedDate = plannedDate; ExpirationDate = expirationDate; StockReserved = stockReserved; CampaignId = campaignId; ContractorId = contractorId; ContactId = contactId; SupplyApprovals = approvals ?? new(); AcceptsMultiplePeople = acceptsMultiplePeople; AcceptsMultipleDates = acceptsMultipleDates;
    }
}
