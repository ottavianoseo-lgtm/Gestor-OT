namespace GestorOT.Shared.Dtos;

public record ErpConceptDto(
    Guid Id,
    string Description,
    double Stock,
    string? UnitA,
    string? UnitB,
    string? GrupoConcepto,
    string? SubGrupoConcepto,
    string? ExternalErpId,
    DateTime LastSyncDate,
    bool IsActivated = false
)
{
    public ErpConceptDto() : this(Guid.Empty, string.Empty, 0, null, null, null, null, null, DateTime.MinValue) { }
}
