namespace GestorOT.Domain.Entities;

public interface ITenantEntity
{
    Guid TenantId { get; set; }
}

public interface IExternalErpEntity
{
    string? ExternalErpId { get; set; }
}

public abstract class BaseEntity
{
    public Guid Id { get; set; }
}

public abstract class TenantEntity : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
}
