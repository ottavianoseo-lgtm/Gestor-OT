namespace GestorOT.Shared.Dtos;

public record FieldDto(
    Guid Id,
    string Name,
    DateTime CreatedAt,
    List<LotSummaryDto> Lots
)
{
    public FieldDto() : this(Guid.Empty, string.Empty, DateTime.MinValue, new List<LotSummaryDto>()) { }
}

public record LotSummaryDto(
    Guid Id,
    string Name,
    string Status
)
{
    public LotSummaryDto() : this(Guid.Empty, string.Empty, "Active") { }
}
