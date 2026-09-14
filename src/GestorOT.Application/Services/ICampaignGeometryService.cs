using GestorOT.Domain.Entities;

namespace GestorOT.Application.Services;

/// <summary>
/// Guarda el polígono de un lote en la campaña activa y deriva de él la superficie real.
///
/// Existe para que el flujo masivo y el de a uno usen exactamente la misma regla. Cuando cada
/// uno decidía por su cuenta, dibujar un lote a mano no registraba nada en la campaña y solo
/// la importación masiva lo hacía.
/// </summary>
public interface ICampaignGeometryService
{
    /// <summary>
    /// Persiste la geometría del año y recalcula <c>ProductiveArea</c>. No hace SaveChanges:
    /// el llamador decide, porque la vinculación masiva corre todo dentro de una transacción.
    /// </summary>
    /// <returns>
    /// Avisos para el operador. No son errores: la geometría manda y se guarda igual, pero hay
    /// cosas que tiene que mirar —labores que quedaron excedidas, o un desvío grande contra la
    /// superficie catastral que puede significar un polígono mal trazado.
    /// </returns>
    Task<List<string>> ApplyAsync(Guid campaignId, Lot lot, CancellationToken ct = default);
}
