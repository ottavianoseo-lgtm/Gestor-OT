# Bug 4 — Exportar OT a PDF imprimible

> Módulo: **Detalle de OT — exportación**
> Imagen del PDF: **imagen5** (ejemplo de "Orden de Trabajo #12" con encabezado, Información General, Labores Planeadas, Total de Insumos Planeados, Detalle de Insumo por Labor Planeada)
> Criticidad: **Alta** (la práctica común es imprimir, no formulario digital)
> Estimación: **8-12h** (incluye introducir QuestPDF, endpoint, botón UI, tests, smoke)

---

## 1. Qué pide el bug

> "Necesitamos poder descargar una OT en formato PDF para imprimirla. Dejo un ejemplo de la forma común de verlas, acorde al prototipo original. Ahí tenes toda la info junta, encabezado, labores, total de insumos a utilizar, detalle de aplicaciónes. En el formato que estan haciendo uds es distinto porque estan metiendo el detalle de insumos a aplicar debajo de cada labor. Que no está mal, pero la info tiene que estar."

El **layout esperado** (imagen5) tiene esta estructura:

```
Orden de Trabajo #12
OT Franco
─────────────────────────────────────────
Información General
Estado: en_progreso                Fecha: 28/01/2026
Campaña: Campaña 2026-2027         Responsable: AGROSERVICIOS GRANERO CHICO SA
Notas: PRUEBA

Labores Planeadas
┌──────────────┬─────────┬───────────────┬─────────────┬──────────┬───────┬─────────┬──────────┬─────────────┐
│ Labor        │ Fecha   │ Persona       │ Tipo Labor  │ Actividad│ Lote  │ Campo   │ Hectáreas│ Cant Insumos│
├──────────────┼─────────┼───────────────┼─────────────┼──────────┼───────┼─────────┼──────────┼─────────────┤
│ Labor Demo   │ 28/1/26 │ ARIEU ISABEL  │ FUMIGACION  │ MAIZ 505 │ Lote  │ El      │ 15       │ 1           │
│ Franco       │         │ MARÍA         │ TERRESTRE   │ RR2 C16  │ Franco│ Porvenir│          │             │
...

Total de Insumos Planeados
┌──────────┬───────────────┬───────────────┬─────────────────┬─────────────┐
│ Insumo   │ Total Planeado│ Total Aprobado│ Centro de Retiro│ Unidad      │
├──────────┼───────────────┼───────────────┼─────────────────┼─────────────┤
│ GLIFOSATO│ 30.00         │ 20.00         │ Galpon          │ Hectárea(HA)│

Detalle de Insumo por Labor Planeada
┌──────────────┬──────────────┬────────┬─────────┬──────────┬──────────┬────────┬──────────┬───────┐
│ Labor        │ Actividad    │ Lote   │ Campo   │ Insumo   │ Hectáreas│ Coef/ha│ Cantidad │ Unidad│
├──────────────┼──────────────┼────────┼─────────┼──────────┼──────────┼────────┼──────────┼───────┤
│ Labor Demo F.│ MAIZ 505 RR2 │ Lote   │ El      │ GLIFOSATO│ 10       │ 2      │ 20       │ HA    │
...

Documento generado el 13 de mayo de 2026 a las 17:34
```

## 2. Estado actual del código

- **No hay librería de PDF** instalada. `GestorOT.Api.csproj` solo tiene AntDesign, EF Core 10, Npgsql.
- Existe `HtmlLaborExporterService` (`Infrastructure/Services/HtmlLaborExporterService.cs`) que genera HTML interactivo para que el contratista llene desde el celular. **Eso no sirve para imprimir** — es un formulario web, no un documento.
- El botón "Compartir Reporte" en `WorkOrderDetailFinal.razor` línea 28 invoca `ExportHtmlInteractivo`, que es el flujo viejo.

## 3. Decisión de librería

El plan elige **QuestPDF**:

