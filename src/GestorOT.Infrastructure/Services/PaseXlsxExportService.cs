using ClosedXML.Excel;
using GestorOT.Application.Interfaces;
using GestorOT.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GestorOT.Infrastructure.Services;

/// <summary>
/// Arma el archivo de importacion de pases del G4: encabezados en la fila 1, datos desde la 2
/// y la hoja llamada igual que el archivo, que el G4 exige (planilla Importador v5.08).
/// </summary>
public sealed class PaseXlsxExportService : IPaseXlsxExportService
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<PaseXlsxExportService> _logger;

    private sealed record Columna(string Header, Func<PaseImputacion, XLCellValue> Valor, string? Formato = null);

    private static XLCellValue OVacio(long? v) => v.HasValue ? (XLCellValue)v.Value : Blank.Value;

    // Las cuentas de gestion quedan vacias cuando hay perfil: el G4 imputa por el perfil.
    private static XLCellValue CuentaGestion(long? cuenta, long? perfil) =>
        perfil.GetValueOrDefault() > 0 || cuenta.GetValueOrDefault() == 0 ? Blank.Value : (XLCellValue)cuenta!.Value;

    // Las 30 columnas del layout de importacion del G4, en el orden del export de Ganaderia:
    // siempre todas, aunque vengan vacias, para poder imputar por perfil o por cuenta. Los
    // nombres que comparte con la planilla Importador v5.08 van como en ella (mayusculas
    // incluidas, sin tilde), que es el archivo que hoy importa bien.
    private static readonly Columna[] Columnas =
    [
        new("IdReferencia", p => p.IdReferencia),
        new("IdAgrupacionPase", p => p.IdAgrupacionPase),
        new("CodEmpresa", p => p.CodEmpresa),
        new("codComprobante", p => p.CodComprobante),
        new("noImputaGestion", p => p.NoImputaGestion),
        new("noImputaContabilidad", p => p.NoImputaContabilidad),
        new("noImputaCentro", p => p.NoImputaCentro),
        new("noImputaAuxiliar", p => p.NoImputaAuxiliar),
        // El G4 los toma como texto con ceros a la izquierda ("02526", "00000569").
        new("puntoVenta", p => p.PuntoVenta.ToString("D5"), "@"),
        new("numeroComprobante", p => p.NumeroComprobante.HasValue ? (XLCellValue)p.NumeroComprobante.Value.ToString("D8") : Blank.Value, "@"),
        new("fecha", p => p.Fecha.ToString("yyyy-MM-dd"), "@"),
        new("codPersona", p => OVacio(p.CodPersona)),
        new("codMoneda", p => p.CodMoneda),
        new("codListaDePrecios", p => OVacio(p.CodListaDePrecios)),
        new("codConcepto", p => p.CodConcepto),
        new("cantidadAuxiliar", p => p.CantidadAuxiliar.HasValue ? (XLCellValue)p.CantidadAuxiliar.Value : Blank.Value),
        new("cantidad", p => p.Cantidad),
        new("precio", p => p.Precio),
        new("codPerfilImputacionDebe", p => OVacio(p.CodPerfilImputacionDebe)),
        new("codPerfilImputacionHaber", p => OVacio(p.CodPerfilImputacionHaber)),
        new("codCuentaDebeGestion", p => CuentaGestion(p.CodCuentaDebeGestion, p.CodPerfilImputacionDebe), "0"),
        new("codCuentaHaberGestion", p => CuentaGestion(p.CodCuentaHaberGestion, p.CodPerfilImputacionHaber), "0"),
        new("codCuentaDebeCentro", p => OVacio(p.CodCuentaDebeCentro), "0"),
        new("codCuentaHaberCentro", p => OVacio(p.CodCuentaHaberCentro), "0"),
        new("codCuentaDebeContabilidad", p => OVacio(p.CodCuentaDebeContabilidad), "0"),
        new("codCuentaHaberContabilidad", p => OVacio(p.CodCuentaHaberContabilidad), "0"),
        new("codCuentaDebeAuxiliar", p => OVacio(p.CodCuentaDebeAuxiliar), "0"),
        new("codCuentaHaberAuxiliar", p => OVacio(p.CodCuentaHaberAuxiliar), "0"),
        new("Notas", p => p.Notas ?? ""),
    ];

    public PaseXlsxExportService(
        IApplicationDbContext context,
        ILogger<PaseXlsxExportService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<(Stream FileStream, string FileName)> ExportLoteAsync(
        Guid tenantId, Guid loteId, CancellationToken ct = default)
    {
        var lote = await _context.PasesLote
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.TenantId == tenantId && l.Id == loteId, ct)
            ?? throw new InvalidOperationException($"Lote {loteId} no encontrado para tenant {tenantId}.");

        var pases = await _context.PasesImputacion
            .AsNoTracking()
            .Where(p => p.PaseLoteId == loteId)
            .OrderBy(p => p.IdAgrupacionPase)
            .ThenBy(p => p.IdReferencia)
            .ToListAsync(ct);

        // idImportacion es un numero correlativo (1, 2, ...) como en la planilla, no un GUID:
        // es el orden del lote entre los del tenant.
        var idImportacion = await _context.PasesLote
            .AsNoTracking()
            .CountAsync(l => l.TenantId == tenantId && l.GeneradoEn <= lote.GeneradoEn, ct);

        // El G4 exige que la hoja se llame igual que el archivo; una hoja admite hasta 31
        // caracteres, asi que el nombre es corto.
        var nombre = $"PasesG4_{idImportacion:D4}_{lote.GeneradoEn:yyyyMMdd}";
        var fileName = $"{nombre}.xlsx";

        var columnas = new List<Columna> { new("idImportacion", _ => idImportacion) };
        columnas.AddRange(Columnas);

        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet(nombre);

        for (int c = 0; c < columnas.Count; c++)
        {
            ws.Cell(1, c + 1).Value = columnas[c].Header;
        }

        for (int i = 0; i < pases.Count; i++)
        {
            for (int c = 0; c < columnas.Count; c++)
            {
                var cell = ws.Cell(i + 2, c + 1);
                if (columnas[c].Formato is string formato)
                {
                    cell.Style.NumberFormat.Format = formato;
                }
                cell.Value = columnas[c].Valor(pases[i]);
            }
        }

        ws.Columns().AdjustToContents();

        var ms = new MemoryStream();
        wb.SaveAs(ms);
        ms.Position = 0;

        _logger.LogInformation("XLSX Pases G4 generado | LoteId={LoteId} Pases={Count} File={File}",
            loteId, pases.Count, fileName);

        return (ms, fileName);
    }
}
