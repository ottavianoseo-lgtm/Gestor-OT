using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using GestorOT.Application.Interfaces;
using GestorOT.Domain.Entities;
using GestorOT.Domain.Enums;
using GestorOT.Shared.Dtos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GestorOT.Infrastructure.Services;

public class LaborExcelImportService : ILaborExcelImportService
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<LaborExcelImportService> _logger;

    public LaborExcelImportService(
        IApplicationDbContext context,
        ILogger<LaborExcelImportService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<LaborImportPreviewDto> PreviewAsync(Guid campaignId, Stream fileStream, CancellationToken ct = default)
    {
        var campaign = await _context.Campaigns.FirstOrDefaultAsync(c => c.Id == campaignId, ct);
        if (campaign == null)
            throw new InvalidOperationException("La campaña especificada no existe.");

        using var workbook = OpenWorkbookSafely(fileStream);
        var worksheet = FindLaborWorksheet(workbook);
        var columnConfig = DetectColumns(worksheet);

        // Fetch campaign lots and available inventory & aliases
        var campaignLots = await _context.CampaignLots
            .Include(cl => cl.Lot)
            .ThenInclude(l => l!.Field)
            .Where(cl => cl.CampaignId == campaignId)
            .AsNoTracking()
            .ToListAsync(ct);

        var existingAliases = await _context.SupplyAliases
            .Include(a => a.Supply)
            .AsNoTracking()
            .ToListAsync(ct);

        var existingInventories = await _context.Inventories
            .AsNoTracking()
            .ToListAsync(ct);

        var existingLaborTypes = await _context.LaborTypes
            .AsNoTracking()
            .ToListAsync(ct);

        var existingContacts = await _context.Contacts
            .AsNoTracking()
            .ToListAsync(ct);

        // Parse grouped labors and supplies
        var parsedLabors = ParseLaborsFromWorksheet(worksheet, columnConfig, campaignLots, existingLaborTypes, existingContacts);

        // Match supplies
        var (supplyMappings, unmatchedCount) = BuildSupplyMappings(parsedLabors, existingAliases, existingInventories);

        // Match labor types
        var (laborTypeMappings, unmatchedLaborTypesCount) = BuildLaborTypeMappings(parsedLabors, existingLaborTypes);

        // Link matched supplies back to parsed items for preview
        var mappingDict = new Dictionary<string, LaborImportSupplyMappingDto>(StringComparer.OrdinalIgnoreCase);
        foreach (var m in supplyMappings)
        {
            mappingDict[m.RawName.Trim()] = m;
        }
        foreach (var labor in parsedLabors)
        {
            foreach (var sup in labor.Supplies)
            {
                if (mappingDict.TryGetValue(sup.SupplyName.Trim(), out var map))
                {
                    sup.MatchedSupplyId = map.MatchedSupplyId;
                    sup.MatchedSupplyName = map.MatchedSupplyName;
                }
            }
        }

        // Link matched labor types back to parsed items for preview
        var laborTypeMappingDict = new Dictionary<string, LaborImportTypeMappingDto>(StringComparer.OrdinalIgnoreCase);
        foreach (var ltm in laborTypeMappings)
        {
            laborTypeMappingDict[ltm.RawName.Trim()] = ltm;
        }
        foreach (var labor in parsedLabors)
        {
            if (laborTypeMappingDict.TryGetValue(labor.LaborTypeName.Trim(), out var ltm) && ltm.MatchedLaborTypeId.HasValue)
            {
                labor.LaborTypeId = ltm.MatchedLaborTypeId;
                labor.Warnings.Clear(); // Cleared if matched
            }
        }

        var diagnostics = new List<string>();
        int unknownLots = parsedLabors.Count(l => !l.LotId.HasValue);
        if (unknownLots > 0)
        {
            diagnostics.Add($"Hay {unknownLots} labores con lotes que no se encontraron en la campaña activa.");
        }

        if (unmatchedCount > 0)
        {
            diagnostics.Add($"Se detectaron {unmatchedCount} insumos sin coincidencia directa en inventario. Podés mapearlos o darlos de alta en la pestaña de Reconciliación de Insumos.");
        }

        if (unmatchedLaborTypesCount > 0)
        {
            diagnostics.Add($"Se detectaron {unmatchedLaborTypesCount} tipos de labor sin coincidencia directa en el catálogo. Podés asignarlos o darlos de alta en la pestaña de Reconciliación de Labores.");
        }

        decimal totalHectares = parsedLabors.Sum(l => l.Hectares);
        int totalSupplies = parsedLabors.Sum(l => l.Supplies.Count);

        return new LaborImportPreviewDto
        {
            TotalLabors = parsedLabors.Count,
            TotalSupplies = totalSupplies,
            TotalHectares = totalHectares,
            UniqueSuppliesCount = supplyMappings.Count,
            UnmatchedSuppliesCount = unmatchedCount,
            UniqueLaborTypesCount = laborTypeMappings.Count,
            UnmatchedLaborTypesCount = unmatchedLaborTypesCount,
            Labors = parsedLabors,
            SupplyMappings = supplyMappings,
            LaborTypeMappings = laborTypeMappings,
            Diagnostics = diagnostics,
            CanProceed = parsedLabors.Count > 0
        };
    }

    public async Task<LaborImportResultDto> ExecuteAsync(
        Guid campaignId,
        Stream fileStream,
        List<LaborImportSupplyMappingDto> mappings,
        List<LaborImportTypeMappingDto>? laborTypeMappings = null,
        CancellationToken ct = default)
    {
        var campaign = await _context.Campaigns.FirstOrDefaultAsync(c => c.Id == campaignId, ct);
        if (campaign == null)
            throw new InvalidOperationException("La campaña especificada no existe.");

        using var workbook = OpenWorkbookSafely(fileStream);
        var worksheet = FindLaborWorksheet(workbook);
        var columnConfig = DetectColumns(worksheet);

        var campaignLots = await _context.CampaignLots
            .Include(cl => cl.Lot)
            .Where(cl => cl.CampaignId == campaignId)
            .ToListAsync(ct);

        var existingLaborTypes = await _context.LaborTypes.ToListAsync(ct);
        var existingAliases = await _context.SupplyAliases.ToListAsync(ct);
        var existingInventories = await _context.Inventories.ToListAsync(ct);
        var existingContacts = await _context.Contacts.ToListAsync(ct);

        var parsedLabors = ParseLaborsFromWorksheet(worksheet, columnConfig, campaignLots, existingLaborTypes, existingContacts);

        int laborsCreated = 0;
        int suppliesCreated = 0;
        int newInventoriesCreated = 0;
        int newLaborTypesCreated = 0;
        int aliasesLearned = 0;
        var errors = new List<string>();

        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            var isRelational = _context.Database.IsRelational();
            Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? tx = isRelational ? await _context.Database.BeginTransactionAsync(ct) : null;
            try
            {
                // 1. Process Supply Mappings (Creations and Aliases)
                var resolvedSupplies = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

                foreach (var map in mappings)
                {
                    if (string.Equals(map.Action, "Ignore", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    Guid? targetSupplyId = map.MatchedSupplyId;

                    if (string.Equals(map.Action, "CreateNew", StringComparison.OrdinalIgnoreCase) || !targetSupplyId.HasValue)
                    {
                        string itemName = !string.IsNullOrWhiteSpace(map.NewItemName) ? map.NewItemName.Trim() : map.RawName.Trim();
                        string category = !string.IsNullOrWhiteSpace(map.NewCategory) ? map.NewCategory.Trim() : (!string.IsNullOrWhiteSpace(map.DetectedCategory) ? map.DetectedCategory.Trim() : "Insumos");
                        string unit = !string.IsNullOrWhiteSpace(map.NewUnit) ? map.NewUnit.Trim() : (!string.IsNullOrWhiteSpace(map.DetectedUnit) ? map.DetectedUnit.Trim() : "unidad");

                        var newInventory = new Inventory
                        {
                            Id = Guid.NewGuid(),
                            TenantId = _context.CurrentTenantId,
                            ItemName = itemName,
                            Category = category,
                            Unit = unit,
                            UnitA = unit,
                            CurrentStock = 0,
                            ReorderLevel = 0,
                            ConversionFactor = 1
                        };
                        _context.Inventories.Add(newInventory);
                        targetSupplyId = newInventory.Id;
                        newInventoriesCreated++;
                    }

                    if (targetSupplyId.HasValue)
                    {
                        resolvedSupplies[map.RawName.Trim()] = targetSupplyId.Value;

                        // Check if alias already exists for this tenant
                        string normRaw = NormalizeString(map.RawName);
                        bool aliasExists = existingAliases.Any(a => string.Equals(a.NormalizedName, normRaw, StringComparison.OrdinalIgnoreCase));
                        if (!aliasExists)
                        {
                            var newAlias = new SupplyAlias
                            {
                                Id = Guid.NewGuid(),
                                TenantId = _context.CurrentTenantId,
                                RawName = map.RawName.Trim(),
                                NormalizedName = normRaw,
                                SupplyId = targetSupplyId.Value,
                                CreatedAt = DateTime.UtcNow
                            };
                            _context.SupplyAliases.Add(newAlias);
                            existingAliases.Add(newAlias);
                            aliasesLearned++;
                        }
                    }
                }

                // 2. Process Labor Type Mappings
                var laborTypesByName = new Dictionary<string, LaborType>(StringComparer.OrdinalIgnoreCase);
                foreach (var lt in existingLaborTypes)
                {
                    laborTypesByName[NormalizeString(lt.Name)] = lt;
                }

                var resolvedLaborTypes = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
                if (laborTypeMappings != null)
                {
                    foreach (var ltm in laborTypeMappings)
                    {
                        if (string.Equals(ltm.Action, "Match", StringComparison.OrdinalIgnoreCase) && ltm.MatchedLaborTypeId.HasValue)
                        {
                            resolvedLaborTypes[ltm.RawName.Trim()] = ltm.MatchedLaborTypeId.Value;
                        }
                        else if (string.Equals(ltm.Action, "CreateNew", StringComparison.OrdinalIgnoreCase) || !ltm.MatchedLaborTypeId.HasValue)
                        {
                            string typeName = !string.IsNullOrWhiteSpace(ltm.NewTypeName) ? ltm.NewTypeName.Trim() : ltm.RawName.Trim();
                            string normName = NormalizeString(typeName);
                            if (laborTypesByName.TryGetValue(normName, out var existingLt))
                            {
                                resolvedLaborTypes[ltm.RawName.Trim()] = existingLt.Id;
                            }
                            else
                            {
                                var newLt = new LaborType
                                {
                                    Id = Guid.NewGuid(),
                                    TenantId = _context.CurrentTenantId,
                                    Name = typeName,
                                    Description = "Creado desde importación Excel"
                                };
                                _context.LaborTypes.Add(newLt);
                                laborTypesByName[normName] = newLt;
                                resolvedLaborTypes[ltm.RawName.Trim()] = newLt.Id;
                                newLaborTypesCreated++;
                            }
                        }
                    }
                }

                // 3. Process Labors & Supplies
                foreach (var parsedLabor in parsedLabors)
                {
                    if (!parsedLabor.LotId.HasValue || !parsedLabor.CampaignLotId.HasValue)
                    {
                        errors.Add($"Labor en fila {parsedLabor.RowIndex}: El lote '{parsedLabor.LotName}' no pertenece a la campaña. Se omitió.");
                        continue;
                    }

                    // Resolve LaborType
                    string laborTypeName = !string.IsNullOrWhiteSpace(parsedLabor.LaborTypeName) ? parsedLabor.LaborTypeName.Trim() : "Labor General";
                    Guid targetLaborTypeId;

                    if (resolvedLaborTypes.TryGetValue(laborTypeName, out var mappedLtId))
                    {
                        targetLaborTypeId = mappedLtId;
                    }
                    else
                    {
                        string normLt = NormalizeString(laborTypeName);
                        if (laborTypesByName.TryGetValue(normLt, out var laborType))
                        {
                            targetLaborTypeId = laborType.Id;
                        }
                        else
                        {
                            var laborTypeNew = new LaborType
                            {
                                Id = Guid.NewGuid(),
                                TenantId = _context.CurrentTenantId,
                                Name = laborTypeName,
                                Description = "Creado automáticamente desde importación Excel"
                            };
                            _context.LaborTypes.Add(laborTypeNew);
                            laborTypesByName[normLt] = laborTypeNew;
                            targetLaborTypeId = laborTypeNew.Id;
                            newLaborTypesCreated++;
                        }
                    }

                    var campaignLot = campaignLots.FirstOrDefault(cl => cl.Id == parsedLabor.CampaignLotId.Value);

                    // Deducir modo por la fecha: si es pasada o igual a hoy, es Realizada; si es a futuro, Planeada
                    bool isRealized = false;
                    if (parsedLabor.Date.HasValue)
                    {
                        isRealized = parsedLabor.Date.Value.Date <= DateTime.UtcNow.Date;
                    }
                    else
                    {
                        isRealized = string.Equals(parsedLabor.Mode, "Realized", StringComparison.OrdinalIgnoreCase);
                    }

                    var laborMode = isRealized ? LaborMode.Realized : LaborMode.Planned;
                    var laborStatus = isRealized ? LaborStatus.Realized : LaborStatus.Planned;

                    var labor = new Labor
                    {
                        Id = Guid.NewGuid(),
                        TenantId = _context.CurrentTenantId,
                        LotId = parsedLabor.LotId.Value,
                        CampaignLotId = parsedLabor.CampaignLotId.Value,
                        ErpActivityId = campaignLot?.CropId, // Inherit Crop/Activity from campaign lot
                        LaborTypeId = targetLaborTypeId,
                        ContactId = parsedLabor.ContactId,
                        IsExternalBilling = parsedLabor.IsExternalBilling,
                        ExecutionDate = parsedLabor.Date,
                        EstimatedDate = parsedLabor.Date,
                        Hectares = parsedLabor.Hectares,
                        EffectiveArea = parsedLabor.Hectares,
                        Rate = 1,
                        RateUnit = "ha",
                        PlannedDose = 1,
                        RealizedDose = isRealized ? 1 : null,
                        Mode = laborMode,
                        Status = laborStatus,
                        Priority = LaborPriority.Regular,
                        CreatedAt = DateTime.UtcNow,
                        Notes = string.IsNullOrWhiteSpace(parsedLabor.Contractor) 
                            ? "Importado desde Excel" 
                            : $"Importado desde Excel. Contratista/Equipo: {parsedLabor.Contractor}"
                    };

                    int mixOrder = 1;
                    foreach (var sup in parsedLabor.Supplies)
                    {
                        if (!resolvedSupplies.TryGetValue(sup.SupplyName.Trim(), out var supplyId))
                        {
                            continue; // Ignored or unmapped
                        }

                        decimal plannedDose = sup.Dose;
                        decimal totalQty = sup.Total ?? (sup.Dose * labor.Hectares);

                        var laborSupply = new LaborSupply
                        {
                            Id = Guid.NewGuid(),
                            TenantId = _context.CurrentTenantId,
                            LaborId = labor.Id,
                            SupplyId = supplyId,
                            PlannedHectares = labor.Hectares,
                            RealHectares = isRealized ? labor.Hectares : null,
                            PlannedDose = plannedDose,
                            RealDose = isRealized ? plannedDose : null,
                            PlannedTotal = totalQty,
                            RealTotal = isRealized ? totalQty : null,
                            UnitOfMeasure = !string.IsNullOrWhiteSpace(sup.Unit) ? sup.Unit : "unidad",
                            TankMixOrder = mixOrder++
                        };

                        labor.Supplies.Add(laborSupply);
                        suppliesCreated++;
                    }

                    _context.Labors.Add(labor);
                    laborsCreated++;
                }

                await _context.SaveChangesAsync(ct);
                if (tx != null) await tx.CommitAsync(ct);
            }
            catch (Exception ex)
            {
                if (tx != null) await tx.RollbackAsync(ct);
                _logger.LogError(ex, "Error durante la ejecución transaccional de importación de labores.");
                throw;
            }
            finally
            {
                if (tx != null) await tx.DisposeAsync();
            }
        });

        return new LaborImportResultDto
        {
            LaborsCreated = laborsCreated,
            SuppliesCreated = suppliesCreated,
            NewSuppliesCreated = newInventoriesCreated,
            NewLaborTypesCreated = newLaborTypesCreated,
            AliasesLearned = aliasesLearned,
            Errors = errors,
            Success = laborsCreated > 0
        };
    }

    public Task<(byte[] Bytes, string FileName)> GenerateTemplateAsync(CancellationToken ct = default)
    {
        using var wb = new XLWorkbook();

        // Sheet 1: Template
        var ws = wb.Worksheets.Add("Labores e Insumos");

        // Headers
        var headers = new[]
        {
            "Fecha",
            "Establecimiento",
            "Lote",
            "Superficie (ha)",
            "Tipo",
            "Labor o Insumo",
            "Dosis",
            "Unidad",
            "Contratista / Maquinaria",
            "Modo",
            "Notas"
        };

        for (int i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(1, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = XLColor.FromArgb(27, 67, 50); // Forest green
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        // Sample Data: Labor 1 (Pulverización with 2 supplies)
        var sampleRows = new object[][]
        {
            new object[] { "2026-10-15", "La Juanita", "Lote 1", 100, "Labor", "Pulverización", 1, "ha", "Propio", "Realizada", "Barbecho químico" },
            new object[] { "2026-10-15", "La Juanita", "Lote 1", 100, "Herbicida", "Glifosato 66%", 2.5, "litros", "Propio", "Realizada", "" },
            new object[] { "2026-10-15", "La Juanita", "Lote 1", 100, "Coadyuvante", "Aceite Metilado", 0.5, "litros", "Propio", "Realizada", "" },
            new object[] { "2026-10-20", "La Juanita", "Lote 2", 80, "Labor", "Fertilización", 1, "ha", "Serv. Agro", "Realizada", "Aplicación voleo" },
            new object[] { "2026-10-20", "La Juanita", "Lote 2", 80, "Fertilizante", "Urea Granulada", 150, "kg", "Serv. Agro", "Realizada", "" },
            new object[] { "2026-11-05", "El Alba", "Lote Norte", 65, "Labor", "Siembra", 1, "ha", "Contratista Don Carlos", "Planeada", "Siembra 1ra" },
            new object[] { "2026-11-05", "El Alba", "Lote Norte", 65, "Semilla", "Soja DM 46i20", 70, "kg", "Contratista Don Carlos", "Planeada", "" }
        };

        for (int r = 0; r < sampleRows.Length; r++)
        {
            for (int c = 0; c < sampleRows[r].Length; c++)
            {
                var cell = ws.Cell(r + 2, c + 1);
                var val = sampleRows[r][c];

                if (val is int intVal) cell.Value = intVal;
                else if (val is double dblVal) cell.Value = dblVal;
                else cell.Value = val.ToString();

                // Highlight labor rows lightly
                if (sampleRows[r][4]?.ToString() == "Labor")
                {
                    cell.Style.Fill.BackgroundColor = XLColor.FromArgb(235, 247, 238);
                }
            }
        }

        ws.Columns().AdjustToContents(10, 35);

        // Sheet 2: Instructions
        var wsInst = wb.Worksheets.Add("Instrucciones");
        wsInst.Cell(1, 1).Value = "GUÍA DE IMPORTACIÓN DE LABORES E INSUMOS";
        wsInst.Cell(1, 1).Style.Font.Bold = true;
        wsInst.Cell(1, 1).Style.Font.FontSize = 14;

        var instructions = new[]
        {
            "1. Agrupamiento por labor:",
            "   - Una fila con Tipo = 'Labor' define el inicio de una labor (ej: Pulverización, Fertilización, Siembra).",
            "   - Las filas siguientes con Tipo distinto de 'Labor' (ej: Herbicida, Fertilizante, Coadyuvante, Semilla)",
            "     representan los insumos aplicados en dicha labor, hasta que se encuentre otra fila con Tipo = 'Labor'.",
            "",
            "2. Identificación de Lotes:",
            "   - Los lotes deben existir previamente en la campaña activa (los podés importar previamente con la Importación de Lotes).",
            "   - La labor heredará automáticamente la imputación de cultivo y rotación de ese lote en la campaña.",
            "",
            "3. Conciliación de Insumos:",
            "   - En el paso de vista previa podrás revisar y corregir qué producto de tu inventario corresponde a cada insumo.",
            "   - El sistema recordará tus elecciones automáticamente para futuras importaciones mediante Alias.",
            "",
            "4. Modos:",
            "   - 'Realizada' o 'r': Se guarda como labor ejecutada.",
            "   - 'Planeada' o 'p': Se guarda como labor en planificación."
        };

        for (int i = 0; i < instructions.Length; i++)
        {
            wsInst.Cell(i + 3, 1).Value = instructions[i];
        }
        wsInst.Column(1).Width = 100;

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return Task.FromResult((ms.ToArray(), "Template_Importacion_Labores_GestorOT.xlsx"));
    }

    #region Helper Classes & Parsing

    private record ColumnConfig
    {
        public bool IsAmsaFormat { get; init; }
        public int HeaderRow { get; init; }
        public int ColFecha { get; init; }
        public int ColEstablecimiento { get; init; }
        public int ColLote { get; init; }
        public int ColSuperficie { get; init; }
        public int ColProducLabor { get; init; }
        public int ColDosis { get; init; }
        public int ColTipo { get; init; }
        public int ColUnidad { get; init; }
        public int ColTotal { get; init; }
        public int ColContratista { get; init; }
        public int ColModo { get; init; }
    }

    private IXLWorksheet FindLaborWorksheet(XLWorkbook workbook)
    {
        // Check for AMSA "Planilla Datos"
        var amsaSheet = workbook.Worksheets.FirstOrDefault(w => 
            string.Equals(w.Name.Trim(), "Planilla Datos", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(w.Name.Trim(), "Datos", StringComparison.OrdinalIgnoreCase));
        if (amsaSheet != null) return amsaSheet;

        // Check for standard sheet name
        var stdSheet = workbook.Worksheets.FirstOrDefault(w =>
            w.Name.Contains("Labor", StringComparison.OrdinalIgnoreCase) ||
            w.Name.Contains("Importacion", StringComparison.OrdinalIgnoreCase));
        if (stdSheet != null) return stdSheet;

        return workbook.Worksheets.First();
    }

    private ColumnConfig DetectColumns(IXLWorksheet ws)
    {
        // Scan first 10 rows for headers
        for (int r = 1; r <= 10; r++)
        {
            var row = ws.Row(r);
            int lastCol = row.LastCellUsed()?.Address.ColumnNumber ?? 0;
            if (lastCol < 4) continue;

            int colFecha = 0, colEst = 0, colLote = 0, colSup = 0, colItem = 0, colDosis = 0, colTipo = 0, colUnidad = 0, colTotal = 0, colContr = 0, colModo = 0;
            bool hasProducLabor = false;

            for (int c = 1; c <= lastCol; c++)
            {
                string header = row.Cell(c).GetString().Trim().ToLowerInvariant();
                if (string.IsNullOrWhiteSpace(header)) continue;

                if (header.Contains("produc/labor") || header.Contains("produc / labor") || header.Contains("labor o insumo"))
                {
                    colItem = c;
                    hasProducLabor = true;
                }
                else if (header == "tipo") colTipo = c;
                else if (header == "fecha" || header.StartsWith("fecha")) colFecha = c;
                else if (header == "lote") colLote = c;
                else if (header == "establecimiento" || header == "campo") colEst = c;
                else if (header == "sup" || header.StartsWith("superficie")) colSup = c;
                else if (header == "dosis") colDosis = c;
                else if (header == "unidad") colUnidad = c;
                else if (header == "total") colTotal = c;
                else if (header.Contains("contr/prove") || header.Contains("contratista") || header.Contains("maquinaria")) colContr = c;
                else if (header.Contains("real/presup") || header == "modo" || header == "estado") colModo = c;
            }

            if (colItem > 0 && (colTipo > 0 || colLote > 0))
            {
                return new ColumnConfig
                {
                    IsAmsaFormat = hasProducLabor && colTotal > 0,
                    HeaderRow = r,
                    ColFecha = colFecha,
                    ColEstablecimiento = colEst,
                    ColLote = colLote,
                    ColSuperficie = colSup,
                    ColProducLabor = colItem,
                    ColDosis = colDosis,
                    ColTipo = colTipo,
                    ColUnidad = colUnidad,
                    ColTotal = colTotal,
                    ColContratista = colContr,
                    ColModo = colModo
                };
            }
        }

        // Fallback to standard 1..10 positions
        return new ColumnConfig
        {
            IsAmsaFormat = false,
            HeaderRow = 1,
            ColFecha = 1,
            ColEstablecimiento = 2,
            ColLote = 3,
            ColSuperficie = 4,
            ColTipo = 5,
            ColProducLabor = 6,
            ColDosis = 7,
            ColUnidad = 8,
            ColContratista = 9,
            ColModo = 10
        };
    }

    private List<LaborImportParsedLaborDto> ParseLaborsFromWorksheet(
        IXLWorksheet ws,
        ColumnConfig cfg,
        List<CampaignLot> campaignLots,
        List<LaborType> laborTypes,
        List<Contact> contacts)
    {
        var result = new List<LaborImportParsedLaborDto>();
        LaborImportParsedLaborDto? currentLabor = null;

        // Build lot lookup dictionaries
        var lotByNormalizedName = new Dictionary<string, CampaignLot>(StringComparer.OrdinalIgnoreCase);
        foreach (var cl in campaignLots)
        {
            if (cl.Lot != null)
            {
                string normName = NormalizeString(cl.Lot.Name);
                lotByNormalizedName[normName] = cl;
            }
        }

        var laborTypesByName = new Dictionary<string, LaborType>(StringComparer.OrdinalIgnoreCase);
        foreach (var lt in laborTypes)
        {
            laborTypesByName[NormalizeString(lt.Name)] = lt;
        }

        int lastRow = ws.LastRowUsed()?.RowNumber() ?? cfg.HeaderRow;

        for (int r = cfg.HeaderRow + 1; r <= lastRow; r++)
        {
            var row = ws.Row(r);
            if (row.IsEmpty()) continue;

            string tipo = cfg.ColTipo > 0 ? row.Cell(cfg.ColTipo).GetString().Trim() : string.Empty;
            string producLabor = cfg.ColProducLabor > 0 ? row.Cell(cfg.ColProducLabor).GetString().Trim() : string.Empty;

            if (string.IsNullOrWhiteSpace(producLabor) && string.IsNullOrWhiteSpace(tipo))
                continue;

            bool isLaborRow = tipo.Contains("Labor", StringComparison.OrdinalIgnoreCase) ||
                             tipo.Contains("Siembra", StringComparison.OrdinalIgnoreCase) ||
                             tipo.Contains("Cosecha", StringComparison.OrdinalIgnoreCase);

            if (isLaborRow)
            {
                if (currentLabor != null)
                {
                    result.Add(currentLabor);
                }

                // Extract Labor fields
                DateTime? date = cfg.ColFecha > 0 ? ParseDateCell(row.Cell(cfg.ColFecha)) : null;
                string fieldName = cfg.ColEstablecimiento > 0 ? row.Cell(cfg.ColEstablecimiento).GetString().Trim() : string.Empty;
                string lotName = cfg.ColLote > 0 ? row.Cell(cfg.ColLote).GetString().Trim() : string.Empty;
                decimal sup = cfg.ColSuperficie > 0 ? ParseDecimalCell(row.Cell(cfg.ColSuperficie)) : 0;
                string contractor = cfg.ColContratista > 0 ? row.Cell(cfg.ColContratista).GetString().Trim() : string.Empty;
                string modoStr = cfg.ColModo > 0 ? row.Cell(cfg.ColModo).GetString().Trim() : string.Empty;

                // Deducir modo por la fecha: si es pasada o igual a hoy, es Realizada; si es a futuro, Planeada
                bool isRealized = false;
                if (date.HasValue)
                {
                    isRealized = date.Value.Date <= DateTime.UtcNow.Date;
                }
                else if (!string.IsNullOrWhiteSpace(modoStr))
                {
                    isRealized = string.Equals(modoStr, "r", StringComparison.OrdinalIgnoreCase) ||
                                 modoStr.StartsWith("realiz", StringComparison.OrdinalIgnoreCase);
                }
                else
                {
                    isRealized = true;
                }

                string mode = isRealized ? "Realized" : "Planned";

                // Match Lot in campaign
                Guid? lotId = null;
                Guid? campaignLotId = null;
                var errors = new List<string>();

                if (!string.IsNullOrWhiteSpace(lotName))
                {
                    string normLot = NormalizeString(lotName);
                    if (lotByNormalizedName.TryGetValue(normLot, out var cl))
                    {
                        lotId = cl.LotId;
                        campaignLotId = cl.Id;
                    }
                    else
                    {
                        errors.Add($"Lote '{lotName}' no encontrado en la campaña activa.");
                    }
                }
                else
                {
                    errors.Add("Fila sin nombre de lote.");
                }

                // Match LaborType
                Guid? laborTypeId = null;
                if (laborTypesByName.TryGetValue(NormalizeString(producLabor), out var lt))
                {
                    laborTypeId = lt.Id;
                }

                var (contactId, matchedContactName, isExternalBilling) = MatchContact(contractor, contacts);

                currentLabor = new LaborImportParsedLaborDto
                {
                    RowIndex = r,
                    Date = date,
                    FieldName = fieldName,
                    LotName = lotName,
                    LotId = lotId,
                    CampaignLotId = campaignLotId,
                    Hectares = sup,
                    LaborTypeName = producLabor,
                    LaborTypeId = laborTypeId,
                    Contractor = contractor,
                    ContactId = contactId,
                    MatchedContactName = matchedContactName,
                    IsExternalBilling = isExternalBilling,
                    Mode = mode,
                    Status = mode,
                    Supplies = new List<LaborImportParsedItemDto>(),
                    Errors = errors
                };
            }
            else
            {
                // This is a Supply row
                if (currentLabor == null)
                {
                    // Orphaned supply line, try to synthesize a labor if lot is present
                    string orphanLot = cfg.ColLote > 0 ? row.Cell(cfg.ColLote).GetString().Trim() : string.Empty;
                    DateTime? orphanDate = cfg.ColFecha > 0 ? ParseDateCell(row.Cell(cfg.ColFecha)) : null;
                    bool orphanRealized = !orphanDate.HasValue || orphanDate.Value.Date <= DateTime.UtcNow.Date;
                    string orphanMode = orphanRealized ? "Realized" : "Planned";

                    string orphanContractor = cfg.ColContratista > 0 ? row.Cell(cfg.ColContratista).GetString().Trim() : string.Empty;
                    var (orphanContactId, orphanMatchedName, orphanExternal) = MatchContact(orphanContractor, contacts);

                    currentLabor = new LaborImportParsedLaborDto
                    {
                        RowIndex = r,
                        Date = orphanDate,
                        FieldName = cfg.ColEstablecimiento > 0 ? row.Cell(cfg.ColEstablecimiento).GetString().Trim() : string.Empty,
                        LotName = orphanLot,
                        Hectares = cfg.ColSuperficie > 0 ? ParseDecimalCell(row.Cell(cfg.ColSuperficie)) : 0,
                        LaborTypeName = "Labor General",
                        Contractor = orphanContractor,
                        ContactId = orphanContactId,
                        MatchedContactName = orphanMatchedName,
                        IsExternalBilling = orphanExternal,
                        Mode = orphanMode,
                        Status = orphanMode,
                        Supplies = new List<LaborImportParsedItemDto>()
                    };
                }

                decimal dose = cfg.ColDosis > 0 ? ParseDecimalCell(row.Cell(cfg.ColDosis)) : 0;
                string unit = cfg.ColUnidad > 0 ? row.Cell(cfg.ColUnidad).GetString().Trim() : string.Empty;
                decimal? total = cfg.ColTotal > 0 ? ParseDecimalCell(row.Cell(cfg.ColTotal)) : null;

                currentLabor.Supplies.Add(new LaborImportParsedItemDto
                {
                    SupplyName = producLabor,
                    Dose = dose,
                    Unit = unit,
                    Total = total > 0 ? total : null,
                    Category = tipo
                });
            }
        }

        if (currentLabor != null)
        {
            result.Add(currentLabor);
        }

        return result;
    }

    private (List<LaborImportSupplyMappingDto> Mappings, int UnmatchedCount) BuildSupplyMappings(
        List<LaborImportParsedLaborDto> labors,
        List<SupplyAlias> aliases,
        List<Inventory> inventories)
    {
        // Extract distinct supplies
        var suppliesGrouped = labors
            .SelectMany(l => l.Supplies)
            .Where(s => !string.IsNullOrWhiteSpace(s.SupplyName))
            .GroupBy(s => s.SupplyName.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToList();

        var aliasesByNorm = new Dictionary<string, SupplyAlias>(StringComparer.OrdinalIgnoreCase);
        foreach (var a in aliases)
        {
            if (!string.IsNullOrWhiteSpace(a.NormalizedName))
                aliasesByNorm[a.NormalizedName] = a;
        }

        var invByNorm = new Dictionary<string, Inventory>(StringComparer.OrdinalIgnoreCase);
        foreach (var i in inventories)
        {
            string norm = NormalizeString(i.ItemName);
            if (!string.IsNullOrWhiteSpace(norm))
                invByNorm[norm] = i;
        }

        var result = new List<LaborImportSupplyMappingDto>();
        int unmatchedCount = 0;

        foreach (var group in suppliesGrouped)
        {
            string rawName = group.Key;
            string normRaw = NormalizeString(rawName);
            var firstItem = group.First();

            int occurrences = group.Count();
            decimal avgDose = occurrences > 0 ? Math.Round(group.Average(g => g.Dose), 3) : 0;

            // Tier 1: Check SupplyAlias (100% confidence)
            if (aliasesByNorm.TryGetValue(normRaw, out var alias) && alias.Supply != null)
            {
                result.Add(new LaborImportSupplyMappingDto
                {
                    RawName = rawName,
                    NormalizedName = normRaw,
                    MatchedSupplyId = alias.SupplyId,
                    MatchedSupplyName = alias.Supply.ItemName,
                    Confidence = 1.0,
                    ConfidenceLevel = "High",
                    IsFromAlias = true,
                    DetectedCategory = firstItem.Category,
                    DetectedUnit = firstItem.Unit,
                    Occurrences = occurrences,
                    AverageDose = avgDose,
                    Action = "Match"
                });
                continue;
            }

            // Tier 2: Check exact Inventory name
            if (invByNorm.TryGetValue(normRaw, out var exactInv))
            {
                result.Add(new LaborImportSupplyMappingDto
                {
                    RawName = rawName,
                    NormalizedName = normRaw,
                    MatchedSupplyId = exactInv.Id,
                    MatchedSupplyName = exactInv.ItemName,
                    Confidence = 1.0,
                    ConfidenceLevel = "High",
                    IsFromAlias = false,
                    DetectedCategory = firstItem.Category,
                    DetectedUnit = firstItem.Unit,
                    Occurrences = occurrences,
                    AverageDose = avgDose,
                    Action = "Match"
                });
                continue;
            }

            // Tier 3: Fuzzy similarity matching
            double bestScore = 0;
            Inventory? bestInv = null;

            foreach (var inv in inventories)
            {
                double score = CalculateSimilarity(normRaw, NormalizeString(inv.ItemName));
                if (score > bestScore)
                {
                    bestScore = score;
                    bestInv = inv;
                }
            }

            if (bestScore >= 0.70 && bestInv != null)
            {
                result.Add(new LaborImportSupplyMappingDto
                {
                    RawName = rawName,
                    NormalizedName = normRaw,
                    MatchedSupplyId = bestInv.Id,
                    MatchedSupplyName = bestInv.ItemName,
                    Confidence = Math.Round(bestScore, 2),
                    ConfidenceLevel = bestScore >= 0.85 ? "High" : "Medium",
                    IsFromAlias = false,
                    DetectedCategory = firstItem.Category,
                    DetectedUnit = firstItem.Unit,
                    Occurrences = occurrences,
                    AverageDose = avgDose,
                    Action = "Match"
                });
            }
            else
            {
                unmatchedCount++;
                result.Add(new LaborImportSupplyMappingDto
                {
                    RawName = rawName,
                    NormalizedName = normRaw,
                    MatchedSupplyId = null,
                    MatchedSupplyName = null,
                    Confidence = 0,
                    ConfidenceLevel = "None",
                    IsFromAlias = false,
                    DetectedCategory = firstItem.Category,
                    DetectedUnit = firstItem.Unit,
                    Occurrences = occurrences,
                    AverageDose = avgDose,
                    Action = "CreateNew",
                    NewItemName = rawName,
                    NewCategory = !string.IsNullOrWhiteSpace(firstItem.Category) ? firstItem.Category : "Insumos",
                    NewUnit = !string.IsNullOrWhiteSpace(firstItem.Unit) ? firstItem.Unit : "unidad"
                });
            }
        }

        // Sort: Non-matches first (so user sees them right away), then alphabetically
        return (result.OrderBy(r => r.ConfidenceLevel == "None" ? 0 : (r.ConfidenceLevel == "Medium" ? 1 : 2))
                      .ThenBy(r => r.RawName)
                      .ToList(), unmatchedCount);
    }

    private (List<LaborImportTypeMappingDto> Mappings, int UnmatchedCount) BuildLaborTypeMappings(
        List<LaborImportParsedLaborDto> labors,
        List<LaborType> laborTypes)
    {
        var typesGrouped = labors
            .Where(l => !string.IsNullOrWhiteSpace(l.LaborTypeName))
            .GroupBy(l => l.LaborTypeName.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToList();

        var typesByNorm = new Dictionary<string, LaborType>(StringComparer.OrdinalIgnoreCase);
        foreach (var lt in laborTypes)
        {
            string norm = NormalizeString(lt.Name);
            if (!string.IsNullOrWhiteSpace(norm) && !typesByNorm.ContainsKey(norm))
            {
                typesByNorm[norm] = lt;
            }
        }

        var result = new List<LaborImportTypeMappingDto>();
        int unmatchedCount = 0;

        foreach (var group in typesGrouped)
        {
            string rawName = group.Key;
            string normRaw = NormalizeString(rawName);
            int occurrences = group.Count();

            // Tier 1: Exact match on normalized name
            if (typesByNorm.TryGetValue(normRaw, out var exactLt))
            {
                result.Add(new LaborImportTypeMappingDto
                {
                    RawName = rawName,
                    NormalizedName = normRaw,
                    MatchedLaborTypeId = exactLt.Id,
                    MatchedLaborTypeName = exactLt.Name,
                    Confidence = 1.0,
                    ConfidenceLevel = "High",
                    Occurrences = occurrences,
                    Action = "Match"
                });
                continue;
            }

            // Tier 2: Substring / Contains match
            var containsMatches = laborTypes
                .Where(lt =>
                {
                    string n = NormalizeString(lt.Name);
                    return !string.IsNullOrWhiteSpace(n) && (n.Contains(normRaw) || normRaw.Contains(n));
                })
                .ToList();

            if (containsMatches.Count == 1)
            {
                var match = containsMatches[0];
                result.Add(new LaborImportTypeMappingDto
                {
                    RawName = rawName,
                    NormalizedName = normRaw,
                    MatchedLaborTypeId = match.Id,
                    MatchedLaborTypeName = match.Name,
                    Confidence = 0.90,
                    ConfidenceLevel = "High",
                    Occurrences = occurrences,
                    Action = "Match"
                });
                continue;
            }

            // Tier 3: Fuzzy similarity matching
            double bestScore = 0;
            LaborType? bestLt = null;

            foreach (var lt in laborTypes)
            {
                double score = CalculateSimilarity(normRaw, NormalizeString(lt.Name));
                if (score > bestScore)
                {
                    bestScore = score;
                    bestLt = lt;
                }
            }

            if (bestScore >= 0.60 && bestLt != null)
            {
                result.Add(new LaborImportTypeMappingDto
                {
                    RawName = rawName,
                    NormalizedName = normRaw,
                    MatchedLaborTypeId = bestLt.Id,
                    MatchedLaborTypeName = bestLt.Name,
                    Confidence = Math.Round(bestScore, 2),
                    ConfidenceLevel = bestScore >= 0.80 ? "High" : "Medium",
                    Occurrences = occurrences,
                    Action = "Match"
                });
            }
            else
            {
                unmatchedCount++;
                result.Add(new LaborImportTypeMappingDto
                {
                    RawName = rawName,
                    NormalizedName = normRaw,
                    MatchedLaborTypeId = null,
                    MatchedLaborTypeName = null,
                    Confidence = 0,
                    ConfidenceLevel = "None",
                    Occurrences = occurrences,
                    Action = "CreateNew",
                    NewTypeName = rawName
                });
            }
        }

        // Sort: Non-matches first (so user sees them right away), then alphabetically
        return (result.OrderBy(r => r.ConfidenceLevel == "None" ? 0 : (r.ConfidenceLevel == "Medium" ? 1 : 2))
                      .ThenBy(r => r.RawName)
                      .ToList(), unmatchedCount);
    }

    #endregion

    #region String & Number Helpers

    private static string NormalizeString(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        string normalized = text.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (char c in normalized)
        {
            var uc = CharUnicodeInfo.GetUnicodeCategory(c);
            if (uc != UnicodeCategory.NonSpacingMark && (char.IsLetterOrDigit(c) || char.IsWhiteSpace(c)))
            {
                sb.Append(c);
            }
        }
        return Regex.Replace(sb.ToString(), @"\s+", " ").Trim();
    }

    private static double CalculateSimilarity(string source, string target)
    {
        if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(target)) return 0.0;
        if (source == target) return 1.0;

        // Check if one contains the other
        if (source.Contains(target) || target.Contains(source))
        {
            int minLen = Math.Min(source.Length, target.Length);
            int maxLen = Math.Max(source.Length, target.Length);
            return 0.75 + (0.25 * ((double)minLen / maxLen));
        }

        int distance = LevenshteinDistance(source, target);
        int maxLenTotal = Math.Max(source.Length, target.Length);
        return 1.0 - ((double)distance / maxLenTotal);
    }

    private static int LevenshteinDistance(string s, string t)
    {
        int n = s.Length;
        int m = t.Length;
        var d = new int[n + 1, m + 1];

        if (n == 0) return m;
        if (m == 0) return n;

        for (int i = 0; i <= n; d[i, 0] = i++) ;
        for (int j = 0; j <= m; d[0, j] = j++) ;

        for (int i = 1; i <= n; i++)
        {
            for (int j = 1; j <= m; j++)
            {
                int cost = (t[j - 1] == s[i - 1]) ? 0 : 1;
                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost);
            }
        }
        return d[n, m];
    }

    private static decimal ParseDecimalCell(IXLCell cell)
    {
        if (cell.IsEmpty()) return 0;
        if (cell.DataType == XLDataType.Number)
            return (decimal)cell.GetDouble();

        string raw = cell.GetString().Trim().Replace("$", "").Replace("ha", "").Replace("kg", "").Replace("l", "").Trim();
        if (string.IsNullOrWhiteSpace(raw)) return 0;

        if (decimal.TryParse(raw, NumberStyles.Any, CultureInfo.GetCultureInfo("es-AR"), out var dVal))
            return dVal;
        if (decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out dVal))
            return dVal;

        return 0;
    }

    private static DateTime? ParseDateCell(IXLCell cell)
    {
        if (cell.IsEmpty()) return null;

        if (cell.DataType == XLDataType.DateTime)
        {
            return DateTime.SpecifyKind(cell.GetDateTime(), DateTimeKind.Utc);
        }

        if (cell.DataType == XLDataType.Number)
        {
            double serial = cell.GetDouble();
            if (serial > 30000 && serial < 70000)
            {
                var dt = DateTime.FromOADate(serial);
                return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
            }
        }

        string raw = cell.GetString().Trim();
        if (string.IsNullOrWhiteSpace(raw)) return null;

        string[] formats = { "dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy", "yyyy-MM-dd", "yyyy/MM/dd", "d/M/yy", "dd/MM/yy" };
        if (DateTime.TryParseExact(raw, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            return DateTime.SpecifyKind(parsed, DateTimeKind.Utc);

        if (DateTime.TryParse(raw, CultureInfo.GetCultureInfo("es-AR"), DateTimeStyles.None, out parsed))
            return DateTime.SpecifyKind(parsed, DateTimeKind.Utc);

        return null;
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

    private static (Guid? ContactId, string? MatchedName, bool IsExternalBilling) MatchContact(
        string? rawContractor,
        List<Contact> contacts)
    {
        if (string.IsNullOrWhiteSpace(rawContractor))
            return (null, null, false);

        string raw = rawContractor.Trim();
        string norm = NormalizeString(raw);

        if (norm == "propio" || norm == "equipo propio" || norm == "personal propio" || norm == "propia")
        {
            var exactPropio = contacts.FirstOrDefault(c => NormalizeString(c.FullName) == norm);
            if (exactPropio != null)
                return (exactPropio.Id, exactPropio.FullName, false);

            return (null, null, false);
        }

        // 1. Exact match on FullName or LegalName
        var exact = contacts.FirstOrDefault(c =>
            NormalizeString(c.FullName) == norm ||
            (!string.IsNullOrWhiteSpace(c.LegalName) && NormalizeString(c.LegalName) == norm));

        if (exact != null)
        {
            return (exact.Id, exact.FullName, exact.Role == ContactRole.Contractor);
        }

        // 2. Clean common prefixes: "contratista ", "cont. ", "empresa ", "servicios ", "serv. "
        string stripped = Regex.Replace(norm, @"^(contratista|cont\.?|empresa|servicios|serv\.?)\s+", "", RegexOptions.IgnoreCase).Trim();
        if (stripped != norm && !string.IsNullOrWhiteSpace(stripped))
        {
            var matchStripped = contacts.FirstOrDefault(c =>
                NormalizeString(c.FullName) == stripped ||
                (!string.IsNullOrWhiteSpace(c.LegalName) && NormalizeString(c.LegalName) == stripped));

            if (matchStripped != null)
            {
                return (matchStripped.Id, matchStripped.FullName, matchStripped.Role == ContactRole.Contractor);
            }
        }

        // 3. Substring matching (if string is long enough)
        if (norm.Length >= 4)
        {
            var subMatch = contacts.FirstOrDefault(c =>
            {
                string cNorm = NormalizeString(c.FullName);
                return cNorm.Contains(norm) || norm.Contains(cNorm);
            });

            if (subMatch != null)
            {
                return (subMatch.Id, subMatch.FullName, subMatch.Role == ContactRole.Contractor);
            }
        }

        bool isContractor = norm.Contains("contrat") || norm.Contains("tercero") || (!norm.Contains("propio") && !string.IsNullOrWhiteSpace(norm));
        return (null, null, isContractor);
    }

    #endregion
}
