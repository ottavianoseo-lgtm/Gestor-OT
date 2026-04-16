namespace GestorOT.Domain.Entities;

public class WorkOrderSupplyApproval : TenantEntity
{
    public Guid WorkOrderId { get; set; }
    public Guid SupplyId { get; set; }
    public decimal TotalCalculated { get; set; }
    public decimal ApprovedWithdrawal { get; set; }
    public string? WithdrawalCenter { get; set; }
    public decimal? RealTotalUsed { get; set; }

    public WorkOrder? WorkOrder { get; set; }
    public Inventory? Supply { get; set; }
}