- **Pros**: PDF nativo, sin Chromium, gratis comercial hasta 1M USD/año, API fluent en C#, performance excelente, soporte para .NET 10 confirmado.
- **Contras**: license dual (Community / Professional). Para uso comercial > 1M USD/año hay que pagar (~$5k/año). **No es problema actual** para Gestor OT.

Alternativas descartadas:

| Librería | Por qué no |
|---|---|
| Puppeteer-Sharp | Requiere Chromium en el container → +200MB. Overkill para este caso. |
| iText 7 | Licencia AGPL en versión gratuita (impone open source en el código que lo usa). Mal fit. |
| PdfSharp | Buena, pero API más verbosa que QuestPDF. Mantenimiento más lento. |
| HTML + `window.print()` | Lo más simple pero deja la calidad del output al criterio del navegador del usuario. Decisión rechazada para entregar un PDF predecible. |

**Si el usuario prefiere otra**, los DTOs y endpoint quedan iguales, solo cambia el servicio que genera el byte stream.

## 4. Pre-lectura obligatoria con context7 (MCP)

1. `context7` — **QuestPDF latest stable**: setup en .NET 10, `Document.Create`, `Composer`, tablas con `RelativeColumn`/`ConstantColumn`, page numbers, headers/footers.
2. `context7` — **QuestPDF Community license**: confirmar requisitos del nag screen (`QuestPDF.Settings.License = LicenseType.Community`) y notice en código.
3. `context7` — **ASP.NET Core 10 `FileStreamResult` / `File(byte[], contentType, fileName)`**: best practice para devolver PDFs.
4. `context7` — **Blazor WASM**: `IJSRuntime` para disparar download de `byte[]` recibido por API.

## 5. Plan de implementación

### 5.1 Agregar dependencia

`src/GestorOT.Api/GestorOT.Api.csproj`:

```xml
<PackageReference Include="QuestPDF" Version="2025.9.0" />
```

(Versión real a confirmar con `context7` al momento del fix.)

### 5.2 Configurar licencia (una sola vez en `Program.cs`)

En `src/GestorOT.Api/Program.cs`, al inicio del archivo:

```csharp
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
```

Esto debe ir **antes** de cualquier uso de QuestPDF. Si falta, lanza excepción en producción.

### 5.3 Crear servicio de export

**Archivos nuevos:**

- `src/GestorOT.Application/Services/IWorkOrderPdfExporterService.cs`
- `src/GestorOT.Infrastructure/Services/WorkOrderPdfExporterService.cs`

Interfaz:

```csharp
public interface IWorkOrderPdfExporterService
{
    Task<byte[]> GeneratePdfAsync(Guid workOrderId, CancellationToken ct = default);
}
```

Implementación (esqueleto):

