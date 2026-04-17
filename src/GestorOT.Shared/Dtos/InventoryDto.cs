namespace GestorOT.Shared.Dtos;

public record InventoryDto(
    Guid Id,
    string Category,
    string ItemName,
    double CurrentStock,
    double ReorderLevel,
    string UnitA = "",
    string UnitB = "",
    double ConversionFactor = 1,
    string? GrupoConcepto = null,
    string? SubGrupoConcepto = null
)
{
    public InventoryDto() : this(Guid.Empty, string.Empty, string.Empty, 0, 0, "", "", 1, null, null) { }
}
