namespace GestorOT.Shared.Dtos;

public record FieldDto(
    Guid Id,
    string Name,
    double HectareasTotales,
    DateTime CreatedAt,
    List<LotSummaryDto> Lots
)
{
    public FieldDto() : this(Guid.Empty, string.Empty, 0, DateTime.MinValue, new List<LotSummaryDto>()) { }
}

public record LotSummaryDto(
    Guid Id,
    string Name,
    string Status
)
{
    public LotSummaryDto() : this(Guid.Empty, string.Empty, "Active") { }
}
