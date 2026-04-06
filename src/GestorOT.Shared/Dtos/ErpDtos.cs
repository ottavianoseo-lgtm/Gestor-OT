using GestorOT.Domain.Enums;

namespace GestorOT.Shared.Dtos;

public record EmployeeDto(
    Guid Id,
    string FullName,
    string? ExternalErpId,
    string? Email,
    string? Position,
    EmployeeRole Role = EmployeeRole.Admin
)
{
    public EmployeeDto() : this(Guid.Empty, string.Empty, null, null, null, EmployeeRole.Admin) { }
}

public record LaborTypeDto(
    Guid Id,
    string Name,
    string? Description,
    string? ExternalErpId
)
{
    public LaborTypeDto() : this(Guid.Empty, string.Empty, null, null) { }
}
