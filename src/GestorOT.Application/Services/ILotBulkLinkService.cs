using GestorOT.Shared.Dtos;

namespace GestorOT.Application.Services;

public interface ILotBulkLinkService
{
    /// <summary>
    /// Propone qué hacer con cada polígono importado, cruzando su nombre contra los lotes del
    /// campo destino. No persiste nada.
    /// </summary>
    Task<LotMatchResultDto> ProposeAsync(LotMatchRequestDto request, CancellationToken ct = default);

    /// <summary>
    /// Aplica las vinculaciones en bloque, todo o nada. Si un ítem falla, ninguno queda aplicado
    /// y el resultado dice cuál falló y por qué.
    /// </summary>
    Task<LotBulkLinkResultDto> ApplyAsync(LotBulkLinkRequestDto request, CancellationToken ct = default);
}
