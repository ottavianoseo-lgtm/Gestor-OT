namespace GestorOT.Shared.Dtos;

public record InventoryDto(
    Guid Id,
    string Category,
    string ItemName,
    double CurrentStock,
    double ReorderLevel,
    string UnitA = "",
    string UnitB = "",
    double ConversionFactor = 1
)
{
    public InventoryDto() : this(Guid.Empty, string.Empty, string.Empty, 0, 0, "", "", 1) { }
}
