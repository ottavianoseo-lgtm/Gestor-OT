using GestorOT.Domain.Enums;

namespace GestorOT.Domain.Entities;

public class LaborType : TenantEntity, IExternalErpEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ExternalErpId { get; set; }

    /// <summary>Propia o de contratista. Null = sin clasificar (ver <see cref="LaborExecutionMode"/>).</summary>
    public LaborExecutionMode? ExecutionMode { get; set; }
}
