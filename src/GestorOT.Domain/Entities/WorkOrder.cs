namespace GestorOT.Domain.Entities;

public class WorkOrder : TenantEntity
{
    public Guid Id { get; set; }
    public string? Name { get; set; } // #24: Human-readable identifier
    public Guid? FieldId { get; set; } // OTs can now span multiple fields via Labors
    public Guid? CampaignId { get; set; }
    public string Description { get; set; } = string.Empty;
    public Guid? WorkOrderStatusId { get; set; }
    public string Status { get; set; } = "Draft";
    public string AssignedTo { get; set; } = string.Empty;
    public DateTime DueDate { get; set; }
    public string OTNumber { get; set; } = string.Empty;
    public Guid? ContractorId { get; set; }
    public Guid? ContactId { get; set; } // Default Responsible for associated labors
    public bool IsExternalBilling { get; set; } // Header level default
    public DateTime PlannedDate { get; set; }
    public DateTime ExpirationDate { get; set; }
    public bool StockReserved { get; set; }
    public bool AcceptsMultiplePeople { get; set; }
    public bool AcceptsMultipleDates { get; set; }
    
    // Navigation
    public Field? Field { get; set; }
    public Campaign? Campaign { get; set; }
    public Contact? Contact { get; set; }
    public WorkOrderStatus? WorkOrderStatus { get; set; }
    public ICollection<Labor> Labors { get; set; } = new List<Labor>();
    public ICollection<WorkOrderSupplyApproval> SupplyApprovals { get; set; } = new List<WorkOrderSupplyApproval>();
}