```csharp
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

public class WorkOrderPdfExporterService : IWorkOrderPdfExporterService
{
    private readonly IWorkOrderQueryService _queryService;

    public WorkOrderPdfExporterService(IWorkOrderQueryService queryService)
    {
        _queryService = queryService;
    }

    public async Task<byte[]> GeneratePdfAsync(Guid workOrderId, CancellationToken ct = default)
    {
        var wo = await _queryService.GetByIdAsync(workOrderId, ct);
        if (wo == null) throw new InvalidOperationException("OT no encontrada.");

        var pdf = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Helvetica"));

                page.Header().Column(c =>
                {
                    c.Item().AlignCenter().Text($"Orden de Trabajo #{wo.OTNumber}")
                        .FontSize(18).Bold();
                    c.Item().AlignCenter().Text(wo.Name ?? wo.Description ?? "")
                        .FontSize(12);
                    c.Item().PaddingTop(10).LineHorizontal(1);
                });

                page.Content().PaddingVertical(10).Column(content =>
                {
                    // 1. Información General (2 columnas)
                    content.Item().Text("Información General").Bold().FontSize(11);
                    content.Item().PaddingTop(5).Table(t =>
                    {
                        t.ColumnsDefinition(cd =>
                        {
                            cd.RelativeColumn(1);
                            cd.RelativeColumn(1);
                        });
                        t.Cell().Text(text => { text.Span("Estado: ").Bold(); text.Span(wo.Status); });
                        t.Cell().Text(text => { text.Span("Fecha: ").Bold(); text.Span(wo.DueDate.ToString("dd/MM/yyyy")); });
                        // ... Campaña, Responsable, Notas
                    });

                    content.Item().PaddingTop(15).Text("Labores Planeadas").Bold().FontSize(11);
                    content.Item().PaddingTop(5).Table(t =>
                    {
                        // 9 columnas: Labor, Fecha, Persona, Tipo, Actividad, Lote, Campo, Ha, Cant Insumos
                        t.ColumnsDefinition(cd =>
                        {
                            cd.RelativeColumn(2); // Labor
                            cd.ConstantColumn(60); // Fecha
                            cd.RelativeColumn(2); // Persona
                            cd.RelativeColumn(2); // Tipo
                            cd.RelativeColumn(2); // Actividad
                            cd.RelativeColumn(1.5f); // Lote
                            cd.RelativeColumn(1.5f); // Campo
                            cd.ConstantColumn(50); // Ha
                            cd.ConstantColumn(60); // Cant Insumos
                        });
                        // Header
                        t.Header(h =>
                        {
                            h.Cell().Background(Colors.Grey.Lighten3).Padding(3).Text("Labor").Bold();
                            // ... resto de headers
                        });
                        foreach (var labor in wo.Labors.Where(l => l.Mode == "Planned"))
                        {
                            t.Cell().Padding(3).Text(labor.LaborTypeName ?? "—");
                            t.Cell().Padding(3).Text((labor.EstimatedDate ?? labor.CreatedAt).ToString("dd/MM/yyyy"));
                            t.Cell().Padding(3).Text(labor.AssignedTo ?? "—");
                            t.Cell().Padding(3).Text(labor.LaborTypeName ?? "—");
                            t.Cell().Padding(3).Text(labor.ErpActivityName ?? "—");
                            t.Cell().Padding(3).Text(labor.LotName ?? "—");
                            t.Cell().Padding(3).Text(labor.FieldName ?? "—");
                            t.Cell().Padding(3).AlignRight().Text(labor.Hectares.ToString("N0"));
                            t.Cell().Padding(3).AlignRight().Text(labor.Supplies.Count.ToString());
                        }
                    });

                    content.Item().PaddingTop(15).Text("Total de Insumos Planeados").Bold().FontSize(11);
                    content.Item().PaddingTop(5).Table(t =>
                    {
                        // 5 columnas: Insumo, Total Planeado, Total Aprobado, Centro de Retiro, Unidad
                        t.ColumnsDefinition(cd =>
                        {
                            cd.RelativeColumn(2);
                            cd.RelativeColumn(1.5f);
                            cd.RelativeColumn(1.5f);
                            cd.RelativeColumn(2);
                            cd.RelativeColumn(1.5f);
                        });
                        t.Header(h =>
                        {
                            h.Cell().Background(Colors.Grey.Lighten3).Padding(3).Text("Insumo").Bold();
                            h.Cell().Background(Colors.Grey.Lighten3).Padding(3).Text("Total Planeado").Bold();
                            h.Cell().Background(Colors.Grey.Lighten3).Padding(3).Text("Total Aprobado").Bold();
                            h.Cell().Background(Colors.Grey.Lighten3).Padding(3).Text("Centro de Retiro").Bold();
                            h.Cell().Background(Colors.Grey.Lighten3).Padding(3).Text("Unidad").Bold();
                        });
                        foreach (var a in wo.SupplyApprovals)
                        {
                            t.Cell().Padding(3).Text(a.SupplyName ?? "—");
                            t.Cell().Padding(3).AlignRight().Text(a.TotalCalculated.ToString("N2"));
                            t.Cell().Padding(3).AlignRight().Text(a.ApprovedWithdrawal.ToString("N2"));
                            t.Cell().Padding(3).Text(a.WithdrawalCenter ?? "—");
                            t.Cell().Padding(3).Text(a.SupplyUnit ?? "—");
                        }
                    });

                    content.Item().PaddingTop(15).Text("Detalle de Insumo por Labor Planeada").Bold().FontSize(11);
                    content.Item().PaddingTop(5).Table(t =>
                    {
                        // 9 columnas: Labor, Actividad, Lote, Campo, Insumo, Hectáreas, Coef/ha, Cantidad, Unidad
                        t.ColumnsDefinition(cd =>
                        {
                            cd.RelativeColumn(2);
                            cd.RelativeColumn(2);
                            cd.RelativeColumn(1.5f);
                            cd.RelativeColumn(1.5f);
                            cd.RelativeColumn(2);
                            cd.ConstantColumn(50);
                            cd.ConstantColumn(50);
                            cd.ConstantColumn(60);
                            cd.ConstantColumn(50);
                        });
                        t.Header(h =>
                        {
                            foreach (var title in new[] { "Labor", "Actividad", "Lote", "Campo", "Insumo", "Hectáreas", "Coef/ha", "Cantidad", "Unidad" })
                            {
                                h.Cell().Background(Colors.Grey.Lighten3).Padding(3).Text(title).Bold();
                            }
                        });
                        foreach (var labor in wo.Labors.Where(l => l.Mode == "Planned"))
                        {
                            foreach (var supply in labor.Supplies)
                            {
                                t.Cell().Padding(3).Text(labor.LaborTypeName ?? "—");
                                t.Cell().Padding(3).Text(labor.ErpActivityName ?? "—");
                                t.Cell().Padding(3).Text(labor.LotName ?? "—");
                                t.Cell().Padding(3).Text(labor.FieldName ?? "—");
                                t.Cell().Padding(3).Text(supply.SupplyName ?? "—");
                                t.Cell().Padding(3).AlignRight().Text((supply.PlannedHectares > 0 ? supply.PlannedHectares : labor.Hectares).ToString("N0"));
                                t.Cell().Padding(3).AlignRight().Text(supply.PlannedDose.ToString("N0"));
                                t.Cell().Padding(3).AlignRight().Text(supply.PlannedTotal.ToString("N0"));
                                t.Cell().Padding(3).Text(supply.SupplyUnit ?? "—");
                            }
                        }
                    });
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span($"Documento generado el {DateTime.Now:dd 'de' MMMM 'de' yyyy 'a las' HH:mm}")
                        .FontSize(8).FontColor(Colors.Grey.Medium);
                });
            });
        });

        return pdf.GeneratePdf();
    }
}
```

