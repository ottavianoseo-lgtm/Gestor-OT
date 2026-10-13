using ClosedXML.Excel;
using GestorOT.Domain.Entities;
using GestorOT.Infrastructure.Data;
using GestorOT.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using System.Security.Claims;
using Xunit;

namespace GestorOT.Tests.Regression;

/// <summary>
/// El archivo de pases tiene que salir como la planilla con la que hoy importan al G4
/// (Importador v5.08): encabezados en la fila 1 con sus nombres exactos, la hoja con el mismo
/// nombre que el archivo, idImportacion correlativo y fecha / punto de venta / comprobante como texto.
/// </summary>
public class PaseXlsxExportTests
{
    private static readonly string[] HeadersPlanilla =
    [
        "idImportacion", "IdReferencia", "IdAgrupacionPase", "CodEmpresa", "codComprobante",
        "noImputaGestion", "noImputaContabilidad", "noImputaCentro", "noImputaAuxiliar",
        "puntoVenta", "numeroComprobante", "fecha", "codPersona", "codMoneda", "codListaDePrecios",
        "codConcepto", "cantidadAuxiliar", "cantidad", "precio",
        "codPerfilImputacionDebe", "codPerfilImputacionHaber",
        "codCuentaDebeGestion", "codCuentaHaberGestion", "codCuentaDebeCentro", "codCuentaHaberCentro",
        "codCuentaDebeContabilidad", "codCuentaHaberContabilidad", "codCuentaDebeAuxiliar", "codCuentaHaberAuxiliar",
        "Notas"
    ];

    private ApplicationDbContext CreateContext(string dbName, Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim("tenant_id", tenantId.ToString()),
                new Claim(ClaimTypes.Role, "Admin")
            }, "TestAuth"))
        };

        return new ApplicationDbContext(options, new HttpContextAccessor { HttpContext = httpContext });
    }

    private static PaseImputacion Pase(Guid tenantId, Guid loteId, int grupo, long concepto, long? persona, int moneda, decimal precio) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        PaseLoteId = loteId,
        IdReferencia = "699",
        IdAgrupacionPase = grupo,
        CodEmpresa = 4,
        CodComprobante = persona.HasValue ? 1080 : 1077,
        NoImputaContabilidad = true,
        NoImputaAuxiliar = true,
        PuntoVenta = 2526,
        NumeroComprobante = 699,
        Fecha = new DateTime(2026, 2, 21, 0, 0, 0, DateTimeKind.Utc),
        CodPersona = persona,
        CodMoneda = moneda,
        CodListaDePrecios = moneda == 1 ? 7 : 3,
        CodConcepto = concepto,
        CantidadAuxiliar = 28,
        Cantidad = 28,
        Precio = precio,
        CodCuentaDebeGestion = 11740310,
        CodCuentaHaberGestion = 21100010,
        CodCuentaDebeCentro = 10270000,
        CodCuentaHaberCentro = 10000005
    };

    private async Task<(XLWorkbook Wb, string FileName)> ExportarSegundoLoteAsync(Action<PaseImputacion>? ajustar = null)
    {
        var dbName = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid();
        var anterior = new PaseLote { Id = Guid.NewGuid(), TenantId = tenantId, GeneradoEn = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc) };
        var lote = new PaseLote { Id = Guid.NewGuid(), TenantId = tenantId, GeneradoEn = new DateTime(2026, 10, 13, 0, 0, 0, DateTimeKind.Utc) };

        using (var ctx = CreateContext(dbName, tenantId))
        {
            ctx.PasesLote.AddRange(anterior, lote);
            var labor = Pase(tenantId, lote.Id, 1, 50798, 339, 1, 12537m);
            var insumo = Pase(tenantId, lote.Id, 2, 50048, null, 2, 16.5m);
            ajustar?.Invoke(insumo);
            ctx.PasesImputacion.AddRange(labor, insumo);
            await ctx.SaveChangesAsync();
        }

        using var read = CreateContext(dbName, tenantId);
        var service = new PaseXlsxExportService(read, NullLogger<PaseXlsxExportService>.Instance);
        var (stream, fileName) = await service.ExportLoteAsync(tenantId, lote.Id);
        return (new XLWorkbook(stream), fileName);
    }

    [Fact]
    public async Task Layout_ComoLaPlanilla()
    {
        var (wb, fileName) = await ExportarSegundoLoteAsync();
        var ws = wb.Worksheets.Single();

        Assert.Equal("PasesG4_0002_20261013.xlsx", fileName);
        Assert.Equal(Path.GetFileNameWithoutExtension(fileName), ws.Name);
        // Las 30 columnas siempre, aunque el lote no use perfiles ni contabilidad.
        Assert.Equal(HeadersPlanilla, ws.Row(1).CellsUsed().Select(c => c.GetString()).ToArray());
        Assert.Equal(3, ws.LastRowUsed()!.RowNumber());
    }

    [Fact]
    public async Task Valores_ComoLaPlanilla()
    {
        var (wb, _) = await ExportarSegundoLoteAsync();
        var ws = wb.Worksheets.Single();
        XLCellValue Celda(int fila, string header) =>
            ws.Cell(fila, Array.IndexOf(HeadersPlanilla, header) + 1).Value;

        Assert.Equal(2, Celda(2, "idImportacion").GetNumber());
        Assert.Equal("02526", Celda(2, "puntoVenta").GetText());
        Assert.Equal("00000699", Celda(2, "numeroComprobante").GetText());
        Assert.Equal("2026-02-21", Celda(2, "fecha").GetText());
        Assert.True(Celda(2, "noImputaContabilidad").GetBoolean());
        Assert.False(Celda(2, "noImputaGestion").GetBoolean());
        Assert.Equal(339, Celda(2, "codPersona").GetNumber());
        Assert.True(Celda(3, "codPersona").IsBlank);
        Assert.Equal(16.5, Celda(3, "precio").GetNumber());
    }

    [Fact]
    public async Task ConPerfil_LaCuentaDeGestionVaVacia()
    {
        var (wb, _) = await ExportarSegundoLoteAsync(p => p.CodPerfilImputacionDebe = 55);
        var ws = wb.Worksheets.Single();
        int Col(string header) => Array.IndexOf(HeadersPlanilla, header) + 1;

        Assert.Equal(55, ws.Cell(3, Col("codPerfilImputacionDebe")).Value.GetNumber());
        Assert.True(ws.Cell(3, Col("codCuentaDebeGestion")).Value.IsBlank);
        Assert.Equal(11740310, ws.Cell(2, Col("codCuentaDebeGestion")).Value.GetNumber());
    }
}
