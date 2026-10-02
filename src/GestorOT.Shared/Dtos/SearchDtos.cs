namespace GestorOT.Shared.Dtos;

// Filas livianas de los endpoints GET .../search: sin geometría ni colecciones anidadas, para que
// una página sea chica de verdad.

public record FieldListItemDto(Guid Id, string Name, long? CodCentro, int LotCount, decimal CadastralArea);

public record LotListItemDto(Guid Id, Guid FieldId, string Name, string Status, string? FieldName, decimal CadastralArea, long? CodCentro);
