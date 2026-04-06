namespace GestorOT.Domain.Entities;

public class WorkOrder : TenantEntity
{
    public Guid LotId { get; set; }
    public Guid? CampaignId { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = "Draft";
    public string AssignedTo { get; set; } = string.Empty;
    public DateTime DueDate { get; set; }
    public string OTNumber { get; set; } = string.Empty;
    public Guid? ContractorId { get; set; }
    public Guid? EmployeeId { get; set; }
    public decimal AgreedRate { get; set; }
    public DateTime PlannedDate { get; set; }
    public DateTime ExpirationDate { get; set; }
    public decimal EstimatedCostUSD { get; set; }
    public bool StockReserved { get; set; }
    public Lot? Lot { get; set; }
    public Campaign? Campaign { get; set; }
    public Employee? Employee { get; set; }
    public ICollection<Labor> Labors { get; set; } = new List<Labor>();
}
