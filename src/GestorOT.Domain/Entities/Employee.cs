using GestorOT.Domain.Enums;

namespace GestorOT.Domain.Entities;

public class Employee : TenantEntity, IExternalErpEntity
{
    public string FullName { get; set; } = string.Empty;
    public string? ExternalErpId { get; set; }
    public string? Email { get; set; }
    public string? Position { get; set; }
    public EmployeeRole Role { get; set; } = EmployeeRole.Admin;
}
