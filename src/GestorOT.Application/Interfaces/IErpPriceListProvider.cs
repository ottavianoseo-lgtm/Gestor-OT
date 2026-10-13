namespace GestorOT.Application.Interfaces;

/// <summary>Un precio de una lista de precios del G4: vale desde <see cref="VigenciaDesde"/>.</summary>
public sealed record ErpPrecio(long CodConcepto, DateTime? VigenciaDesde, decimal Precio);

/// <summary>
/// Precios de las listas del G4 (ListListasDePreciosPrecios). Separado de IErpSyncService para
/// que el armado de pases dependa solo de esto y se pueda probar sin el cliente HTTP.
/// </summary>
public interface IErpPriceListProvider
{
    /// <summary>Todos los precios de la lista, con todas sus vigencias. Lista vacia si el G4 no responde.</summary>
    Task<IReadOnlyList<ErpPrecio>> GetPreciosAsync(Guid tenantId, long codListaDePrecios, CancellationToken ct = default);
}
