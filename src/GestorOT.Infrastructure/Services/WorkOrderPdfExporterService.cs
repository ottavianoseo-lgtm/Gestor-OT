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

        var plannedLabors = wo.Labors.Where(l => l.Mode == "Planned").ToList();
        var realizedLabors = wo.Labors.Where(l => l.Mode == "Realized" || l.Status == "Realized").ToList();
        var allLabors = wo.Labors.ToList();

        var pdf = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Helvetica"));

                page.Header().Column(c =>
                {
                    c.Item().AlignCenter().Text($"Orden de Trabajo #{wo.OTNumber}").FontSize(18).Bold();
                    c.Item().AlignCenter().Text(wo.Name ?? wo.Description ?? "").FontSize(12);
                    c.Item().PaddingTop(10).LineHorizontal(1);
                });

                page.Content().PaddingVertical(10).Column(content =>
                {
                    // ── Información General ──────────────────────────────────────
                    content.Item().Text("Información General").Bold().FontSize(11);
                    content.Item().PaddingTop(5).Table(t =>
                    {
                        t.ColumnsDefinition(cd => { cd.RelativeColumn(1); cd.RelativeColumn(1); });
                        t.Cell().Padding(3).Text(text => { text.Span("Estado: ").Bold(); text.Span(wo.Status); });
                        t.Cell().Padding(3).Text(text => { text.Span("Fecha: ").Bold(); text.Span(wo.DueDate.ToString("dd/MM/yyyy")); });
                        t.Cell().Padding(3).Text(text => { text.Span("Campaña: ").Bold(); text.Span("—"); });
                        t.Cell().Padding(3).Text(text => { text.Span("Responsable: ").Bold(); text.Span(wo.AssignedTo ?? "—"); });
                        t.Cell().ColumnSpan(2).Padding(3).Text(text =>
                        {
                            text.Span("Notas: ").Bold(); text.Span(wo.Description ?? "—");
                        });
                    });

                    // ── Labores Planeadas ────────────────────────────────────────
                    content.Item().PaddingTop(15).Text("Labores Planeadas").Bold().FontSize(11);
                    if (!plannedLabors.Any())
                    {
                        content.Item().PaddingTop(5)
                            .Text("Sin labores planeadas.")
                            .FontColor(Colors.Grey.Medium).Italic();
                    }
                    else
                    {
                        content.Item().PaddingTop(5).Table(t =>
                        {
                            t.ColumnsDefinition(cd =>
                            {
                                cd.RelativeColumn(2); cd.ConstantColumn(60);
                                cd.RelativeColumn(2); cd.RelativeColumn(2);
                                cd.RelativeColumn(2); cd.RelativeColumn(1.5f);
                                cd.RelativeColumn(1.5f); cd.ConstantColumn(50);
                                cd.ConstantColumn(60);
                            });
                            t.Header(h =>
                            {
                                foreach (var title in new[] { "Labor", "Fecha", "Persona", "Tipo Labor", "Actividad", "Lote", "Campo", "Ha", "Insumos" })
                                    h.Cell().Background(Colors.Grey.Lighten3).Padding(3).Text(title).Bold();
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
                                t.Cell().Padding(3).AlignRight().Text(labor.Hectares.ToString("N2"));
                                t.Cell().Padding(3).AlignRight().Text(labor.Supplies.Count.ToString());
                            }
                        });
                    }

                    // ── Labores Realizadas ───────────────────────────────────────
                    if (realizedLabors.Any())
                    {
                        content.Item().PaddingTop(15).Text("Labores Realizadas").Bold().FontSize(11);
                        content.Item().PaddingTop(5).Table(t =>
                        {
                            t.ColumnsDefinition(cd =>
                            {
                                cd.RelativeColumn(2); cd.ConstantColumn(60);
                                cd.RelativeColumn(2); cd.RelativeColumn(2);
                                cd.RelativeColumn(1.5f); cd.RelativeColumn(1.5f);
                                cd.ConstantColumn(50); cd.ConstantColumn(60);
                            });
                            t.Header(h =>
                            {
                                foreach (var title in new[] { "Labor", "Fecha Ejec.", "Persona", "Tipo Labor", "Lote", "Campo", "Ha Real", "Insumos" })
                                    h.Cell().Background(Colors.Green.Lighten3).Padding(3).Text(title).Bold();
                            });
                            foreach (var labor in realizedLabors)
                            {
                                t.Cell().Padding(3).Text(labor.LaborTypeName ?? "—");
                                t.Cell().Padding(3).Text((labor.ExecutionDate ?? labor.EstimatedDate ?? labor.CreatedAt).ToString("dd/MM/yy"));
                                t.Cell().Padding(3).Text(labor.AssignedTo ?? "—");
                                t.Cell().Padding(3).Text(labor.LaborTypeName ?? "—");
                                t.Cell().Padding(3).Text(labor.LotName ?? "—");
                                t.Cell().Padding(3).Text(labor.FieldName ?? "—");
                                t.Cell().Padding(3).AlignRight().Text(labor.Hectares.ToString("N2"));
                                t.Cell().Padding(3).AlignRight().Text(labor.Supplies.Count.ToString());
                            }
                        });
                    }

                    // ── Total de Insumos Planeados ──────────────────────────────
                    content.Item().PaddingTop(15).Text("Total de Insumos Planeados").Bold().FontSize(11);
                    if (!wo.SupplyApprovals.Any())
                    {
                        content.Item().PaddingTop(5).Text("Sin insumos consolidados.").FontColor(Colors.Grey.Medium).Italic();
                    }
                    else
                    {
                        content.Item().PaddingTop(5).Table(t =>
                        {
                            t.ColumnsDefinition(cd =>
                            {
                                cd.RelativeColumn(2); cd.RelativeColumn(1.5f);
                                cd.RelativeColumn(1.5f); cd.RelativeColumn(1.5f);
                                cd.RelativeColumn(2); cd.RelativeColumn(1.5f);
                            });
                            t.Header(h =>
                            {
                                foreach (var title in new[] { "Insumo", "Total Plan.", "Aprobado", "Total Real", "Centro Retiro", "Unidad" })
                                    h.Cell().Background(Colors.Grey.Lighten3).Padding(3).Text(title).Bold();
                            });
                            foreach (var a in wo.SupplyApprovals)
                            {
                                t.Cell().Padding(3).Text(a.SupplyName ?? "—");
                                t.Cell().Padding(3).AlignRight().Text(a.TotalCalculated.ToString("N2"));
                                t.Cell().Padding(3).AlignRight().Text(a.ApprovedWithdrawal.ToString("N2"));
                                var realStr = a.RealTotalUsed.HasValue ? a.RealTotalUsed.Value.ToString("N2") : "—";
                                t.Cell().Padding(3).AlignRight().Text(realStr);
                                t.Cell().Padding(3).Text(a.WithdrawalCenter ?? "—");
                                t.Cell().Padding(3).Text(a.SupplyUnit ?? "—");
                            }
                        });
                    }

                    // ── Detalle de Insumo por Labor ──────────────────────────────
                    content.Item().PaddingTop(15).Text("Detalle de Insumo por Labor").Bold().FontSize(11);
                    var laborsWithSupplies = allLabors.Where(l => l.Supplies.Any()).ToList();
                    if (!laborsWithSupplies.Any())
                    {
                        content.Item().PaddingTop(5).Text("Sin detalle de insumos.").FontColor(Colors.Grey.Medium).Italic();
                    }
                    else
                    {
                        content.Item().PaddingTop(5).Table(t =>
                        {
                            t.ColumnsDefinition(cd =>
                            {
                                cd.RelativeColumn(2); cd.RelativeColumn(2);
                                cd.RelativeColumn(1.5f); cd.RelativeColumn(1.5f);
                                cd.RelativeColumn(2); cd.ConstantColumn(50);
                                cd.ConstantColumn(50); cd.ConstantColumn(60);
                                cd.ConstantColumn(50);
                            });
                            t.Header(h =>
                            {
                                foreach (var title in new[] { "Labor", "Actividad", "Lote", "Campo", "Insumo", "Ha", "Coef/ha", "Cantidad", "Unidad" })
                                    h.Cell().Background(Colors.Grey.Lighten3).Padding(3).Text(title).Bold();
                            });
                            foreach (var labor in laborsWithSupplies)
                            {
                                foreach (var supply in labor.Supplies)
                                {
                                    var isRealized = labor.Mode == "Realized" || labor.Status == "Realized";
                                    var ha = isRealized
                                        ? (supply.RealHectares ?? supply.PlannedHectares)
                                        : supply.PlannedHectares;
                                    var dose = isRealized
                                        ? (supply.RealDose ?? supply.PlannedDose)
                                        : supply.PlannedDose;
                                    var total = isRealized
                                        ? (supply.RealTotal ?? supply.PlannedTotal)
                                        : supply.PlannedTotal;

                                    t.Cell().Padding(3).Text(labor.LaborTypeName ?? "—");
                                    t.Cell().Padding(3).Text(labor.ErpActivityName ?? "—");
                                    t.Cell().Padding(3).Text(labor.LotName ?? "—");
                                    t.Cell().Padding(3).Text(labor.FieldName ?? "—");
                                    t.Cell().Padding(3).Text(supply.SupplyName ?? "—");
                                    t.Cell().Padding(3).AlignRight().Text((ha > 0 ? ha : labor.Hectares).ToString("N2"));
                                    t.Cell().Padding(3).AlignRight().Text(dose.ToString("N2"));
                                    t.Cell().Padding(3).AlignRight().Text(total.ToString("N2"));
                                    t.Cell().Padding(3).Text(supply.SupplyUnit ?? "—");
                                }
                            }
                        });
                    }
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
