namespace GestorOT.Domain.Entities;

public class Campaign : TenantEntity
{
    public string Name { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public bool IsActive { get; set; } = true;
    public string Status { get; set; } = "Planning";
    public decimal BudgetTotalUSD { get; set; }
    public string? BusinessRulesJson { get; set; }
    public DateTime CreatedAt { get; set; }
    public ICollection<CampaignField> CampaignFields { get; set; } = new List<CampaignField>();
    public ICollection<CampaignLot> CampaignLots { get; set; } = new List<CampaignLot>();
    public ICollection<WorkOrder> WorkOrders { get; set; } = new List<WorkOrder>();
}
