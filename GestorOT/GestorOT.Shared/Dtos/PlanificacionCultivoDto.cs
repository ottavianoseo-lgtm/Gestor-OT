namespace GestorOT.Shared.Dtos;

public record CultivoDto(
    Guid Id,
    string Name,
    string? Variedad,
    string? Ciclo,
    DateTime CreatedAt
)
{
    public CultivoDto() : this(Guid.Empty, string.Empty, null, null, DateTime.MinValue) { }
}

public record PlanificacionCultivoDto(
    Guid Id,
    Guid LoteId,
    Guid CampanaId,
    Guid CultivoId,
    decimal SuperficieSembradaHa,
    decimal SuperficieGeometriaHa,
    string? LoteName = null,
    string? CampanaName = null,
    string? CultivoName = null,
    DateTime? CreatedAt = null
)
{
    public PlanificacionCultivoDto() : this(Guid.Empty, Guid.Empty, Guid.Empty, Guid.Empty, 0, 0) { }
}

public record SuperficieCampoDto(
    Guid CampoId,
    Guid CampanaId,
    string CampoName,
    decimal SuperficieTotalSembradaHa,
    decimal SuperficieTotalGeometriaHa,
    int CantidadLotes,
    List<PlanificacionCultivoDto> Detalle
)
{
    public SuperficieCampoDto() : this(Guid.Empty, Guid.Empty, string.Empty, 0, 0, 0, new()) { }
}

public record RotacionHistorialDto(
    Guid LoteId,
    string LoteName,
    List<RotacionEntryDto> Historial
)
{
    public RotacionHistorialDto() : this(Guid.Empty, string.Empty, new()) { }
}

public record RotacionEntryDto(
    Guid CampanaId,
    string CampanaName,
    DateOnly CampanaInicio,
    Guid CultivoId,
    string CultivoName,
    decimal SuperficieSembradaHa
)
{
    public RotacionEntryDto() : this(Guid.Empty, string.Empty, DateOnly.MinValue, Guid.Empty, string.Empty, 0) { }
}
