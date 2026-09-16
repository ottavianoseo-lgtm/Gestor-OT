using ClosedXML.Excel;
using GestorOT.Application.Interfaces;
using GestorOT.Domain.Entities;
using GestorOT.Shared.Dtos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;

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

        using var workbook = OpenWorkbookSafely(fileStream);
        var worksheet = FindDataWorksheet(workbook);
        var (headers, headerRow) = FindHeaderColumns(worksheet);

        var existingFields = await _context.Fields.AsNoTracking().ToListAsync(ct);
        var existingLots = await _context.Lots.AsNoTracking().ToListAsync(ct);
        var existingActivities = await _context.ErpActivities.AsNoTracking().ToListAsync(ct);
        if (!existingActivities.Any() && _context.CurrentTenantId != Guid.Empty)
        {
            existingActivities = await _context.ErpActivities
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(a => a.TenantId == Guid.Empty)
                .ToListAsync(ct);
        }

        var summary = new LotImportSummaryDto();
        var seenFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenLots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenCentros = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var contadasPorLote = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
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

            // El GIS es opcional, pero si viene tiene que terminar en un polígono válido: mejor
            // frenar en la vista previa que reventar al guardar en PostGIS. El parser normaliza
            // lo que puede (agujeros exportados como shells, anillos auto-intersectados) y avisa
            // cuáles tuvo que tocar.
            string gisRaw = headers.ColGis > 0 ? GetCellString(row.Cell(headers.ColGis)) : string.Empty;
            var gisValido = GeoJsonGeometryParser.TryParse(gisRaw, out var gisGeometry, out var gisError, out var gisReparada);

            long? codCentro = headers.ColCentro > 0 ? ParseLongCell(row.Cell(headers.ColCentro)) : null;

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
                Notes = string.IsNullOrWhiteSpace(notas) ? null : notas,
                HasGeometry = gisGeometry != null,
                GeometryRepaired = gisReparada,
                CodCentro = codCentro
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
            else if (!gisValido)
            {
                rowDto.Status = "Error";
                rowDto.ValidationMessage = $"La geometría GIS no es válida: {gisError}.";
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

                // Avisos: la fila entra igual, pero se marca para que alguien la mire.
                var avisos = new List<string>();

                if (hasDateWarning)
                {
                    if (fechaDesde == null && fechaHasta == null)
                        avisos.Add("Fechas no especificadas: se asignó el rango general de la campaña.");
                    else if (fechaDesde == null)
                        avisos.Add("Fecha de inicio no especificada: se asignó automáticamente.");
                    else if (fechaHasta == null)
                        avisos.Add("Fecha de fin no especificada: se asignó automáticamente.");
                    else
                        avisos.Add("Inconsistencia en rango de fechas ajustada automáticamente.");
                }

                if (rowDto.GeometryRepaired)
                    avisos.Add("La geometría GIS venía mal formada (agujeros exportados como polígonos sueltos o anillos cruzados) y se normalizó: contrastá la superficie contra la declarada.");

                if (avisos.Count > 0)
                {
                    rowDto.Status = "Warning";
                    rowDto.ValidationMessage = string.Join(" ", avisos);
                    summary.WarningRows++;
                }
                else
                {
                    rowDto.Status = "Valid";
                    summary.ValidRows++;
                }

                if (rowDto.HasGeometry)
                    summary.GeometryRows++;
                if (rowDto.GeometryRepaired)
                    summary.RepairedGeometryRows++;

                if (codCentro.HasValue && seenCentros.Add(campoStr))
                    summary.FieldsWithCodCentro++;

                // Por lote, no por fila: un lote con dos cultivos ocupa dos filas y sus
                // hectareas son las mismas, no el doble.
                if (contadasPorLote.Add(lotKey))
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

        using var workbook = OpenWorkbookSafely(fileStream);
        var worksheet = FindDataWorksheet(workbook);
        var (headers, headerRow) = FindHeaderColumns(worksheet);

        // Load existing entities for current tenant
        var existingFields = await _context.Fields.ToListAsync(ct);
        var existingLots = await _context.Lots.ToListAsync(ct);
        var existingActivities = await _context.ErpActivities.ToListAsync(ct);
        if (!existingActivities.Any() && _context.CurrentTenantId != Guid.Empty)
        {
            existingActivities = await _context.ErpActivities
                .IgnoreQueryFilters()
                .Where(a => a.TenantId == Guid.Empty)
                .ToListAsync(ct);
        }
        var existingCampFields = await _context.CampaignFields.Where(cf => cf.CampaignId == campaignId).ToListAsync(ct);
        var existingCampLots = await _context.CampaignLots.Include(cl => cl.Rotations).Where(cl => cl.CampaignId == campaignId).ToListAsync(ct);

        var fieldMap = new Dictionary<string, Field>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in existingFields)
        {
            if (!string.IsNullOrWhiteSpace(f.Name))
                fieldMap[f.Name.Trim().ToLowerInvariant()] = f;
        }

        var lotMap = new Dictionary<(Guid FieldId, string Name), Lot>();
        foreach (var l in existingLots)
        {
            if (!string.IsNullOrWhiteSpace(l.Name))
                lotMap[(l.FieldId, l.Name.Trim().ToLowerInvariant())] = l;
        }

        var activityMap = new Dictionary<string, ErpActivity>(StringComparer.OrdinalIgnoreCase);
        foreach (var a in existingActivities)
        {
            if (!string.IsNullOrWhiteSpace(a.Name))
            {
                var key = a.Name.Trim().ToLowerInvariant();
                if (!activityMap.ContainsKey(key) || a.IsActive)
                    activityMap[key] = a;
            }
        }

        var campFieldMap = new Dictionary<Guid, CampaignField>();
        foreach (var cf in existingCampFields)
        {
            campFieldMap[cf.FieldId] = cf;
        }

        var campLotMap = new Dictionary<Guid, CampaignLot>();
        foreach (var cl in existingCampLots)
        {
            campLotMap[cl.LotId] = cl;
        }

        var result = new LotImportResultDto();
        var affectedCampFields = new HashSet<CampaignField>();
        var centrosAsignados = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var hectareasContadas = new HashSet<Guid>();
        var geometriasContadas = new HashSet<Guid>();

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

                    // La vista previa ya validó las geometrías; acá se vuelven a leer para calcular el
                    // lote. Si alguna no parsea (archivo cambiado entre preview y execute, o alguien
                    // pegando directo contra la API sin pasar por el preview) se importa el lote sin
                    // GIS en vez de abortar toda la importación, pero queda en el log: si no,
                    // desaparecían geometrías en silencio.
                    Geometry? gisGeometry = null;
                    if (headers.ColGis > 0)
                    {
                        var gisRaw = GetCellString(row.Cell(headers.ColGis));
                        if (!GeoJsonGeometryParser.TryParse(gisRaw, out gisGeometry, out var gisError))
                        {
                            gisGeometry = null;
                            _logger.LogWarning(
                                "Importación de lotes (campaña {CampaignId}, fila {Row}): el lote {Campo}/{Lote} se importa sin GIS porque la geometría no es utilizable: {Error}",
                                campaignId, r, campoStr, loteStr, gisError);
                        }
                    }

                    long? codCentro = headers.ColCentro > 0 ? ParseLongCell(row.Cell(headers.ColCentro)) : null;

                    // 1. Campo (Field)
                    string fieldKey = campoStr.Trim().ToLowerInvariant();
                    if (!fieldMap.TryGetValue(fieldKey, out var field))
                    {
                        field = new Field
                        {
                            Id = Guid.NewGuid(),
                            Name = campoStr.Trim(),
                            CreatedAt = DateTime.UtcNow,
                            CodCentro = codCentro
                        };
                        _context.Fields.Add(field);
                        fieldMap[fieldKey] = field;
                        result.FieldsCreated++;
                        if (codCentro.HasValue && centrosAsignados.Add(fieldKey))
                            result.CodCentrosAssigned++;
                    }
                    else if (codCentro.HasValue && field.CodCentro != codCentro)
                    {
                        // El centro se abre por campo, y la planilla es la fuente: si trae uno
                        // distinto al que estaba, manda el de la planilla (igual que el GIS).
                        field.CodCentro = codCentro;
                        if (centrosAsignados.Add(fieldKey))
                            result.CodCentrosAssigned++;
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
                            Status = "Active",
                            Geometry = gisGeometry
                        };
                        _context.Lots.Add(lot);
                        lotMap[(field.Id, lotKey)] = lot;
                        result.LotsCreated++;
                        if (gisGeometry != null && geometriasContadas.Add(lot.Id))
                            result.GeometriesImported++;
                    }
                    else
                    {
                        if (lot.CadastralArea == 0)
                            lot.CadastralArea = sup;
                        // Si la planilla trae GIS, manda sobre lo que hubiera: reimportar debe corregir.
                        if (gisGeometry != null)
                        {
                            lot.Geometry = gisGeometry;
                            if (geometriasContadas.Add(lot.Id))
                                result.GeometriesImported++;
                        }
                    }

                    // 4. CampaignLot
                    if (!campLotMap.TryGetValue(lot.Id, out var campLot))
                    {
                        campLot = new CampaignLot
                        {
                            Id = Guid.NewGuid(),
                            CampaignId = campaignId,
                            LotId = lot.Id,
                            ProductiveArea = sup,
                            Geometry = gisGeometry
                        };
                        _context.CampaignLots.Add(campLot);
                        campLotMap[lot.Id] = campLot;
                        result.CampaignLotsLinked++;
                    }
                    else
                    {
                        campLot.ProductiveArea = sup;
                        if (gisGeometry != null)
                            campLot.Geometry = gisGeometry;
                    }

                    // Por lote: un lote con dos cultivos son dos filas con la misma superficie.
                    if (hectareasContadas.Add(lot.Id))
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
                                TenantId = _context.CurrentTenantId,
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
                result.Message = $"Se procesaron con éxito los datos: {result.FieldsCreated} campos creados, {result.LotsCreated} lotes creados, {result.CampaignLotsLinked} lotes asociados a la campaña, {result.RotationsCreated} rotaciones registradas, {result.GeometriesImported} geometrías GIS importadas y {result.CodCentrosAssigned} campos con centro de costo asignado ({result.TotalHectares:N2} ha totales).";
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
            "Notas",
            "GIS (GeoJSON)",
            "Centro ERP"
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
        var ejemploSig = "{\"type\":\"Polygon\",\"coordinates\":[[[-58.51,-34.61],[-58.50,-34.61],[-58.50,-34.60],[-58.51,-34.60],[-58.51,-34.61]]]}";

        var examples = new (string Campo, string Lote, decimal Sup, string Cultivo, string Desde, string Hasta, string Notas, string Gis, string Centro)[]
        {
            ("Bassi-Prieto", "Bassi", 67.0m, "Maíz tardío", "2026-11-01", "2027-07-01", "", "", "10120000"),
            ("Bassi-Prieto", "Lobianco", 27.0m, "Maíz tardío", "2026-11-01", "2027-07-01", "", "", "10120000"),
            ("Breit", "Breit", 155.0m, "Girasol", "2026-09-01", "2027-05-01", "", "", "10050000"),
            ("Corral", "Kiko", 43.0m, "Trigo", "2026-04-01", "2026-12-25", "Lote principal", "", ""),
            ("La Casuarina", "30", 48.0m, "Maíz", "2026-09-01", "2027-05-01", "Superficie estimada", ejemploSig, "10160000")
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
            ws.Cell(r + 2, 8).Value = ex.Gis;
            ws.Cell(r + 2, 9).Value = ex.Centro;
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
            "6. La columna 'GIS (GeoJSON)' es opcional: si se completa con un polígono GeoJSON (o WKT), el lote se importa con su geometría. Si se deja vacía, el lote queda sin GIS.",
            "7. Si la geometría viene mal formada (agujeros exportados como polígonos sueltos, anillos cruzados), el sistema la normaliza y marca la fila como advertencia en la vista previa.",
            "8. La columna 'Centro ERP' es opcional y se aplica al CAMPO, no al lote: es la cuenta del plan de centros a la que imputan los pases G4 de sus labores. Alcanza con repetirla en cada fila del mismo campo.",
            "9. Esta importación se efectúa dentro de la campaña activa en la que te encuentres al momento de importar."
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
                else if (EsColumnaGeometria(text) && indices.ColGis == 0) indices.ColGis = c;
                else if (text.Contains("centro") && indices.ColCentro == 0) indices.ColCentro = c;
            }

            if (indices.ColCampo > 0 && indices.ColLote > 0 && indices.ColSuperficie > 0)
            {
                return (indices, r);
            }
        }

        throw new InvalidOperationException("No se encontraron los encabezados obligatorios ('Campo', 'Lote', 'Superficie') en la hoja.");
    }

    /// <summary>
    /// Detecta la columna con la geometría. La planilla AMSA trae "GIS (GeoJSON)" y además
    /// columnas de apoyo "GIS - Nombre origen" / "GIS - Superficie (ha)" que no son la geometría,
    /// por eso se excluyen explícitamente.
    /// </summary>
    private static bool EsColumnaGeometria(string headerLower)
    {
        if (headerLower.Contains("geojson") || headerLower.Contains("wkt") || headerLower.Contains("geometr"))
            return true;

        if (!headerLower.Contains("gis")) return false;

        return !(headerLower.Contains("nombre")
                 || headerLower.Contains("superficie")
                 || headerLower.Contains("origen")
                 || headerLower.Contains("area"));
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

    /// <summary>
    /// El centro de costo es una cuenta del ERP: llega como número o como texto según cómo
    /// haya quedado formateada la celda, y puede traer separadores de miles.
    /// </summary>
    private static long? ParseLongCell(IXLCell cell)
    {
        if (cell.IsEmpty()) return null;

        if (cell.DataType == XLDataType.Number)
        {
            var num = cell.GetDouble();
            if (Math.Abs(num) < 1 || num != Math.Floor(num)) return null;
            return (long)num;
        }

        var str = cell.GetString().Trim().Replace(".", "").Replace(",", "").Replace(" ", "");
        if (string.IsNullOrEmpty(str)) return null;

        return long.TryParse(str, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var val)
            ? val
            : null;
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
        public int ColGis { get; set; }
        public int ColCentro { get; set; }
    }

    private static XLWorkbook OpenWorkbookSafely(Stream fileStream)
    {
        var ms = new MemoryStream();
        fileStream.CopyTo(ms);
        ms.Position = 0;

        try
        {
            return new XLWorkbook(ms);
        }
        catch (Exception)
        {
            ms.Position = 0;
            using (var doc = DocumentFormat.OpenXml.Packaging.SpreadsheetDocument.Open(ms, true))
            {
                var wbPart = doc.WorkbookPart;
                if (wbPart != null)
                {
                    var pivotCacheParts = wbPart.PivotTableCacheDefinitionParts.ToList();
                    foreach (var p in pivotCacheParts)
                    {
                        wbPart.DeletePart(p);
                    }

                    foreach (var wsPart in wbPart.WorksheetParts)
                    {
                        var ptParts = wsPart.PivotTableParts.ToList();
                        foreach (var pt in ptParts)
                        {
                            wsPart.DeletePart(pt);
                        }
                    }
                    wbPart.Workbook.Save();
                }
            }

            ms.Position = 0;
            return new XLWorkbook(ms);
        }
    }
}

