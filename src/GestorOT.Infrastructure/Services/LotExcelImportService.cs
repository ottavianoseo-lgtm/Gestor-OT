using ClosedXML.Excel;
using GestorOT.Application.Interfaces;
using GestorOT.Domain.Entities;
using GestorOT.Shared.Dtos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GestorOT.Infrastructure.Services;

public class LotExcelImportService : ILotExcelImportService
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<LotExcelImportService> _logger;

    public LotExcelImportService(
        IApplicationDbContext context,
        ILogger<LotExcelImportService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<LotImportSummaryDto> PreviewAsync(Guid campaignId, Stream fileStream, CancellationToken ct = default)
    {
        var campaign = await _context.Campaigns.FirstOrDefaultAsync(c => c.Id == campaignId, ct);
        if (campaign == null)
            throw new InvalidOperationException("La campaña especificada no existe.");

        using var workbook = new XLWorkbook(fileStream);
        var worksheet = FindDataWorksheet(workbook);
        var (headers, headerRow) = FindHeaderColumns(worksheet);

        var existingFields = await _context.Fields.AsNoTracking().ToListAsync(ct);
        var existingLots = await _context.Lots.AsNoTracking().ToListAsync(ct);
        var existingActivities = await _context.ErpActivities.AsNoTracking().ToListAsync(ct);

        var summary = new LotImportSummaryDto();
        var seenFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenLots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var newCropsSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        int lastRow = worksheet.LastRowUsed()?.RowNumber() ?? headerRow;
        int rowCounter = 0;

        for (int r = headerRow + 1; r <= lastRow; r++)
        {
            var row = worksheet.Row(r);
            if (row.IsEmpty()) continue;

            string campoStr = GetCellString(row.Cell(headers.ColCampo));
            string loteStr = GetCellString(row.Cell(headers.ColLote));

            if (string.IsNullOrWhiteSpace(campoStr) && string.IsNullOrWhiteSpace(loteStr))
                continue;

            rowCounter++;
            var declaredArea = ParseDecimalCell(row.Cell(headers.ColSuperficie));
            string? cropStr = headers.ColCultivo > 0 ? GetCellString(row.Cell(headers.ColCultivo)) : null;
            if (string.IsNullOrWhiteSpace(cropStr)) cropStr = null;

            DateOnly? fechaDesde = headers.ColFechaDesde > 0 ? ParseDateCell(row.Cell(headers.ColFechaDesde)) : null;
            DateOnly? fechaHasta = headers.ColFechaHasta > 0 ? ParseDateCell(row.Cell(headers.ColFechaHasta)) : null;
            string? notas = headers.ColNotas > 0 ? GetCellString(row.Cell(headers.ColNotas)) : null;

            var (effStart, effEnd, hasDateWarning) = ResolveRotationDates(fechaDesde, fechaHasta, campaign);

            var rowDto = new LotImportRowDto
            {
                RowNumber = rowCounter,
                FieldName = campoStr,
                LotName = loteStr,
                DeclaredAreaHa = declaredArea ?? 0,
                CropName = cropStr,
                StartDate = effStart,
                EndDate = effEnd,
                Notes = string.IsNullOrWhiteSpace(notas) ? null : notas
            };

            // Validation
            if (string.IsNullOrWhiteSpace(campoStr))
            {
                rowDto.Status = "Error";
                rowDto.ValidationMessage = "El nombre del campo es obligatorio.";
                summary.ErrorRows++;
            }
            else if (string.IsNullOrWhiteSpace(loteStr))
            {
                rowDto.Status = "Error";
                rowDto.ValidationMessage = "El nombre del lote es obligatorio.";
                summary.ErrorRows++;
            }
            else if (declaredArea == null || declaredArea <= 0)
            {
                rowDto.Status = "Error";
                rowDto.ValidationMessage = "La superficie debe ser un valor numérico mayor a cero.";
                summary.ErrorRows++;
            }
            else
            {
                // Field evaluation
                var fieldExists = existingFields.Any(f => f.Name.Equals(campoStr, StringComparison.OrdinalIgnoreCase));
                rowDto.IsFieldNew = !fieldExists;
                if (rowDto.IsFieldNew && seenFields.Add(campoStr))
                    summary.NewFieldsCount++;
                else if (fieldExists && seenFields.Add(campoStr))
                    summary.ExistingFieldsCount++;

                // Lot evaluation
                var matchingField = existingFields.FirstOrDefault(f => f.Name.Equals(campoStr, StringComparison.OrdinalIgnoreCase));
                var lotExists = matchingField != null && existingLots.Any(l => l.FieldId == matchingField.Id && l.Name.Equals(loteStr, StringComparison.OrdinalIgnoreCase));
                rowDto.IsLotNew = !lotExists;

                var lotKey = $"{campoStr}_{loteStr}";
                if (rowDto.IsLotNew && seenLots.Add(lotKey))
                    summary.NewLotsCount++;
                else if (lotExists && seenLots.Add(lotKey))
                    summary.ExistingLotsCount++;

                // Crop evaluation
                if (!string.IsNullOrEmpty(cropStr))
                {
                    var cropExists = existingActivities.Any(a => a.Name.Equals(cropStr, StringComparison.OrdinalIgnoreCase));
                    rowDto.IsCropNew = !cropExists;
                    if (rowDto.IsCropNew && newCropsSet.Add(cropStr))
                    {
                        summary.NewCropsToCreate.Add(cropStr);
                    }
                }

                // Check dates warning
                if (hasDateWarning)
                {
                    rowDto.Status = "Warning";
                    if (fechaDesde == null && fechaHasta == null)
                        rowDto.ValidationMessage = "Fechas no especificadas: se asignó el rango general de la campaña.";
                    else if (fechaDesde == null)
                        rowDto.ValidationMessage = "Fecha de inicio no especificada: se asignó automáticamente.";
                    else if (fechaHasta == null)
                        rowDto.ValidationMessage = "Fecha de fin no especificada: se asignó automáticamente.";
                    else
                        rowDto.ValidationMessage = "Inconsistencia en rango de fechas ajustada automáticamente.";

                    summary.WarningRows++;
                }
                else
                {
                    rowDto.Status = "Valid";
                    summary.ValidRows++;
                }

                summary.TotalHectares += rowDto.DeclaredAreaHa;
            }

            summary.Rows.Add(rowDto);
        }

        summary.TotalRows = summary.Rows.Count;
        return summary;
    }

    public async Task<LotImportResultDto> ExecuteAsync(Guid campaignId, Stream fileStream, CancellationToken ct = default)
    {
        var campaign = await _context.Campaigns.FirstOrDefaultAsync(c => c.Id == campaignId, ct);
        if (campaign == null)
            throw new InvalidOperationException("La campaña especificada no existe.");

        if (campaign.Status == "Locked")
            throw new InvalidOperationException("No se pueden importar lotes en una campaña bloqueada.");

        using var workbook = new XLWorkbook(fileStream);
        var worksheet = FindDataWorksheet(workbook);
        var (headers, headerRow) = FindHeaderColumns(worksheet);

        // Load existing entities for current tenant
        var existingFields = await _context.Fields.ToListAsync(ct);
        var existingLots = await _context.Lots.ToListAsync(ct);
        var existingActivities = await _context.ErpActivities.ToListAsync(ct);
        var existingCampFields = await _context.CampaignFields.Where(cf => cf.CampaignId == campaignId).ToListAsync(ct);
        var existingCampLots = await _context.CampaignLots.Include(cl => cl.Rotations).Where(cl => cl.CampaignId == campaignId).ToListAsync(ct);

        var fieldMap = existingFields.ToDictionary(f => f.Name.Trim().ToLowerInvariant(), f => f);
        var lotMap = existingLots.ToDictionary(l => (l.FieldId, l.Name.Trim().ToLowerInvariant()), l => l);
        var activityMap = existingActivities.ToDictionary(a => a.Name.Trim().ToLowerInvariant(), a => a);
        var campFieldMap = existingCampFields.ToDictionary(cf => cf.FieldId, cf => cf);
        var campLotMap = existingCampLots.ToDictionary(cl => cl.LotId, cl => cl);

        var result = new LotImportResultDto();
        var affectedCampFields = new HashSet<CampaignField>();

        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            var isRelational = _context.Database.IsRelational();
            Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? tx = isRelational ? await _context.Database.BeginTransactionAsync(ct) : null;

            try
            {
                int lastRow = worksheet.LastRowUsed()?.RowNumber() ?? headerRow;
                for (int r = headerRow + 1; r <= lastRow; r++)
                {
                    var row = worksheet.Row(r);
                    if (row.IsEmpty()) continue;

                    string campoStr = GetCellString(row.Cell(headers.ColCampo));
                    string loteStr = GetCellString(row.Cell(headers.ColLote));

                    if (string.IsNullOrWhiteSpace(campoStr) || string.IsNullOrWhiteSpace(loteStr))
                        continue;

                    var declaredArea = ParseDecimalCell(row.Cell(headers.ColSuperficie));
                    if (declaredArea == null || declaredArea <= 0)
                        continue;

                    decimal sup = declaredArea.Value;
                    string? cropStr = headers.ColCultivo > 0 ? GetCellString(row.Cell(headers.ColCultivo)) : null;
                    if (string.IsNullOrWhiteSpace(cropStr)) cropStr = null;

                    DateOnly? rawDesde = headers.ColFechaDesde > 0 ? ParseDateCell(row.Cell(headers.ColFechaDesde)) : null;
                    DateOnly? rawHasta = headers.ColFechaHasta > 0 ? ParseDateCell(row.Cell(headers.ColFechaHasta)) : null;
                    var (fechaDesde, fechaHasta, _) = ResolveRotationDates(rawDesde, rawHasta, campaign);
                    string? notas = headers.ColNotas > 0 ? GetCellString(row.Cell(headers.ColNotas)) : null;

                    // 1. Campo (Field)
                    string fieldKey = campoStr.Trim().ToLowerInvariant();
                    if (!fieldMap.TryGetValue(fieldKey, out var field))
                    {
                        field = new Field
                        {
                            Id = Guid.NewGuid(),
                            Name = campoStr.Trim(),
                            CreatedAt = DateTime.UtcNow
                        };
                        _context.Fields.Add(field);
                        fieldMap[fieldKey] = field;
                        result.FieldsCreated++;
                    }

                    // 2. CampaignField
                    if (!campFieldMap.TryGetValue(field.Id, out var campField))
                    {
                        campField = new CampaignField
                        {
                            Id = Guid.NewGuid(),
                            CampaignId = campaignId,
                            FieldId = field.Id,
                            TargetYieldTonHa = 0,
                            AllocatedHectares = 0
                        };
                        _context.CampaignFields.Add(campField);
                        campFieldMap[field.Id] = campField;
                    }
                    affectedCampFields.Add(campField);

                    // 3. Lote (Lot)
                    string lotKey = loteStr.Trim().ToLowerInvariant();
                    if (!lotMap.TryGetValue((field.Id, lotKey), out var lot))
                    {
                        lot = new Lot
                        {
                            Id = Guid.NewGuid(),
                            FieldId = field.Id,
                            Name = loteStr.Trim(),
                            CadastralArea = sup,
                            Status = "Active"
                        };
                        _context.Lots.Add(lot);
                        lotMap[(field.Id, lotKey)] = lot;
                        result.LotsCreated++;
                    }
                    else
                    {
                        if (lot.CadastralArea == 0)
                            lot.CadastralArea = sup;
                    }

                    // 4. CampaignLot
                    if (!campLotMap.TryGetValue(lot.Id, out var campLot))
                    {
                        campLot = new CampaignLot
                        {
                            Id = Guid.NewGuid(),
                            CampaignId = campaignId,
                            LotId = lot.Id,
                            ProductiveArea = sup
                        };
                        _context.CampaignLots.Add(campLot);
                        campLotMap[lot.Id] = campLot;
                        result.CampaignLotsLinked++;
                    }
                    else
                    {
                        campLot.ProductiveArea = sup;
                    }

                    result.TotalHectares += sup;

                    // 5. Cultivo y Rotación
                    if (!string.IsNullOrWhiteSpace(cropStr))
                    {
                        string cropKey = cropStr.Trim().ToLowerInvariant();
                        if (!activityMap.TryGetValue(cropKey, out var activity))
                        {
                            activity = new ErpActivity
                            {
                                Id = Guid.NewGuid(),
                                Name = cropStr.Trim(),
                                IsActive = true
                            };
                            _context.ErpActivities.Add(activity);
                            activityMap[cropKey] = activity;
                            result.CropsCreated++;
                        }

                        // Check if rotation already exists with same activity & campaignLot
                        var existingRotation = campLot.Rotations.FirstOrDefault(rot => rot.ErpActivityId == activity.Id);
                        if (existingRotation == null)
                        {
                            var rotation = new Rotation
                            {
                                Id = Guid.NewGuid(),
                                CampaignLotId = campLot.Id,
                                ErpActivityId = activity.Id,
                                StartDate = fechaDesde,
                                EndDate = fechaHasta,
                                Notes = string.IsNullOrWhiteSpace(notas) ? null : notas.Trim()
                            };
                            _context.Rotations.Add(rotation);
                            campLot.Rotations.Add(rotation);
                            result.RotationsCreated++;
                        }
                        else
                        {
                            existingRotation.StartDate = fechaDesde;
                            existingRotation.EndDate = fechaHasta;
                            if (!string.IsNullOrWhiteSpace(notas))
                                existingRotation.Notes = notas.Trim();
                        }
                    }
                }

                // Recalculate AllocatedHectares on CampaignFields
                foreach (var cf in affectedCampFields)
                {
                    var lotsInField = lotMap.Values.Where(l => l.FieldId == cf.FieldId).Select(l => l.Id).ToHashSet();
                    var sumArea = campLotMap.Values.Where(cl => lotsInField.Contains(cl.LotId)).Sum(cl => cl.ProductiveArea);
                    cf.AllocatedHectares = sumArea;
                }

                await _context.SaveChangesAsync(ct);
                if (tx != null)
                    await tx.CommitAsync(ct);

                result.Success = true;
                result.Message = $"Se procesaron con éxito los datos: {result.FieldsCreated} campos creados, {result.LotsCreated} lotes creados, {result.CampaignLotsLinked} lotes asociados a la campaña y {result.RotationsCreated} rotaciones registradas ({result.TotalHectares:N2} ha totales).";
                return result;
            }
            catch (Exception ex)
            {
                if (tx != null)
                    await tx.RollbackAsync(ct);
                _logger.LogError(ex, "Error al ejecutar la importación masiva de lotes para la campaña {CampaignId}", campaignId);
                throw;
            }
            finally
            {
                if (tx != null)
                    await tx.DisposeAsync();
            }
        });
    }

    public async Task<(byte[] Bytes, string FileName)> GenerateTemplateAsync(CancellationToken ct = default)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Campos_Lotes_Rotacion");

        // Headers
        string[] headers =
        [
            "Campo",
            "Lote",
            "Superficie Declarada (ha)",
            "Cultivo Actual",
            "Fecha Desde",
            "Fecha Hasta",
            "Notas"
        ];

        for (int i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(1, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = XLColor.FromArgb(39, 174, 96);
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        // Example rows
        var examples = new (string Campo, string Lote, decimal Sup, string Cultivo, string Desde, string Hasta, string Notas)[]
        {
            ("Bassi-Prieto", "Bassi", 67.0m, "Maíz tardío", "2026-11-01", "2027-07-01", ""),
            ("Bassi-Prieto", "Lobianco", 27.0m, "Maíz tardío", "2026-11-01", "2027-07-01", ""),
            ("Breit", "Breit", 155.0m, "Girasol", "2026-09-01", "2027-05-01", ""),
            ("Corral", "Kiko", 43.0m, "Trigo", "2026-04-01", "2026-12-25", "Lote principal"),
            ("La Casuarina", "30", 48.0m, "Maíz", "2026-09-01", "2027-05-01", "Superficie estimada")
        };

        for (int r = 0; r < examples.Length; r++)
        {
            var ex = examples[r];
            ws.Cell(r + 2, 1).Value = ex.Campo;
            ws.Cell(r + 2, 2).Value = ex.Lote;
            ws.Cell(r + 2, 3).Value = ex.Sup;
            ws.Cell(r + 2, 4).Value = ex.Cultivo;
            ws.Cell(r + 2, 5).Value = ex.Desde;
            ws.Cell(r + 2, 6).Value = ex.Hasta;
            ws.Cell(r + 2, 7).Value = ex.Notas;
        }

        ws.Columns().AdjustToContents();

        // Sheet 2: Instrucciones
        var wsInst = workbook.Worksheets.Add("Instrucciones");
        wsInst.Cell(1, 1).Value = "Instrucciones de Importación de Campos, Lotes y Rotaciones";
        wsInst.Cell(1, 1).Style.Font.Bold = true;
        wsInst.Cell(1, 1).Style.Font.FontSize = 14;

        string[] instructions =
        [
            "1. La columna 'Campo' es obligatoria. Si el campo ya existe en el sistema, se reutiliza; si no, se crea.",
            "2. La columna 'Lote' es obligatoria. Se asocia al campo correspondiente.",
            "3. La columna 'Superficie Declarada (ha)' debe ser un número mayor a cero.",
            "4. La columna 'Cultivo Actual' representa la rotación asignada a ese lote en la campaña.",
            "5. Si 'Fecha Desde' o 'Fecha Hasta' se dejan en blanco, el sistema asignará automáticamente las fechas de inicio y fin de la campaña.",
            "6. Esta importación se efectúa dentro de la campaña activa en la que te encuentres al momento de importar."
        ];

        for (int i = 0; i < instructions.Length; i++)
        {
            wsInst.Cell(i + 3, 1).Value = instructions[i];
        }

        wsInst.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return (ms.ToArray(), "Plantilla_Importacion_Campos_Lotes.xlsx");
    }

    private static IXLWorksheet FindDataWorksheet(XLWorkbook workbook)
    {
        var ws = workbook.Worksheets.FirstOrDefault(w => w.Name.Equals("Campos_Lotes_Rotacion", StringComparison.OrdinalIgnoreCase));
        if (ws != null) return ws;

        foreach (var candidate in workbook.Worksheets)
        {
            var firstRow = candidate.FirstRowUsed();
            if (firstRow != null)
            {
                bool hasCampo = false;
                bool hasLote = false;
                foreach (var cell in firstRow.CellsUsed())
                {
                    var text = cell.GetString().ToLowerInvariant();
                    if (text.Contains("campo")) hasCampo = true;
                    if (text.Contains("lote")) hasLote = true;
                }
                if (hasCampo && hasLote) return candidate;
            }
        }

        var defaultWs = workbook.Worksheets.FirstOrDefault();
        if (defaultWs == null)
            throw new InvalidOperationException("El archivo Excel no contiene ninguna hoja con datos.");

        return defaultWs;
    }

    private static (HeaderIndices Indices, int HeaderRow) FindHeaderColumns(IXLWorksheet ws)
    {
        int lastRow = Math.Min(ws.LastRowUsed()?.RowNumber() ?? 1, 10);
        for (int r = 1; r <= lastRow; r++)
        {
            var row = ws.Row(r);
            var indices = new HeaderIndices();
            int lastCol = row.LastCellUsed()?.Address.ColumnNumber ?? 0;

            for (int c = 1; c <= lastCol; c++)
            {
                var text = row.Cell(c).GetString().Trim().ToLowerInvariant();
                if (string.IsNullOrEmpty(text)) continue;

                if (text.Contains("campo") && indices.ColCampo == 0) indices.ColCampo = c;
                else if (text.Contains("lote") && indices.ColLote == 0) indices.ColLote = c;
                else if ((text.Contains("superficie") || text.Contains("hectarea") || text.Contains("(ha)")) && indices.ColSuperficie == 0) indices.ColSuperficie = c;
                else if ((text.Contains("cultivo") || text.Contains("actividad")) && indices.ColCultivo == 0) indices.ColCultivo = c;
                else if ((text.Contains("desde") || text.Contains("inicio")) && indices.ColFechaDesde == 0) indices.ColFechaDesde = c;
                else if ((text.Contains("hasta") || text.Contains("fin")) && indices.ColFechaHasta == 0) indices.ColFechaHasta = c;
                else if ((text.Contains("nota") || text.Contains("obs")) && indices.ColNotas == 0) indices.ColNotas = c;
            }

            if (indices.ColCampo > 0 && indices.ColLote > 0 && indices.ColSuperficie > 0)
            {
                return (indices, r);
            }
        }

        throw new InvalidOperationException("No se encontraron los encabezados obligatorios ('Campo', 'Lote', 'Superficie') en la hoja.");
    }

    private static string GetCellString(IXLCell cell)
    {
        if (cell.IsEmpty()) return string.Empty;
        return cell.GetString().Trim();
    }

    private static decimal? ParseDecimalCell(IXLCell cell)
    {
        if (cell.IsEmpty()) return null;
        if (cell.DataType == XLDataType.Number)
            return (decimal)cell.GetDouble();

        var str = cell.GetString().Trim().Replace(",", ".");
        if (decimal.TryParse(str, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var val))
            return val;

        return null;
    }

    private static DateOnly? ParseDateCell(IXLCell cell)
    {
        if (cell.IsEmpty()) return null;

        if (cell.DataType == XLDataType.DateTime)
        {
            var dt = cell.GetDateTime();
            return DateOnly.FromDateTime(dt);
        }

        if (cell.DataType == XLDataType.Number)
        {
            try
            {
                var dt = DateTime.FromOADate(cell.GetDouble());
                return DateOnly.FromDateTime(dt);
            }
            catch { }
        }

        var str = cell.GetString().Trim();
        if (string.IsNullOrWhiteSpace(str)) return null;

        if (DateOnly.TryParse(str, out var dOnly))
            return dOnly;

        if (DateTime.TryParse(str, out var dtParsed))
            return DateOnly.FromDateTime(dtParsed);

        return null;
    }

    private static (DateOnly StartDate, DateOnly EndDate, bool HasWarning) ResolveRotationDates(
        DateOnly? fechaDesde, DateOnly? fechaHasta, Campaign campaign)
    {
        bool hasWarning = false;
        DateOnly start;
        DateOnly end;

        if (fechaDesde.HasValue && fechaHasta.HasValue)
        {
            start = fechaDesde.Value;
            end = fechaHasta.Value;
        }
        else if (fechaDesde.HasValue && !fechaHasta.HasValue)
        {
            hasWarning = true;
            start = fechaDesde.Value;
            end = campaign.EndDate >= start ? campaign.EndDate : start.AddMonths(6);
        }
        else if (!fechaDesde.HasValue && fechaHasta.HasValue)
        {
            hasWarning = true;
            end = fechaHasta.Value;
            start = campaign.StartDate <= end ? campaign.StartDate : end.AddMonths(-6);
        }
        else
        {
            hasWarning = true;
            start = campaign.StartDate;
            end = campaign.EndDate >= start ? campaign.EndDate : start.AddMonths(6);
        }

        if (start > end)
        {
            hasWarning = true;
            start = end.AddMonths(-6);
        }

        return (start, end, hasWarning);
    }

    private class HeaderIndices
    {
        public int ColCampo { get; set; }
        public int ColLote { get; set; }
        public int ColSuperficie { get; set; }
        public int ColCultivo { get; set; }
        public int ColFechaDesde { get; set; }
        public int ColFechaHasta { get; set; }
        public int ColNotas { get; set; }
    }
}
