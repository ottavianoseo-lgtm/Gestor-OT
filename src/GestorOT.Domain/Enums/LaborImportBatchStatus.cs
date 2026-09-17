namespace GestorOT.Domain.Enums;

/// <summary>
/// Estado de un lote de importación de labores pendiente: todavía tiene filas
/// por resolver, o ya se resolvió todo (importado o descartado) y queda como historial.
/// </summary>
public enum LaborImportBatchStatus
{
    Pending = 0,
    Completed = 1
}

/// <summary>
/// Resolución de una fila pendiente de importación: sin resolver, importada
/// como labor, o descartada por el usuario.
/// </summary>
public enum LaborImportRowResolution
{
    Unresolved = 0,
    Imported = 1,
    Excluded = 2
}
