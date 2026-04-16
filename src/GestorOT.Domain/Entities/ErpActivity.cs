namespace GestorOT.Domain.Entities;

public class ErpActivity : TenantEntity, IExternalErpEntity
{
    public string Name { get; set; } = string.Empty;
    public string? ExternalErpId { get; set; }
}
