using GestorOT.Application.Services;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace GestorOT.Infrastructure.Services;

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
                    content.Item().Text("Información General").Bold().FontSize(11);
                    content.Item().PaddingTop(5).Table(t =>
                    {
                        t.ColumnsDefinition(cd =>
                        {
                            cd.RelativeColumn(1);
                            cd.RelativeColumn(1);
                        });

                        t.Cell().Padding(3).Text(text => { text.Span("Estado: ").Bold(); text.Span(wo.Status); });
                        t.Cell().Padding(3).Text(text => { text.Span("Fecha: ").Bold(); text.Span(wo.DueDate.ToString("dd/MM/yyyy")); });
                        t.Cell().Padding(3).Text(text => { text.Span("Campaña: ").Bold(); text.Span("—"); });
                        t.Cell().Padding(3).Text(text => { text.Span("Responsable: ").Bold(); text.Span(wo.AssignedTo ?? "—"); });
                        t.Cell().Padding(3).Column(col =>
                        {
                            col.Item().Text(text => { text.Span("Notas: ").Bold(); text.Span(wo.Description ?? "—"); });
                        });
                    });

                    content.Item().PaddingTop(15).Text("Labores Planeadas").Bold().FontSize(11);
                    var plannedLabors = wo.Labors.Where(l => l.Mode == "Planned").ToList();
                    content.Item().PaddingTop(5).Table(t =>
                    {
                        t.ColumnsDefinition(cd =>
                        {
                            cd.RelativeColumn(2);
                            cd.ConstantColumn(60);
                            cd.RelativeColumn(2);
                            cd.RelativeColumn(2);
                            cd.RelativeColumn(2);
                            cd.RelativeColumn(1.5f);
                            cd.RelativeColumn(1.5f);
                            cd.ConstantColumn(50);
                            cd.ConstantColumn(60);
                        });

                        t.Header(h =>
                        {
                            h.Cell().Background(Colors.Grey.Lighten3).Padding(3).Text("Labor").Bold();
                            h.Cell().Background(Colors.Grey.Lighten3).Padding(3).Text("Fecha").Bold();
                            h.Cell().Background(Colors.Grey.Lighten3).Padding(3).Text("Persona").Bold();
                            h.Cell().Background(Colors.Grey.Lighten3).Padding(3).Text("Tipo Labor").Bold();
                            h.Cell().Background(Colors.Grey.Lighten3).Padding(3).Text("Actividad").Bold();
                            h.Cell().Background(Colors.Grey.Lighten3).Padding(3).Text("Lote").Bold();
                            h.Cell().Background(Colors.Grey.Lighten3).Padding(3).Text("Campo").Bold();
                            h.Cell().Background(Colors.Grey.Lighten3).Padding(3).Text("Hectáreas").Bold();
                            h.Cell().Background(Colors.Grey.Lighten3).Padding(3).Text("Cant Insumos").Bold();
                        });

                        foreach (var labor in plannedLabors)
                        {
                            t.Cell().Padding(3).Text(labor.LaborTypeName ?? "—");
                            t.Cell().Padding(3).Text((labor.EstimatedDate ?? labor.CreatedAt).ToString("dd/MM/yy"));
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

                        foreach (var labor in plannedLabors)
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
