namespace GestorOT.Application.Interfaces;

public interface ICurrentTenantService
{
    Guid TenantId { get; }
}
