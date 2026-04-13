namespace GestorOT.Domain.Entities;

public class WorkOrder : TenantEntity
{
    public Guid FieldId { get; set; } // OTs are now linked to a Field (Campo)
    public Guid? CampaignId { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = "Draft";
    public string AssignedTo { get; set; } = string.Empty;
    public DateTime DueDate { get; set; }
    public string OTNumber { get; set; } = string.Empty;
    public Guid? ContractorId { get; set; }
    public Guid? ContactId { get; set; } // Default Responsible for associated labors
    public bool IsExternalBilling { get; set; } // Header level default
    public decimal AgreedRate { get; set; }
    public DateTime PlannedDate { get; set; }
    public DateTime ExpirationDate { get; set; }
    public decimal EstimatedCostUSD { get; set; }
    public bool StockReserved { get; set; }
    
    // Navigation
    public Field? Field { get; set; }
    public Campaign? Campaign { get; set; }
    public Contact? Contact { get; set; }
    public ICollection<Labor> Labors { get; set; } = new List<Labor>();
}
