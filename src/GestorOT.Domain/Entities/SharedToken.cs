namespace GestorOT.Domain.Entities;

public class SharedToken
{
    public Guid Id { get; set; }
    public Guid WorkOrderId { get; set; }
    public Guid TenantId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public bool IsRevoked { get; set; }
    public bool IsUsed { get; set; }
    public DateTime CreatedAt { get; set; }
    public WorkOrder? WorkOrder { get; set; }
}
