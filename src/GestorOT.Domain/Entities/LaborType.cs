namespace GestorOT.Domain.Entities;

public class LaborType : TenantEntity, IExternalErpEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ExternalErpId { get; set; }
}