Registrar el servicio en `ServiceExtensions.cs`:

```csharp
services.AddScoped<IWorkOrderPdfExporterService, WorkOrderPdfExporterService>();
```

### 5.4 Crear endpoint

**Archivo:** `src/GestorOT.Api/Controllers/WorkOrdersController.cs`. Agregar al final del controller, junto a los otros export-csv y similar:

```csharp
[HttpGet("{id:guid}/export-pdf")]
public async Task<IActionResult> ExportPdf(Guid id, CancellationToken ct)
{
    var wo = await _context.WorkOrders
        .AsNoTracking()
        .FirstOrDefaultAsync(w => w.Id == id, ct);
    if (wo == null) return NotFound();

    var pdf = await _pdfExporter.GeneratePdfAsync(id, ct);
    var fileName = $"OT-{wo.OTNumber}-{DateTime.Now:yyyyMMdd}.pdf";
    return File(pdf, "application/pdf", fileName);
}
```

Inyectar `IWorkOrderPdfExporterService _pdfExporter` en el constructor del controller.

### 5.5 Frontend — botón "Descargar PDF"

**Archivo:** `src/GestorOT.Client/Pages/WorkOrderDetailFinal.razor` líneas 27-30.

Reemplazar:

```razor
<div style="display: flex; gap: 12px;">
    <Button OnClick="ExportHtmlInteractivo" Icon="share-alt" Ghost>Compartir Reporte</Button>
    <Button Type="@ButtonType.Primary" OnClick="SaveAllChanges" ...>Guardar Cambios</Button>
</div>
```

Por:

