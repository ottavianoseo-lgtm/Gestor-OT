namespace GestorOT.Shared.Dtos;

public record LaborResumenDto(
    Guid Id,
    string LaborType,
    string Status,
    DateTime? Fecha,
    string? LoteName
)
{
    public LaborResumenDto() : this(Guid.Empty, string.Empty, string.Empty, null, null) { }
}

public record LoteResumenDto(
    Guid LoteId,
    string Nombre,
    string? CampoNombre,
    decimal SuperficieActualHa,
    string? CultivoActual,
    int DiasDesdeSiembra,
    LaborResumenDto? UltimaLabor,
    LaborResumenDto? ProximaLabor,
    string? ResponsableActual,
    string? Estado
)
{
    public LoteResumenDto() : this(Guid.Empty, string.Empty, null, 0, null, 0, null, null, null, null) { }
}

public record LaborDetalleDto(
    Guid LaborId,
    string TipoLabor,
    string Estado,
    DateTime? FechaPlanificada,
    string LoteNombre,
    Guid LoteId,
    string? NombreResponsable,
    string? Maquinaria,
    List<string> Insumos,
    decimal Hectareas,
    string? NotasLabor
)
{
    public LaborDetalleDto() : this(Guid.Empty, string.Empty, string.Empty, null, string.Empty, Guid.Empty, null, null, new(), 0, null) { }
}
