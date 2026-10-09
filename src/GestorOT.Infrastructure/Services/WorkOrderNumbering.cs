using GestorOT.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace GestorOT.Infrastructure.Services;

/// <summary>
/// Numeracion de OTs: ascendente por tenant, a partir de la ultima. El numero de OT es el
/// comprobante del pase al G4 (numeroComprobante), asi que una OT sin numero no se puede imputar.
/// </summary>
public static class WorkOrderNumbering
{
    /// <summary>
    /// El siguiente numero libre: el mayor numero de OT del tenant + 1. Toma los digitos del
    /// numero ("OT-569" cuenta como 569) porque las OTs importadas traen el numero de la planilla
    /// tal cual. El filtro de tenant lo pone el query filter global del contexto.
    /// </summary>
    public static async Task<int> NextAsync(IApplicationDbContext context, CancellationToken ct = default)
    {
        var numeros = await context.WorkOrders
            .AsNoTracking()
            .Where(w => w.OTNumber != "")
            .Select(w => w.OTNumber)
            .ToListAsync(ct);

        var max = numeros
            .Select(Parse)
            .Where(n => n.HasValue)
            .Select(n => n!.Value)
            .DefaultIfEmpty(0)
            .Max();

        return max + 1;
    }

    private static int? Parse(string otNumber)
    {
        var digitos = new string(otNumber.Where(char.IsAsciiDigit).ToArray());
        return int.TryParse(digitos, out var n) ? n : null;
    }
}
