using ClosedXML.Excel;
using GestorOT.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GestorOT.Infrastructure.Services;

public sealed class PaseXlsxExportService : IPaseXlsxExportService
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<PaseXlsxExportService> _logger;

    private static readonly string[] Headers =
    [
        "idImportacion", "idReferencia", "idAgrupacionPase", "codEmpresa",
        "codComprobante", "noImputaGestión", "noImputaContabilidad", "noImputaCentro",
        "noImputaAuxiliar", "puntoVenta", "numeroComprobante", "fecha",
        "codPersona", "codMoneda", "codListaDePrecios", "codConcepto",
        "cantidadAuxiliar", "cantidad", "precio",
        "codPerfilImputacionDebe", "codPerfilImputacionHaber",
        "codCuentaDebeGestion", "codCuentaHaberGestion",
        "codCuentaDebeCentro", "codCuentaHaberCentro",
        "codCuentaDebeContabilidad", "codCuentaHaberContabilidad",
        "codCuentaDebeAuxiliar", "codCuentaHaberAuxiliar",
        "Notas"
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

        var fileName = $"Importacion Pases G4 - {loteId}.xlsx";

        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Importación Pases G4");

        ws.Cell("A1").Value = fileName;
        ws.Cell("A1").Style.Font.Bold = true;

        for (int col = 0; col < Headers.Length; col++)
        {
            var cell = ws.Cell(3, col + 1);
            cell.Value = Headers[col];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#D9D9D9");
            cell.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
        }

        for (int i = 0; i < pases.Count; i++)
        {
            var p = pases[i];
            var row = i + 4;
            var idImportacion = lote.Id.ToString();

            ws.Cell(row, 1).Value = idImportacion;
            ws.Cell(row, 2).Value = p.IdReferencia;
            ws.Cell(row, 3).Value = p.IdAgrupacionPase;
            ws.Cell(row, 4).Value = p.CodEmpresa;
            ws.Cell(row, 5).Value = p.CodComprobante;
            ws.Cell(row, 6).Value = p.NoImputaGestion ? "true" : "false";
            ws.Cell(row, 7).Value = p.NoImputaContabilidad ? "true" : "false";
            ws.Cell(row, 8).Value = p.NoImputaCentro ? "true" : "false";
            ws.Cell(row, 9).Value = p.NoImputaAuxiliar ? "true" : "false";
            ws.Cell(row, 10).Value = p.PuntoVenta;
            ws.Cell(row, 11).Value = p.NumeroComprobante.HasValue ? p.NumeroComprobante.Value : "";
            ws.Cell(row, 12).Value = p.Fecha;
            ws.Cell(row, 12).Style.NumberFormat.Format = "dd/MM/yyyy";
            ws.Cell(row, 13).Value = p.CodPersona.HasValue ? p.CodPersona.Value : "";
            ws.Cell(row, 14).Value = p.CodMoneda;
            ws.Cell(row, 15).Value = p.CodListaDePrecios.HasValue ? p.CodListaDePrecios.Value : "";
            ws.Cell(row, 16).Value = p.CodConcepto;
            ws.Cell(row, 17).Value = p.CantidadAuxiliar.HasValue ? p.CantidadAuxiliar.Value : "";
            ws.Cell(row, 18).Value = p.Cantidad;
            ws.Cell(row, 19).Value = p.Precio;
            ws.Cell(row, 20).Value = p.CodPerfilImputacionDebe.HasValue ? p.CodPerfilImputacionDebe.Value : "";
            ws.Cell(row, 21).Value = p.CodPerfilImputacionHaber.HasValue ? p.CodPerfilImputacionHaber.Value : "";

            if (p.CodPerfilImputacionDebe.GetValueOrDefault() > 0 || !p.CodCuentaDebeGestion.HasValue || p.CodCuentaDebeGestion.Value == 0)
            {
                ws.Cell(row, 22).Value = "";
            }
            else
            {
                ws.Cell(row, 22).Value = p.CodCuentaDebeGestion.Value;
            }

            if (p.CodPerfilImputacionHaber.GetValueOrDefault() > 0 || !p.CodCuentaHaberGestion.HasValue || p.CodCuentaHaberGestion.Value == 0)
            {
                ws.Cell(row, 23).Value = "";
            }
            else
            {
                ws.Cell(row, 23).Value = p.CodCuentaHaberGestion.Value;
            }

            ws.Cell(row, 24).Value = p.CodCuentaDebeCentro.HasValue ? p.CodCuentaDebeCentro.Value : "";
            ws.Cell(row, 25).Value = p.CodCuentaHaberCentro.HasValue ? p.CodCuentaHaberCentro.Value : "";
            ws.Cell(row, 26).Value = p.CodCuentaDebeContabilidad.HasValue ? p.CodCuentaDebeContabilidad.Value : "";
            ws.Cell(row, 27).Value = p.CodCuentaHaberContabilidad.HasValue ? p.CodCuentaHaberContabilidad.Value : "";
            ws.Cell(row, 28).Value = p.CodCuentaDebeAuxiliar.HasValue ? p.CodCuentaDebeAuxiliar.Value : "";
            ws.Cell(row, 29).Value = p.CodCuentaHaberAuxiliar.HasValue ? p.CodCuentaHaberAuxiliar.Value : "";
            ws.Cell(row, 30).Value = p.Notas ?? "";

            for (int col = 22; col <= 29; col++)
                ws.Cell(row, col).Style.NumberFormat.Format = "0";
        }

        ws.Columns().AdjustToContents();
        ws.SheetView.FreezeRows(3);

        var ms = new MemoryStream();
        wb.SaveAs(ms);
        ms.Position = 0;

        _logger.LogInformation("XLSX Pases G4 generado | LoteId={LoteId} Pases={Count} File={File}",
            loteId, pases.Count, fileName);

        return (ms, fileName);
    }
}