```razor
<div style="display: flex; gap: 12px;">
    <Button OnClick="ExportPdf" Icon="file-pdf" Ghost>Descargar PDF</Button>
    <Button OnClick="ExportHtmlInteractivo" Icon="share-alt" Ghost>Compartir Reporte</Button>
    <Button Type="@ButtonType.Primary" OnClick="SaveAllChanges" ...>Guardar Cambios</Button>
</div>
```

En `OTDetalleFinalBase.cs` agregar:

```csharp
protected async Task ExportPdf()
{
    try
    {
        var response = await _http.GetAsync($"api/workorders/{WorkOrderId}/export-pdf");
        if (!response.IsSuccessStatusCode)
        {
            _message.Error($"Error al generar PDF: {response.StatusCode}");
            return;
        }

        var bytes = await response.Content.ReadAsByteArrayAsync();
        var fileName = response.Content.Headers.ContentDisposition?.FileName?.Trim('"')
                       ?? $"OT-{WorkOrderId}.pdf";

        await _js.InvokeVoidAsync("downloadFile", fileName, "application/pdf", Convert.ToBase64String(bytes));
    }
    catch (Exception ex)
    {
        _message.Error($"Error: {ex.Message}");
    }
}
```

Y en `wwwroot/js/site.js` (o equivalente, verificar dónde el repo guarda funciones JS):

```javascript
window.downloadFile = function (fileName, contentType, base64Data) {
    const link = document.createElement('a');
    link.href = `data:${contentType};base64,${base64Data}`;
    link.download = fileName;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
};
```

**Acción del agente**: verificar si ya existe una función similar (`downloadFile`, `saveAs`, etc.). Reutilizar si está.

### 5.6 Localización del footer

QuestPDF respeta `CultureInfo.CurrentCulture`. Para que el footer salga en español ("13 de mayo de 2026 a las 17:34" en lugar de "May 13, 2026 at 5:34 PM"), setear en `Program.cs`:

```csharp
var defaultCulture = new System.Globalization.CultureInfo("es-AR");
System.Globalization.CultureInfo.DefaultThreadCurrentCulture = defaultCulture;
System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = defaultCulture;
```

**Si esto ya está**, no tocar.

## 6. Tests

**Archivo nuevo:** `src/GestorOT.Tests/Regression/WorkOrderPdfExportTests.cs`

Casos:

1. **`ExportPdf_ReturnsPdfWithCorrectContentType`** — endpoint devuelve `application/pdf`.
2. **`ExportPdf_FileNameIncludesOTNumberAndDate`** — `OT-12-20260513.pdf` (o similar).
3. **`ExportPdf_NotFound_When OTDoesNotExist`** — Guid inválido → 404.
4. **`ExportPdf_PdfContainsOTNumberAndName`** — usar `PdfPig` o similar para extraer texto del byte stream y assert que contiene `wo.OTNumber`.
5. **`ExportPdf_PdfContainsAllPlannedLabors`** — OT con 3 labores planeadas → el texto del PDF contiene los 3 `LaborTypeName`.
6. **`ExportPdf_PdfContainsConsolidatedSupplies`** — OT con 2 supplies en approvals → ambos aparecen en la sección "Total de Insumos Planeados" del PDF.
7. **`ExportPdf_DoesNotIncludeRealizedLabors`** — labor con `Mode = "Realized"` → no aparece en la sección "Labores Planeadas". (Confirmar con usuario si este comportamiento es el deseado o si quiere incluir todas las labores con un indicador de estado.)
8. **`ExportPdf_HandlesEmptyApprovals_Gracefully`** — OT sin approvals → PDF se genera con la sección "Total de Insumos Planeados" vacía (sin crashear).

## 7. Smoke test manual

1. OT con: 4 labores planeadas, 2 supplies en approvals, fechas variadas. Click "Descargar PDF".
2. El navegador descarga `OT-{OTNumber}-{yyyyMMdd}.pdf`.
3. Abrir el PDF:
   - Encabezado: "Orden de Trabajo #12" + nombre debajo.
   - Información General: Estado, Fecha, Campaña, Responsable, Notas. Todos los campos llenos.
   - Tabla "Labores Planeadas" con las 4 labores y las 9 columnas en orden.
   - Tabla "Total de Insumos Planeados" con los 2 supplies y 5 columnas (Insumo, Total Planeado, Total Aprobado, Centro de Retiro, Unidad).
   - Tabla "Detalle de Insumo por Labor Planeada" con una fila por (labor × supply).
   - Footer: "Documento generado el {fecha en español}".
4. Imprimir el PDF en una impresora → se ve legible, sin tablas cortadas raras.
5. OT con muchos supplies (10+) → el PDF se pagina correctamente, sin texto encimado.
6. OT sin labores → PDF se genera con tabla vacía, sin crashear.
7. OT bloqueada (estado no editable) → se puede descargar el PDF igual (read-only).
8. **Smoke en producción**: la primera vez que un cliente real haga click, verificar que `QuestPDF.Settings.License = LicenseType.Community` está aplicado. Si no, lanza excepción visible.

## 8. Definition of Done específica

- [ ] Build limpio.
- [ ] `QuestPDF` agregado al `.csproj` del API.
- [ ] Licencia Community configurada en `Program.cs`.
- [ ] Localización `es-AR` (o la que use el resto del sistema).
- [ ] `IWorkOrderPdfExporterService` + implementación.
- [ ] Endpoint `GET /api/workorders/{id}/export-pdf`.
- [ ] Botón "Descargar PDF" en `WorkOrderDetailFinal.razor`.
- [ ] Función JS `downloadFile` agregada o reutilizada.
- [ ] 8 tests nuevos en `WorkOrderPdfExportTests.cs`, en verde.
- [ ] Smoke test de 8 pasos completado.
- [ ] PR description con consultas a `context7`.
- [ ] El PR depende de Bug 1 (`SupplyUnit` en DTO) — confirmar que ese PR esté mergeado primero.

## 9. Lo que NO se cambia en este PR

- El flujo de "Compartir Reporte" (HTML interactivo) — sigue funcionando para contratistas que prefieren formulario digital.
- El `HtmlLaborExporterService` (no se toca).
- `WorkOrderQueryService` (se reutiliza tal cual).
- Los DTOs (ya cubiertos por Bug 1).
- La generación de PDFs de otras entidades (Labores, Estrategias) — fuera de scope.

---

## Riesgos y mitigaciones

| Riesgo | Mitigación |
|---|---|
| El usuario quiere editar el layout sin código | QuestPDF no permite templates externos. Si esto se requiere, evaluar alternativa con Razor-to-PDF o plantilla HTML. **No para este PR.** |
| Generar PDF lento para OTs grandes (>100 labores) | QuestPDF puede generar en background con `IDocument.GeneratePdfAsync`. Si el usuario reporta lentitud, mover a un background job + notificación. |
| Caracteres especiales (acentos, ñ) renderizan mal | QuestPDF usa Helvetica por defecto que soporta latin-1. Para escritura de mayor calidad, embeber fuente custom. Smoke test debe incluir nombres con acentos. |
| Datos sensibles en PDF | El endpoint usa el mismo filtro multi-tenant que el resto. No hay riesgo nuevo. Verificar con un test de seguridad: usuario A no puede descargar PDF de OT del tenant B. |
| Tamaño del PDF | Sin imágenes embebidas, queda en ~50-150 KB. OK para descargar/imprimir. Si después se agregan logos/firmas, monitorear. |

---

## Nota para el agente

Este es el PR con **más superficie nueva** de los 4 (librería + servicio + endpoint + JS interop + botón). Pero es **el más aislado** — no toca la lógica de negocio existente. Si algo se rompe, se desactiva el botón y se vuelve atrás sin impacto en el resto.

Ejecutar **después** de los Bugs 1, 2 y 3 para que el PDF aproveche todos los campos nuevos (`SupplyUnit`, `WithdrawalCenter`, `ApprovedWithdrawal`).
