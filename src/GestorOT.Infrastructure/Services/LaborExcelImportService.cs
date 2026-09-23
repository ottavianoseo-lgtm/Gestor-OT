using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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

        var existingLaborTypeAliases = await _context.LaborTypeAliases
            .Include(a => a.LaborType)
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
        var (laborTypeMappings, unmatchedLaborTypesCount) = BuildLaborTypeMappings(parsedLabors, existingLaborTypeAliases, existingLaborTypes);

        // Match supply providers (OT-26)
        var (supplierMappings, unmatchedSuppliersCount) = BuildSupplierMappings(parsedLabors);

        // Link matched supplies and labor types back to parsed items for preview
        ApplyMappingsToLabors(parsedLabors, supplyMappings, laborTypeMappings);

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

        if (unmatchedSuppliersCount > 0)
        {
            diagnostics.Add($"Se detectaron {unmatchedSuppliersCount} proveedores de insumos sin coincidencia directa en el padrón de contactos. Podés vincularlos en la pestaña de Reconciliación de Proveedores; si quedan sin vincular, el insumo se importa igual pero sin proveedor asignado.");
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
            UnmatchedSuppliersCount = unmatchedSuppliersCount,
            Labors = parsedLabors,
            SupplyMappings = supplyMappings,
            LaborTypeMappings = laborTypeMappings,
            SupplierMappings = supplierMappings,
            Diagnostics = diagnostics,
            CanProceed = parsedLabors.Count > 0
        };
    }

    public async Task<LaborImportResultDto> ExecuteAsync(
        Guid campaignId,
        Stream fileStream,
        List<LaborImportSupplyMappingDto> mappings,
        List<LaborImportTypeMappingDto>? laborTypeMappings = null,
        List<LaborImportSupplierMappingDto>? supplierMappings = null,
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
            .Include(cl => cl.Rotations)
            .Where(cl => cl.CampaignId == campaignId)
            .ToListAsync(ct);

        var existingLaborTypes = await _context.LaborTypes.ToListAsync(ct);
        var existingLaborTypeAliases = await _context.LaborTypeAliases.Include(a => a.LaborType).ToListAsync(ct);
        var existingAliases = await _context.SupplyAliases.ToListAsync(ct);
        var existingInventories = await _context.Inventories.ToListAsync(ct);
        var existingContacts = await _context.Contacts.ToListAsync(ct);

        // Fetch existing labors in this campaign to support deduplication/updating on re-import
        var campaignLotIds = campaignLots.Select(cl => cl.Id).ToHashSet();
        var existingLabors = await _context.Labors
            .Include(l => l.Supplies)
            .Where(l => l.CampaignLotId.HasValue && campaignLotIds.Contains(l.CampaignLotId.Value))
            .ToListAsync(ct);

        var parsedLabors = ParseLaborsFromWorksheet(worksheet, columnConfig, campaignLots, existingLaborTypes, existingContacts);

        var counters = new ImportCounters();

        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            var isRelational = _context.Database.IsRelational();
            Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? tx = isRelational ? await _context.Database.BeginTransactionAsync(ct) : null;
            try
            {
                // 1+2. Mappings de conciliación (núcleo compartido con subida directa y lotes pendientes)
                var resolved = ProcessImportMappings(mappings, laborTypeMappings, supplierMappings,
                    existingAliases, existingLaborTypes, existingLaborTypeAliases,
                    existingInventories, existingContacts, counters, autoCreateUnmatchedSupplies: true);

                // 3. Process Labors & Supplies
                ImportParsedLaborList(parsedLabors, resolved, campaignLots, existingLabors,
                    existingInventories, existingContacts, existingLaborTypeAliases, counters);

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
            LaborsCreated = counters.LaborsCreated,
            LaborsUpdated = counters.LaborsUpdated,
            SuppliesCreated = counters.SuppliesCreated,
            NewSuppliesCreated = counters.NewInventories,
            NewLaborTypesCreated = 0,
            AliasesLearned = counters.AliasesLearned,
            Errors = counters.Errors,
            Success = (counters.LaborsCreated + counters.LaborsUpdated) > 0
        };
    }

    #region Núcleo de importación compartido (ejecución directa y lotes pendientes)

    private sealed class ImportCounters
    {
        public int LaborsCreated;
        public int LaborsUpdated;
        public int SuppliesCreated;
        public int SuppliesSkipped;
        public int NewInventories;
        public int AliasesLearned;
        public List<string> Errors = new();
    }

    private sealed record ResolvedImportMappings(
        Dictionary<string, Guid> Supplies,
        Dictionary<string, Guid> LaborTypes,
        Dictionary<string, LaborType> LaborTypesByName,
        Dictionary<string, Guid?> Suppliers);

    /// <summary>
    /// Procesa los mappings de conciliación: aprende alias y resuelve los
    /// diccionarios que usa la importación de labores. Crear insumo nuevo requiere
    /// autoCreateUnmatchedSupplies=true (wizard viejo, sin UI que lo dispare hoy) o
    /// un mapping "CreateNew" con Confirmed=true (una persona lo eligió a mano en la
    /// conciliación y guardó). El default del algoritmo para lo que no llega a 0.70
    /// de similitud también es "CreateNew" pero con Confirmed=false: sin retoque
    /// humano no crea nada, el insumo queda sin resolver y la fila va a revisión en
    /// vez de contaminar el catálogo con ítems sin código ERP.
    /// </summary>
    private ResolvedImportMappings ProcessImportMappings(
        List<LaborImportSupplyMappingDto> mappings,
        List<LaborImportTypeMappingDto>? laborTypeMappings,
        List<LaborImportSupplierMappingDto>? supplierMappings,
        List<SupplyAlias> existingAliases,
        List<LaborType> existingLaborTypes,
        List<LaborTypeAlias> existingLaborTypeAliases,
        List<Inventory> existingInventories,
        List<Contact> existingContacts,
        ImportCounters counters,
        bool autoCreateUnmatchedSupplies)
    {
        // Los mappings de un lote pendiente se congelan cuando se sube el archivo, pero
        // el catálogo sigue vivo: un insumo borrado (o un re-sync del ERP que recrea la
        // fila con otro Id) deja el MatchedSupplyId apuntando a la nada. Si ese id se
        // usa igual, el insert del SupplyAlias viola el FK contra Inventories y se cae
        // la transacción entera, sin importar ninguna fila. Validamos contra el catálogo
        // actual y mandamos a revisión lo que quedó colgado.
        var validSupplyIds = existingInventories.Select(i => i.Id).ToHashSet();
        var validLaborTypeIds = existingLaborTypes.Select(lt => lt.Id).ToHashSet();
        var validContactIds = existingContacts.Select(c => c.Id).ToHashSet();

        // 1. Process Supply Mappings (Creations and Aliases)
        var resolvedSupplies = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

        foreach (var map in mappings)
        {
            if (string.Equals(map.Action, "Ignore", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            Guid? targetSupplyId = map.MatchedSupplyId;

            if (targetSupplyId.HasValue && !validSupplyIds.Contains(targetSupplyId.Value))
            {
                _logger.LogWarning(
                    "El mapping de insumo '{RawName}' apunta al inventario {SupplyId}, que ya no existe. Queda sin resolver.",
                    map.RawName, targetSupplyId.Value);
                counters.Errors.Add($"El insumo '{map.RawName}' estaba vinculado a un ítem de inventario que ya no existe. Volvé a vincularlo en la conciliación.");
                continue;
            }

            // El matcheador nunca crea insumos por su cuenta: no tienen código ERP. Si
            // "CreateNew" quedó así por default del algoritmo (map.Confirmed en false,
            // nadie tocó la fila en la conciliación), no se crea nada: el insumo queda
            // sin resolver y la fila va a revisión, igual que un insumo sin match. Solo
            // se crea cuando una persona lo confirmó explícitamente (radio + Guardar
            // matches) o cuando autoCreateUnmatchedSupplies=true (wizard viejo).
            bool explicitCreate = string.Equals(map.Action, "CreateNew", StringComparison.OrdinalIgnoreCase) && map.Confirmed;
            if (!targetSupplyId.HasValue && (explicitCreate || autoCreateUnmatchedSupplies))
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
                // Se suma al catálogo en memoria: el alias que viene abajo lo da por
                // válido y ImportParsedLaborList resuelve su unidad en vez de caer al
                // "unidad" por default.
                existingInventories.Add(newInventory);
                validSupplyIds.Add(newInventory.Id);
                targetSupplyId = newInventory.Id;
                counters.NewInventories++;
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
                    counters.AliasesLearned++;
                }
            }
        }

        // 2. Process Labor Type Mappings (Learns aliases, NEVER creates new LaborType without ERP)
        var laborTypesByName = new Dictionary<string, LaborType>(StringComparer.OrdinalIgnoreCase);
        foreach (var lt in existingLaborTypes)
        {
            string norm = NormalizeString(lt.Name);
            if (!string.IsNullOrWhiteSpace(norm) && !laborTypesByName.ContainsKey(norm))
            {
                laborTypesByName[norm] = lt;
            }
        }

        var resolvedLaborTypes = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        if (laborTypeMappings != null)
        {
            foreach (var ltm in laborTypeMappings)
            {
                if (ltm.MatchedLaborTypeId.HasValue)
                {
                    var targetLtId = ltm.MatchedLaborTypeId.Value;

                    // Mismo riesgo que el insumo congelado: si la labor type ya no está,
                    // el alias viola el FK y voltea la transacción.
                    if (!validLaborTypeIds.Contains(targetLtId))
                    {
                        _logger.LogWarning(
                            "El mapping de labor '{RawName}' apunta al tipo {LaborTypeId}, que ya no existe. Queda sin resolver.",
                            ltm.RawName, targetLtId);
                        counters.Errors.Add($"La labor '{ltm.RawName}' estaba vinculada a un tipo que ya no existe. Volvé a vincularla en la conciliación.");
                        continue;
                    }

                    resolvedLaborTypes[ltm.RawName.Trim()] = targetLtId;

                    // Learn alias if not already existing
                    string normRaw = NormalizeString(ltm.RawName);
                    bool aliasExists = existingLaborTypeAliases.Any(a => string.Equals(a.NormalizedName, normRaw, StringComparison.OrdinalIgnoreCase));
                    if (!aliasExists)
                    {
                        var newAlias = new LaborTypeAlias
                        {
                            Id = Guid.NewGuid(),
                            TenantId = _context.CurrentTenantId,
                            RawName = ltm.RawName.Trim(),
                            NormalizedName = normRaw,
                            LaborTypeId = targetLtId,
                            CreatedAt = DateTime.UtcNow
                        };
                        _context.LaborTypeAliases.Add(newAlias);
                        existingLaborTypeAliases.Add(newAlias);
                        counters.AliasesLearned++;
                    }
                }
            }
        }

        // 2b. Resolve supply provider overrides confirmed by the user in preview (OT-26).
        // Un proveedor sin vincular no bloquea nada: el LaborSupply se crea igual, solo
        // sin SupplierContactId (ver trampa del plan: nombre no resuelto no aborta el import).
        var resolvedSuppliers = new Dictionary<string, Guid?>(StringComparer.OrdinalIgnoreCase);
        if (supplierMappings != null)
        {
            foreach (var sm in supplierMappings)
            {
                if (string.Equals(sm.Action, "Ignore", StringComparison.OrdinalIgnoreCase))
                {
                    resolvedSuppliers[sm.RawName.Trim()] = null;
                    continue;
                }

                // Contacto borrado desde que se congeló el mapping: el FK de
                // LaborSupply.SupplierContactId reventaría la transacción. Acá no
                // bloquea la fila (un proveedor sin vincular nunca lo hizo), solo
                // se importa sin proveedor.
                if (sm.MatchedContactId.HasValue && !validContactIds.Contains(sm.MatchedContactId.Value))
                {
                    _logger.LogWarning(
                        "El mapping de proveedor '{RawName}' apunta al contacto {ContactId}, que ya no existe. Se importa sin proveedor.",
                        sm.RawName, sm.MatchedContactId.Value);
                    resolvedSuppliers[sm.RawName.Trim()] = null;
                    continue;
                }

                resolvedSuppliers[sm.RawName.Trim()] = sm.MatchedContactId;
            }
        }

        return new ResolvedImportMappings(resolvedSupplies, resolvedLaborTypes, laborTypesByName, resolvedSuppliers);
    }

    /// <summary>
    /// Importa una lista de labores ya parseadas (crea o actualiza por
    /// deduplicación lote+fecha+tipo) con sus recetas de insumos. Devuelve las
    /// labores persistidas por fila para trazabilidad. No hace SaveChanges.
    /// </summary>
    private List<(int RowIndex, Guid LaborId)> ImportParsedLaborList(
        List<LaborImportParsedLaborDto> parsedLabors,
        ResolvedImportMappings resolved,
        List<CampaignLot> campaignLots,
        List<Labor> existingLabors,
        List<Inventory> existingInventories,
        List<Contact> existingContacts,
        List<LaborTypeAlias> existingLaborTypeAliases,
        ImportCounters counters)
    {
        var imported = new List<(int RowIndex, Guid LaborId)>();

        // Las filas de un lote pendiente guardan el contacto que matcheó al subir el
        // archivo. Si desde entonces lo borraron, escribirlo revienta el FK y voltea
        // la transacción; importar sin responsable/proveedor es la degradación
        // esperada (un nombre sin resolver nunca abortó el import).
        var validContactIds = existingContacts.Select(c => c.Id).ToHashSet();
        Guid? LiveContact(Guid? id) => id.HasValue && validContactIds.Contains(id.Value) ? id : null;

        // 3. Process Labors & Supplies
        foreach (var parsedLabor in parsedLabors)
        {
            if (!parsedLabor.LotId.HasValue || !parsedLabor.CampaignLotId.HasValue)
            {
                counters.Errors.Add($"Labor en fila {parsedLabor.RowIndex}: El lote '{parsedLabor.LotName}' no pertenece a la campaña. Se omitió.");
                continue;
            }

            // Resolve LaborType strictly from mapped or existing ERP concepts
            string laborTypeName = !string.IsNullOrWhiteSpace(parsedLabor.LaborTypeName) ? parsedLabor.LaborTypeName.Trim() : "Labor General";
            Guid? targetLaborTypeId = null;

            if (resolved.LaborTypes.TryGetValue(laborTypeName, out var mappedLtId))
            {
                targetLaborTypeId = mappedLtId;
            }
            else
            {
                string normLt = NormalizeString(laborTypeName);
                var aliasMatch = existingLaborTypeAliases.FirstOrDefault(a => string.Equals(a.NormalizedName, normLt, StringComparison.OrdinalIgnoreCase));
                if (aliasMatch != null)
                {
                    targetLaborTypeId = aliasMatch.LaborTypeId;
                }
                else if (resolved.LaborTypesByName.TryGetValue(normLt, out var laborType))
                {
                    targetLaborTypeId = laborType.Id;
                }
            }

            if (!targetLaborTypeId.HasValue)
            {
                counters.Errors.Add($"Labor en fila {parsedLabor.RowIndex}: El tipo de labor '{laborTypeName}' no está vinculado a ningún concepto del ERP. Se omitió.");
                continue;
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

            // Check if labor already exists (deduplication on re-import: same Lot + Date + LaborType)
            Labor? existingLabor = null;
            if (parsedLabor.Date.HasValue)
            {
                var targetDate = parsedLabor.Date.Value.Date;
                existingLabor = existingLabors.FirstOrDefault(l =>
                    l.CampaignLotId == parsedLabor.CampaignLotId.Value &&
                    l.LaborTypeId == targetLaborTypeId.Value &&
                    ((l.ExecutionDate.HasValue && l.ExecutionDate.Value.Date == targetDate) ||
                     (l.EstimatedDate.HasValue && l.EstimatedDate.Value.Date == targetDate)));
            }

            Labor laborToSave;
            if (existingLabor != null)
            {
                existingLabor.Hectares = parsedLabor.Hectares;
                existingLabor.EffectiveArea = parsedLabor.Hectares;
                existingLabor.ContactId = LiveContact(parsedLabor.ContactId);
                existingLabor.IsExternalBilling = parsedLabor.IsExternalBilling;
                existingLabor.ExecutionDate = parsedLabor.Date;
                existingLabor.EstimatedDate = parsedLabor.Date;
                existingLabor.Mode = laborMode;
                existingLabor.Status = laborStatus;
                existingLabor.Notes = string.IsNullOrWhiteSpace(parsedLabor.Contractor)
                    ? "Importado desde Excel"
                    : $"Importado desde Excel. Contratista/Equipo: {parsedLabor.Contractor}";

                // Replace supplies
                var oldSupplies = existingLabor.Supplies.ToList();
                if (oldSupplies.Count > 0)
                {
                    _context.LaborSupplies.RemoveRange(oldSupplies);
                }

                laborToSave = existingLabor;
                counters.LaborsUpdated++;
            }
            else
            {
                // Resolve ErpActivityId from the active rotation for this lot at the labor date
                Guid? resolvedActivityId = null;
                if (campaignLot != null && parsedLabor.Date.HasValue)
                {
                    var laborDate = DateOnly.FromDateTime(parsedLabor.Date.Value);
                    var activeRotation = campaignLot.Rotations
                        .FirstOrDefault(r => r.StartDate <= laborDate && r.EndDate >= laborDate);
                    resolvedActivityId = activeRotation?.ErpActivityId;
                }

                laborToSave = new Labor
                {
                    Id = Guid.NewGuid(),
                    TenantId = _context.CurrentTenantId,
                    LotId = parsedLabor.LotId.Value,
                    CampaignLotId = parsedLabor.CampaignLotId.Value,
                    ErpActivityId = resolvedActivityId,
                    LaborTypeId = targetLaborTypeId.Value,
                    ContactId = LiveContact(parsedLabor.ContactId),
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

                _context.Labors.Add(laborToSave);
                existingLabors.Add(laborToSave);
                counters.LaborsCreated++;
            }

            int mixOrder = 1;
            foreach (var sup in parsedLabor.Supplies)
            {
                if (!resolved.Supplies.TryGetValue(sup.SupplyName.Trim(), out var supplyId))
                {
                    // Un insumo sin resolver no puede desaparecer callado: la labor se
                    // importa igual pero queda constancia de qué se perdió, que es como
                    // se detectó OT-61 (201 de 363 insumos que nunca llegaron a la base).
                    counters.SuppliesSkipped++;
                    _logger.LogWarning(
                        "Fila {Row}: el insumo '{SupplyName}' no está vinculado a inventario, se importa la labor sin él.",
                        parsedLabor.RowIndex, sup.SupplyName);
                    continue;
                }

                decimal plannedDose = sup.Dose;
                decimal totalQty = sup.Total ?? (sup.Dose * laborToSave.Hectares);
                string supplyUnit = !string.IsNullOrWhiteSpace(sup.Unit)
                    ? sup.Unit
                    : (existingInventories.FirstOrDefault(i => i.Id == supplyId)?.Unit ?? "unidad");

                // Proveedor del insumo (OT-26): preferir la corrección del usuario en
                // preview; si no la hay, usar el matcheo automático hecho al parsear.
                Guid? supplierContactId = null;
                if (!string.IsNullOrWhiteSpace(sup.SupplierRawName))
                {
                    supplierContactId = resolved.Suppliers.TryGetValue(sup.SupplierRawName.Trim(), out var overrideId)
                        ? overrideId
                        : LiveContact(sup.SupplierContactId);
                }

                var laborSupply = new LaborSupply
                {
                    Id = Guid.NewGuid(),
                    TenantId = _context.CurrentTenantId,
                    LaborId = laborToSave.Id,
                    SupplyId = supplyId,
                    PlannedHectares = laborToSave.Hectares,
                    RealHectares = isRealized ? laborToSave.Hectares : null,
                    PlannedDose = plannedDose,
                    RealDose = isRealized ? plannedDose : null,
                    PlannedTotal = totalQty,
                    RealTotal = isRealized ? totalQty : null,
                    UnitOfMeasure = supplyUnit,
                    TankMixOrder = mixOrder++,
                    SupplierContactId = supplierContactId
                };

                _context.LaborSupplies.Add(laborSupply);
                counters.SuppliesCreated++;
            }

            imported.Add((parsedLabor.RowIndex, laborToSave.Id));
        }

        return imported;
    }

    /// <summary>
    /// Los mismos valores que MatchContact considera "propio" y por los que no
    /// exige un contacto vinculado.
    /// </summary>
    private static bool IsPropioLike(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return true;
        string norm = NormalizeString(raw);
        return norm == "propio" || norm == "equipo propio" || norm == "personal propio" || norm == "propia";
    }

    /// <summary>
    /// Verde estricto: la fila hizo full match y se puede importar sola al subir
    /// el Excel (lote + tipo + todos los insumos + responsable/proveedor).
    /// </summary>
    private static bool IsGreenRow(LaborImportParsedLaborDto labor)
    {
        if (!labor.LotId.HasValue || !labor.CampaignLotId.HasValue || labor.Errors.Count > 0)
            return false;
        if (!labor.LaborTypeId.HasValue)
            return false;
        foreach (var s in labor.Supplies)
        {
            if (!s.MatchedSupplyId.HasValue)
                return false;
            if (!string.IsNullOrWhiteSpace(s.SupplierRawName)
                && !IsPropioLike(s.SupplierRawName)
                && !s.SupplierContactId.HasValue)
                return false;
        }
        if (!string.IsNullOrWhiteSpace(labor.Contractor) && !labor.ContactId.HasValue && !IsPropioLike(labor.Contractor))
            return false;
        return true;
    }

    /// <summary>
    /// Chequea si una fila pendiente ya es importable con los mappings actuales
    /// del batch (teniendo en cuenta decisiones explícitas como Ignorar).
    /// </summary>
    private static bool IsRowImportable(
        LaborImportParsedLaborDto labor,
        ResolvedImportMappings resolved,
        List<LaborTypeAlias> existingLaborTypeAliases,
        List<LaborImportSupplyMappingDto> supplyMappings,
        List<LaborImportSupplierMappingDto> supplierMappings,
        out string? reason)
    {
        reason = null;
        if (!labor.LotId.HasValue || !labor.CampaignLotId.HasValue || labor.Errors.Count > 0)
        {
            reason = $"el lote '{labor.LotName}' no está en la campaña";
            return false;
        }

        string laborTypeName = !string.IsNullOrWhiteSpace(labor.LaborTypeName) ? labor.LaborTypeName.Trim() : "Labor General";
        bool typeOk = resolved.LaborTypes.ContainsKey(laborTypeName)
            || existingLaborTypeAliases.Any(a => string.Equals(a.NormalizedName, NormalizeString(laborTypeName), StringComparison.OrdinalIgnoreCase))
            || resolved.LaborTypesByName.ContainsKey(NormalizeString(laborTypeName));
        if (!typeOk)
        {
            reason = $"el tipo '{laborTypeName}' no está vinculado a un concepto del ERP";
            return false;
        }

        foreach (var s in labor.Supplies)
        {
            if (string.IsNullOrWhiteSpace(s.SupplyName))
                continue;
            if (resolved.Supplies.ContainsKey(s.SupplyName.Trim()))
                continue;
            var map = supplyMappings.FirstOrDefault(m => string.Equals(m.RawName.Trim(), s.SupplyName.Trim(), StringComparison.OrdinalIgnoreCase));
            if (map != null && string.Equals(map.Action, "Ignore", StringComparison.OrdinalIgnoreCase))
                continue;
            reason = $"el insumo '{s.SupplyName}' no está vinculado a inventario";
            return false;
        }

        foreach (var s in labor.Supplies)
        {
            if (string.IsNullOrWhiteSpace(s.SupplierRawName) || IsPropioLike(s.SupplierRawName))
                continue;
            var map = supplierMappings.FirstOrDefault(m => string.Equals(m.RawName.Trim(), s.SupplierRawName!.Trim(), StringComparison.OrdinalIgnoreCase));
            if (map != null)
                continue; // vinculado o explícitamente ignorado
            if (s.SupplierContactId.HasValue)
                continue;
            reason = $"el proveedor '{s.SupplierRawName}' no está vinculado al padrón";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(labor.Contractor) && !labor.ContactId.HasValue && !IsPropioLike(labor.Contractor))
        {
            reason = $"el responsable '{labor.Contractor}' no está vinculado al padrón";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Vincula los mappings automáticos a las filas parseadas (mismo código que
    /// usa la vista previa, para que subir y ver pendiente coincidan).
    /// </summary>
    private static void ApplyMappingsToLabors(
        List<LaborImportParsedLaborDto> parsedLabors,
        List<LaborImportSupplyMappingDto> supplyMappings,
        List<LaborImportTypeMappingDto> laborTypeMappings)
    {
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
    }

    #endregion

    #region Lotes de importación pendientes (sección Importaciones pendientes)

    private static readonly JsonSerializerOptions BatchJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private static string ComputeSha256(byte[] bytes)
    {
        byte[] hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }

    private static LaborImportPendingRow ToPendingRow(LaborImportBatch batch, LaborImportParsedLaborDto labor)
    {
        return new LaborImportPendingRow
        {
            Id = Guid.NewGuid(),
            TenantId = batch.TenantId,
            BatchId = batch.Id,
            RowIndex = labor.RowIndex,
            Date = labor.Date,
            FieldName = labor.FieldName,
            LotName = labor.LotName,
            LotId = labor.LotId,
            CampaignLotId = labor.CampaignLotId,
            Hectares = labor.Hectares,
            LaborTypeName = labor.LaborTypeName,
            LaborTypeId = labor.LaborTypeId,
            Contractor = labor.Contractor,
            ContactId = labor.ContactId,
            MatchedContactName = labor.MatchedContactName,
            IsExternalBilling = labor.IsExternalBilling,
            SuppliesJson = JsonSerializer.Serialize(labor.Supplies, BatchJsonOptions),
            ErrorsJson = JsonSerializer.Serialize(labor.Errors, BatchJsonOptions),
            WarningsJson = JsonSerializer.Serialize(labor.Warnings, BatchJsonOptions),
            Resolution = LaborImportRowResolution.Unresolved
        };
    }

    private static LaborImportParsedLaborDto ToParsedLabor(LaborImportPendingRow row)
    {
        List<LaborImportParsedItemDto> supplies;
        List<string> errors;
        List<string> warnings;
        try { supplies = JsonSerializer.Deserialize<List<LaborImportParsedItemDto>>(row.SuppliesJson, BatchJsonOptions) ?? new(); }
        catch { supplies = new(); }
        try { errors = JsonSerializer.Deserialize<List<string>>(row.ErrorsJson, BatchJsonOptions) ?? new(); }
        catch { errors = new(); }
        try { warnings = JsonSerializer.Deserialize<List<string>>(row.WarningsJson, BatchJsonOptions) ?? new(); }
        catch { warnings = new(); }

        return new LaborImportParsedLaborDto
        {
            RowIndex = row.RowIndex,
            Date = row.Date,
            FieldName = row.FieldName,
            LotName = row.LotName,
            LotId = row.LotId,
            CampaignLotId = row.CampaignLotId,
            Hectares = row.Hectares,
            LaborTypeName = row.LaborTypeName,
            LaborTypeId = row.LaborTypeId,
            Contractor = row.Contractor,
            ContactId = row.ContactId,
            MatchedContactName = row.MatchedContactName,
            IsExternalBilling = row.IsExternalBilling,
            Mode = "Realized",
            Status = "Realized",
            Supplies = supplies,
            Errors = errors,
            Warnings = warnings
        };
    }

    private static List<T> FromBatchJson<T>(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new();
        try { return JsonSerializer.Deserialize<List<T>>(json, BatchJsonOptions) ?? new(); }
        catch { return new(); }
    }

    private static LaborImportBatchDto ToBatchDto(LaborImportBatch batch)
    {
        return new LaborImportBatchDto(
            batch.Id, batch.CampaignId, batch.Campaign?.Name, batch.FileName,
            batch.UploadedAt, batch.UploadedBy, batch.Status.ToString(),
            batch.TotalRows, batch.ImportedCount, batch.PendingCount, batch.ExcludedCount);
    }

    private async Task<LaborImportBatchDetailDto?> BuildBatchDetailAsync(Guid batchId, CancellationToken ct)
    {
        var batch = await _context.LaborImportBatches
            .Include(b => b.Campaign)
            .Include(b => b.Rows)
            .FirstOrDefaultAsync(b => b.Id == batchId, ct);
        if (batch == null) return null;

        var supplyMappings = FromBatchJson<LaborImportSupplyMappingDto>(batch.SupplyMappingsJson);
        var laborTypeMappings = FromBatchJson<LaborImportTypeMappingDto>(batch.LaborTypeMappingsJson);
        var supplierMappings = FromBatchJson<LaborImportSupplierMappingDto>(batch.SupplierMappingsJson);

        var orderedRows = batch.Rows.OrderBy(r => r.RowIndex).ToList();
        var labors = orderedRows.Select(ToParsedLabor).ToList();
        var states = orderedRows
            .Select(r => new LaborImportRowStateDto(r.RowIndex, r.Resolution.ToString(), r.ResultLaborId))
            .ToList();

        var diagnostics = new List<string>();
        int pendingRows = orderedRows.Count(r => r.Resolution == LaborImportRowResolution.Unresolved);
        if (pendingRows > 0)
            diagnostics.Add($"{pendingRows} fila(s) pendientes de match manual.");
        foreach (var err in orderedRows.SelectMany(r => r.ErrorsJson != "[]" ? FromBatchJson<string>(r.ErrorsJson) : new List<string>()).Distinct().Take(10))
            diagnostics.Add(err);

        var preview = new LaborImportPreviewDto
        {
            TotalLabors = labors.Count,
            TotalSupplies = labors.Sum(l => l.Supplies.Count),
            TotalHectares = labors.Sum(l => l.Hectares),
            UniqueSuppliesCount = supplyMappings.Count,
            UnmatchedSuppliesCount = supplyMappings.Count(m => !m.MatchedSupplyId.HasValue && !string.Equals(m.Action, "Ignore", StringComparison.OrdinalIgnoreCase)),
            UniqueLaborTypesCount = laborTypeMappings.Count,
            UnmatchedLaborTypesCount = laborTypeMappings.Count(m => !m.MatchedLaborTypeId.HasValue),
            UnmatchedSuppliersCount = supplierMappings.Count(m => !m.MatchedContactId.HasValue && !string.Equals(m.Action, "Ignore", StringComparison.OrdinalIgnoreCase)),
            Labors = labors,
            SupplyMappings = supplyMappings,
            LaborTypeMappings = laborTypeMappings,
            SupplierMappings = supplierMappings,
            Diagnostics = diagnostics,
            CanProceed = pendingRows > 0
        };

        return new LaborImportBatchDetailDto(ToBatchDto(batch), preview, states);
    }

    private static void UpdateBatchCounters(LaborImportBatch batch)
    {
        batch.ImportedCount = batch.Rows.Count(r => r.Resolution == LaborImportRowResolution.Imported);
        batch.ExcludedCount = batch.Rows.Count(r => r.Resolution == LaborImportRowResolution.Excluded);
        batch.PendingCount = batch.Rows.Count(r => r.Resolution == LaborImportRowResolution.Unresolved);
        batch.Status = batch.PendingCount == 0 ? LaborImportBatchStatus.Completed : LaborImportBatchStatus.Pending;
    }

    public async Task<LaborImportUploadResultDto> UploadAsync(
        Guid campaignId, Stream fileStream, string fileName, string? uploadedBy, bool force = false, CancellationToken ct = default)
    {
        var campaign = await _context.Campaigns.FirstOrDefaultAsync(c => c.Id == campaignId, ct);
        if (campaign == null)
            throw new InvalidOperationException("La campaña especificada no existe.");

        using var ms = new MemoryStream();
        await fileStream.CopyToAsync(ms, ct);
        byte[] bytes = ms.ToArray();
        if (bytes.Length == 0)
            throw new InvalidOperationException("El archivo está vacío.");
        string fileHash = ComputeSha256(bytes);

        if (!force)
        {
            var duplicate = await _context.LaborImportBatches
                .Where(b => b.CampaignId == campaignId && b.FileHash == fileHash)
                .OrderByDescending(b => b.UploadedAt)
                .FirstOrDefaultAsync(ct);
            if (duplicate != null)
            {
                return new LaborImportUploadResultDto
                {
                    DuplicateOfBatchId = duplicate.Id,
                    DuplicateFileName = duplicate.FileName,
                    DuplicateUploadedAt = duplicate.UploadedAt,
                    PendingRows = duplicate.PendingCount,
                    Success = false,
                    Errors = new List<string> { $"Este archivo ya se subió el {duplicate.UploadedAt:dd/MM/yyyy HH:mm} ({duplicate.PendingCount} fila(s) pendientes, {duplicate.ImportedCount} importadas)." }
                };
            }
        }

        using var workbook = OpenWorkbookSafely(new MemoryStream(bytes));
        var worksheet = FindLaborWorksheet(workbook);
        var columnConfig = DetectColumns(worksheet);

        var campaignLots = await _context.CampaignLots
            .Include(cl => cl.Lot)
            .ThenInclude(l => l!.Field)
            .Include(cl => cl.Rotations)
            .Where(cl => cl.CampaignId == campaignId)
            .ToListAsync(ct);

        var existingAliases = await _context.SupplyAliases
            .Include(a => a.Supply)
            .ToListAsync(ct);
        var existingInventories = await _context.Inventories.ToListAsync(ct);
        var existingLaborTypeAliases = await _context.LaborTypeAliases
            .Include(a => a.LaborType)
            .ToListAsync(ct);
        var existingLaborTypes = await _context.LaborTypes.ToListAsync(ct);
        var existingContacts = await _context.Contacts.ToListAsync(ct);

        var parsedLabors = ParseLaborsFromWorksheet(worksheet, columnConfig, campaignLots, existingLaborTypes, existingContacts);
        if (parsedLabors.Count == 0)
        {
            return new LaborImportUploadResultDto
            {
                Success = false,
                Errors = new List<string> { "No se detectaron labores en el archivo." }
            };
        }

        var (supplyMappings, _) = BuildSupplyMappings(parsedLabors, existingAliases, existingInventories);
        var (laborTypeMappings, _) = BuildLaborTypeMappings(parsedLabors, existingLaborTypeAliases, existingLaborTypes);
        var (supplierMappings, _) = BuildSupplierMappings(parsedLabors);
        ApplyMappingsToLabors(parsedLabors, supplyMappings, laborTypeMappings);

        var green = parsedLabors.Where(IsGreenRow).ToList();
        var pending = parsedLabors.Where(l => !IsGreenRow(l)).ToList();

        var campaignLotIds = campaignLots.Select(cl => cl.Id).ToHashSet();
        var existingLabors = await _context.Labors
            .Include(l => l.Supplies)
            .Where(l => l.CampaignLotId.HasValue && campaignLotIds.Contains(l.CampaignLotId.Value))
            .ToListAsync(ct);

        var counters = new ImportCounters();
        LaborImportBatch? batch = null;

        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            var isRelational = _context.Database.IsRelational();
            Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? tx = isRelational ? await _context.Database.BeginTransactionAsync(ct) : null;
            try
            {
                if (green.Count > 0)
                {
                    // Sin CreateNew implícito: lo verde ya hizo full match, así que
                    // procesar mappings solo vincula y aprende alias, no crea nada nuevo.
                    var resolved = ProcessImportMappings(supplyMappings, laborTypeMappings, supplierMappings,
                        existingAliases, existingLaborTypes, existingLaborTypeAliases,
                        existingInventories, existingContacts, counters, autoCreateUnmatchedSupplies: false);
                    ImportParsedLaborList(green, resolved, campaignLots, existingLabors,
                        existingInventories, existingContacts, existingLaborTypeAliases, counters);
                }

                if (pending.Count > 0)
                {
                    batch = new LaborImportBatch
                    {
                        Id = Guid.NewGuid(),
                        TenantId = _context.CurrentTenantId,
                        CampaignId = campaignId,
                        FileName = fileName,
                        FileHash = fileHash,
                        UploadedAt = DateTime.UtcNow,
                        UploadedBy = uploadedBy,
                        Status = LaborImportBatchStatus.Pending,
                        TotalRows = pending.Count,
                        ImportedCount = 0,
                        PendingCount = pending.Count,
                        ExcludedCount = 0,
                        SupplyMappingsJson = JsonSerializer.Serialize(supplyMappings, BatchJsonOptions),
                        LaborTypeMappingsJson = JsonSerializer.Serialize(laborTypeMappings, BatchJsonOptions),
                        SupplierMappingsJson = JsonSerializer.Serialize(supplierMappings, BatchJsonOptions)
                    };
                    _context.LaborImportBatches.Add(batch);
                    foreach (var p in pending)
                        _context.LaborImportPendingRows.Add(ToPendingRow(batch, p));
                }

                await _context.SaveChangesAsync(ct);
                if (tx != null) await tx.CommitAsync(ct);
            }
            catch (Exception ex)
            {
                if (tx != null) await tx.RollbackAsync(ct);
                _logger.LogError(ex, "Error durante la subida directa de labores.");
                throw;
            }
            finally
            {
                if (tx != null) await tx.DisposeAsync();
            }
        });

        return new LaborImportUploadResultDto
        {
            LaborsCreated = counters.LaborsCreated,
            LaborsUpdated = counters.LaborsUpdated,
            SuppliesCreated = counters.SuppliesCreated,
            NewSuppliesCreated = counters.NewInventories,
            AliasesLearned = counters.AliasesLearned,
            PendingBatchId = batch?.Id,
            PendingRows = pending.Count,
            Errors = counters.Errors,
            Success = (counters.LaborsCreated + counters.LaborsUpdated) > 0 || pending.Count > 0
        };
    }

    public async Task<List<LaborImportBatchDto>> GetBatchesAsync(Guid campaignId, CancellationToken ct = default)
    {
        return await _context.LaborImportBatches
            .Include(b => b.Campaign)
            .Where(b => b.CampaignId == campaignId)
            .OrderByDescending(b => b.UploadedAt)
            .Select(b => new LaborImportBatchDto(
                b.Id, b.CampaignId, b.Campaign != null ? b.Campaign.Name : null, b.FileName,
                b.UploadedAt, b.UploadedBy, b.Status.ToString(),
                b.TotalRows, b.ImportedCount, b.PendingCount, b.ExcludedCount))
            .ToListAsync(ct);
    }

    public Task<LaborImportBatchDetailDto?> GetBatchDetailAsync(Guid batchId, CancellationToken ct = default)
        => BuildBatchDetailAsync(batchId, ct);

    public async Task SaveBatchMappingsAsync(Guid batchId, LaborImportBatchMappingsDto mappings, CancellationToken ct = default)
    {
        var batch = await _context.LaborImportBatches.FirstOrDefaultAsync(b => b.Id == batchId, ct);
        if (batch == null)
            throw new InvalidOperationException("Lote de importación no encontrado.");
        if (batch.Status == LaborImportBatchStatus.Completed)
            throw new InvalidOperationException("El lote ya está completo y no admite cambios.");

        batch.SupplyMappingsJson = JsonSerializer.Serialize(mappings.SupplyMappings ?? new(), BatchJsonOptions);
        batch.LaborTypeMappingsJson = JsonSerializer.Serialize(mappings.LaborTypeMappings ?? new(), BatchJsonOptions);
        batch.SupplierMappingsJson = JsonSerializer.Serialize(mappings.SupplierMappings ?? new(), BatchJsonOptions);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<LaborImportBatchResolveResultDto> ImportBatchRowsAsync(Guid batchId, List<int>? rowIndexes, CancellationToken ct = default)
    {
        var batch = await _context.LaborImportBatches
            .Include(b => b.Rows)
            .FirstOrDefaultAsync(b => b.Id == batchId, ct);
        if (batch == null)
            throw new InvalidOperationException("Lote de importación no encontrado.");
        if (batch.Status == LaborImportBatchStatus.Completed)
            throw new InvalidOperationException("El lote ya está completo.");

        var targets = batch.Rows
            .Where(r => r.Resolution == LaborImportRowResolution.Unresolved
                && (rowIndexes == null || rowIndexes.Contains(r.RowIndex)))
            .OrderBy(r => r.RowIndex)
            .ToList();
        if (targets.Count == 0)
        {
            return new LaborImportBatchResolveResultDto
            {
                Success = false,
                BatchCompleted = batch.Status == LaborImportBatchStatus.Completed,
                Errors = new List<string> { "No hay filas pendientes para importar en esta selección." }
            };
        }

        var supplyMappings = FromBatchJson<LaborImportSupplyMappingDto>(batch.SupplyMappingsJson);
        var laborTypeMappings = FromBatchJson<LaborImportTypeMappingDto>(batch.LaborTypeMappingsJson);
        var supplierMappings = FromBatchJson<LaborImportSupplierMappingDto>(batch.SupplierMappingsJson);

        var parsed = targets.Select(ToParsedLabor).ToList();
        ApplyMappingsToLabors(parsed, supplyMappings, laborTypeMappings);

        var campaignLots = await _context.CampaignLots
            .Include(cl => cl.Lot)
            .Include(cl => cl.Rotations)
            .Where(cl => cl.CampaignId == batch.CampaignId)
            .ToListAsync(ct);
        var existingLaborTypes = await _context.LaborTypes.ToListAsync(ct);
        var existingLaborTypeAliases = await _context.LaborTypeAliases.Include(a => a.LaborType).ToListAsync(ct);
        var existingAliases = await _context.SupplyAliases.Include(a => a.Supply).ToListAsync(ct);
        var existingInventories = await _context.Inventories.ToListAsync(ct);
        var existingContacts = await _context.Contacts.ToListAsync(ct);

        var campaignLotIds = campaignLots.Select(cl => cl.Id).ToHashSet();
        var existingLabors = await _context.Labors
            .Include(l => l.Supplies)
            .Where(l => l.CampaignLotId.HasValue && campaignLotIds.Contains(l.CampaignLotId.Value))
            .ToListAsync(ct);

        var counters = new ImportCounters();
        var resolved = ProcessImportMappings(supplyMappings, laborTypeMappings, supplierMappings,
            existingAliases, existingLaborTypes, existingLaborTypeAliases,
            existingInventories, existingContacts, counters, autoCreateUnmatchedSupplies: false);

        var importable = new List<LaborImportParsedLaborDto>();
        var resolveErrors = new List<string>();
        foreach (var p in parsed)
        {
            if (IsRowImportable(p, resolved, existingLaborTypeAliases, supplyMappings, supplierMappings, out var reason))
                importable.Add(p);
            else
                resolveErrors.Add($"Fila {p.RowIndex}: todavía sin match ({reason}). Vinculá el concepto o descartá la fila.");
        }

        var strategy = _context.Database.CreateExecutionStrategy();
        List<(int RowIndex, Guid LaborId)> importedPairs = new();
        await strategy.ExecuteAsync(async () =>
        {
            var isRelational = _context.Database.IsRelational();
            Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? tx = isRelational ? await _context.Database.BeginTransactionAsync(ct) : null;
            try
            {
                if (importable.Count > 0)
                {
                    importedPairs = ImportParsedLaborList(importable, resolved, campaignLots, existingLabors,
                        existingInventories, existingContacts, existingLaborTypeAliases, counters);
                    var importedByRow = importedPairs.ToDictionary(x => x.RowIndex, x => x.LaborId);
                    foreach (var t in targets)
                    {
                        if (importedByRow.TryGetValue(t.RowIndex, out var laborId))
                        {
                            t.Resolution = LaborImportRowResolution.Imported;
                            t.ResultLaborId = laborId;
                            t.ResolvedAt = DateTime.UtcNow;
                        }
                    }
                }

                UpdateBatchCounters(batch);
                await _context.SaveChangesAsync(ct);
                if (tx != null) await tx.CommitAsync(ct);
            }
            catch (Exception ex)
            {
                if (tx != null) await tx.RollbackAsync(ct);
                _logger.LogError(ex, "Error al importar filas del lote {BatchId}.", batchId);
                throw;
            }
            finally
            {
                if (tx != null) await tx.DisposeAsync();
            }
        });

        resolveErrors.AddRange(counters.Errors);
        return new LaborImportBatchResolveResultDto
        {
            Imported = importedPairs.Count,
            Excluded = 0,
            BatchCompleted = batch.Status == LaborImportBatchStatus.Completed,
            Errors = resolveErrors,
            Success = importedPairs.Count > 0
        };
    }

    public async Task<LaborImportBatchResolveResultDto> DiscardBatchRowsAsync(Guid batchId, List<int>? rowIndexes, CancellationToken ct = default)
    {
        var batch = await _context.LaborImportBatches
            .Include(b => b.Rows)
            .FirstOrDefaultAsync(b => b.Id == batchId, ct);
        if (batch == null)
            throw new InvalidOperationException("Lote de importación no encontrado.");
        if (batch.Status == LaborImportBatchStatus.Completed)
            throw new InvalidOperationException("El lote ya está completo.");

        var targets = batch.Rows
            .Where(r => r.Resolution == LaborImportRowResolution.Unresolved
                && (rowIndexes == null || rowIndexes.Contains(r.RowIndex)))
            .ToList();
        if (targets.Count == 0)
        {
            return new LaborImportBatchResolveResultDto
            {
                Success = false,
                BatchCompleted = batch.Status == LaborImportBatchStatus.Completed,
                Errors = new List<string> { "No hay filas pendientes para descartar en esta selección." }
            };
        }

        foreach (var t in targets)
        {
            t.Resolution = LaborImportRowResolution.Excluded;
            t.ResolvedAt = DateTime.UtcNow;
        }
        UpdateBatchCounters(batch);
        await _context.SaveChangesAsync(ct);

        return new LaborImportBatchResolveResultDto
        {
            Imported = 0,
            Excluded = targets.Count,
            BatchCompleted = batch.Status == LaborImportBatchStatus.Completed,
            Success = true
        };
    }

    /// <summary>
    /// Re-evalúa los matches automáticos de un lote contra el estado actual de
    /// los catálogos (por si se dio de alta el lote, el insumo o el contacto en
    /// el padrón después de subir el archivo). No pisa decisiones manuales.
    /// </summary>
    public async Task<LaborImportBatchDetailDto?> ReevaluateBatchAsync(Guid batchId, CancellationToken ct = default)
    {
        var batch = await _context.LaborImportBatches
            .Include(b => b.Rows)
            .FirstOrDefaultAsync(b => b.Id == batchId, ct);
        if (batch == null) return null;
        if (batch.Status == LaborImportBatchStatus.Completed)
            return await BuildBatchDetailAsync(batchId, ct);

        var supplyMappings = FromBatchJson<LaborImportSupplyMappingDto>(batch.SupplyMappingsJson);
        var laborTypeMappings = FromBatchJson<LaborImportTypeMappingDto>(batch.LaborTypeMappingsJson);
        var supplierMappings = FromBatchJson<LaborImportSupplierMappingDto>(batch.SupplierMappingsJson);

        var unresolved = batch.Rows.Where(r => r.Resolution == LaborImportRowResolution.Unresolved).ToList();
        var parsed = unresolved.Select(ToParsedLabor).ToList();

        var existingAliases = await _context.SupplyAliases.Include(a => a.Supply).ToListAsync(ct);
        var existingInventories = await _context.Inventories.ToListAsync(ct);
        var existingLaborTypeAliases = await _context.LaborTypeAliases.Include(a => a.LaborType).ToListAsync(ct);
        var existingLaborTypes = await _context.LaborTypes.ToListAsync(ct);
        var existingContacts = await _context.Contacts.ToListAsync(ct);

        var (freshSupplies, _) = BuildSupplyMappings(parsed, existingAliases, existingInventories);
        var (freshTypes, _) = BuildLaborTypeMappings(parsed, existingLaborTypeAliases, existingLaborTypes);

        var freshSuppliesByName = freshSupplies.ToDictionary(m => m.RawName.Trim(), m => m, StringComparer.OrdinalIgnoreCase);
        foreach (var map in supplyMappings)
        {
            if (map.MatchedSupplyId.HasValue || !string.Equals(map.Action, "Match", StringComparison.OrdinalIgnoreCase) || !string.IsNullOrWhiteSpace(map.NewItemName))
                continue; // decisión manual o alta personalizada: no se toca
            if (freshSuppliesByName.TryGetValue(map.RawName.Trim(), out var fresh) && fresh.MatchedSupplyId.HasValue)
            {
                map.MatchedSupplyId = fresh.MatchedSupplyId;
                map.MatchedSupplyName = fresh.MatchedSupplyName;
                map.Confidence = fresh.Confidence;
                map.ConfidenceLevel = fresh.ConfidenceLevel;
                map.IsFromAlias = fresh.IsFromAlias;
            }
        }

        var freshTypesByName = freshTypes.ToDictionary(m => m.RawName.Trim(), m => m, StringComparer.OrdinalIgnoreCase);
        foreach (var map in laborTypeMappings)
        {
            if (map.MatchedLaborTypeId.HasValue)
                continue;
            if (freshTypesByName.TryGetValue(map.RawName.Trim(), out var fresh))
            {
                if (fresh.MatchedLaborTypeId.HasValue)
                {
                    map.MatchedLaborTypeId = fresh.MatchedLaborTypeId;
                    map.MatchedLaborTypeName = fresh.MatchedLaborTypeName;
                    map.Confidence = fresh.Confidence;
                    map.ConfidenceLevel = fresh.ConfidenceLevel;
                    map.IsFromAlias = fresh.IsFromAlias;
                }
                map.SuggestedLaborTypeId = fresh.SuggestedLaborTypeId;
                map.SuggestedLaborTypeName = fresh.SuggestedLaborTypeName;
            }
        }

        // Proveedores: re-match automático donde el usuario no decidió nada.
        foreach (var row in unresolved)
        {
            var supplies = FromBatchJson<LaborImportParsedItemDto>(row.SuppliesJson);
            bool changed = false;
            foreach (var s in supplies)
            {
                if (string.IsNullOrWhiteSpace(s.SupplierRawName) || s.SupplierContactId.HasValue)
                    continue;
                var sm = supplierMappings.FirstOrDefault(m => string.Equals(m.RawName.Trim(), s.SupplierRawName!.Trim(), StringComparison.OrdinalIgnoreCase));
                if (sm != null && (!string.Equals(sm.Action, "Match", StringComparison.OrdinalIgnoreCase) || sm.MatchedContactId.HasValue))
                    continue;
                var (contactId, matchedName, _) = MatchContact(s.SupplierRawName, existingContacts);
                if (contactId.HasValue)
                {
                    s.SupplierContactId = contactId;
                    s.MatchedSupplierName = matchedName;
                    changed = true;
                }
            }
            if (changed)
                row.SuppliesJson = JsonSerializer.Serialize(supplies, BatchJsonOptions);

            // Responsable: no tiene capa de mapping, se re-matchea directo en la fila.
            if (!string.IsNullOrWhiteSpace(row.Contractor) && !row.ContactId.HasValue && !IsPropioLike(row.Contractor))
            {
                var (contactId, matchedName, isExternal) = MatchContact(row.Contractor, existingContacts);
                if (contactId.HasValue)
                {
                    row.ContactId = contactId;
                    row.MatchedContactName = matchedName;
                    row.IsExternalBilling = isExternal;
                }
            }

            // Tipo por nombre exacto o alias nuevo (la vinculación por mapping
            // se resuelve al importar; esto es solo para mostrar la fila al día).
            if (!row.LaborTypeId.HasValue && !string.IsNullOrWhiteSpace(row.LaborTypeName))
            {
                string normLt = NormalizeString(row.LaborTypeName);
                var aliasMatch = existingLaborTypeAliases.FirstOrDefault(a => string.Equals(a.NormalizedName, normLt, StringComparison.OrdinalIgnoreCase));
                if (aliasMatch != null)
                    row.LaborTypeId = aliasMatch.LaborTypeId;
                else
                {
                    var exact = existingLaborTypes.FirstOrDefault(t => string.Equals(NormalizeString(t.Name), normLt, StringComparison.OrdinalIgnoreCase));
                    if (exact != null)
                        row.LaborTypeId = exact.Id;
                }
            }
        }

        batch.SupplyMappingsJson = JsonSerializer.Serialize(supplyMappings, BatchJsonOptions);
        batch.LaborTypeMappingsJson = JsonSerializer.Serialize(laborTypeMappings, BatchJsonOptions);
        batch.SupplierMappingsJson = JsonSerializer.Serialize(supplierMappings, BatchJsonOptions);
        await _context.SaveChangesAsync(ct);

        return await BuildBatchDetailAsync(batchId, ct);
    }

    public async Task<int> GetPendingCountAsync(Guid campaignId, CancellationToken ct = default)
    {
        return await _context.LaborImportBatches
            .Where(b => b.CampaignId == campaignId && b.Status == LaborImportBatchStatus.Pending)
            .SumAsync(b => b.PendingCount, ct);
    }

    #endregion

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
        /// <summary>Superficie presupuestada: solo se usa si la columna real vino vacía.</summary>
        public int ColSuperficieFallback { get; init; }
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

            int colFecha = 0, colEst = 0, colLote = 0, colSup = 0, colSupReal = 0, colItem = 0, colDosis = 0, colTipo = 0, colUnidad = 0, colTotal = 0, colContr = 0, colModo = 0;
            bool hasProducLabor = false;

            for (int c = 1; c <= lastCol; c++)
            {
                // Los encabezados reales vienen con saltos de línea adentro ("Contr/\nProve",
                // "real/\npresup"): sin aplanarlos, el Contains nunca matchea y la columna
                // queda en 0, que el parser interpreta como "no existe" y la ignora en
                // silencio. `header` aplana los espacios; `compact` los saca del todo para
                // los nombres partidos al medio.
                string header = CollapseWhitespace(row.Cell(c).GetString().ToLowerInvariant());
                if (string.IsNullOrWhiteSpace(header)) continue;
                string compact = header.Replace(" ", string.Empty);

                if (compact.Contains("produc/labor") || header.Contains("labor o insumo"))
                {
                    colItem = c;
                    hasProducLabor = true;
                }
                else if (header == "tipo") colTipo = c;
                // "Fecha-1" también empieza con "fecha": la exacta manda y no la pisa
                // ninguna variante posterior.
                else if (header == "fecha") colFecha = c;
                else if (colFecha == 0 && header.StartsWith("fecha")) colFecha = c;
                else if (header == "lote") colLote = c;
                else if (header == "establecimiento" || header == "campo") colEst = c;
                // "Sup" es la superficie presupuestada y "Sup. Real" la que se trabajó de
                // verdad (OT-60): si están las dos, gana la real.
                else if (compact == "sup.real" || compact == "supreal" || compact.StartsWith("superficiereal")) colSupReal = c;
                else if (header == "sup" || header.StartsWith("superficie")) colSup = c;
                else if (header == "dosis") colDosis = c;
                else if (header == "unidad") colUnidad = c;
                else if (header == "total") colTotal = c;
                else if (compact.Contains("contr/prove") || compact.Contains("contratista") || compact.Contains("maquinaria")) colContr = c;
                else if (compact.Contains("real/presup") || header == "modo" || header == "estado") colModo = c;
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
                    ColSuperficie = colSupReal > 0 ? colSupReal : colSup,
                    ColSuperficieFallback = colSupReal > 0 ? colSup : 0,
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
                decimal sup = ReadSuperficie(row, cfg);
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
                // This is a Supply row.
                // La columna "Contr/Prove" es la misma que en la fila de Labor, pero en una fila
                // de insumo representa al PROVEEDOR de ese insumo puntual, no al responsable de
                // la labor (confirmado con datos reales AMSA: en la fila "Labor" vale "Propio",
                // en las filas de insumos que siguen vale el nombre del distribuidor, ej. "Ekun").
                string supplyRowContractorRaw = cfg.ColContratista > 0 ? row.Cell(cfg.ColContratista).GetString().Trim() : string.Empty;

                if (currentLabor == null)
                {
                    // Orphaned supply line, try to synthesize a labor if lot is present
                    string orphanLot = cfg.ColLote > 0 ? row.Cell(cfg.ColLote).GetString().Trim() : string.Empty;
                    DateTime? orphanDate = cfg.ColFecha > 0 ? ParseDateCell(row.Cell(cfg.ColFecha)) : null;
                    bool orphanRealized = !orphanDate.HasValue || orphanDate.Value.Date <= DateTime.UtcNow.Date;
                    string orphanMode = orphanRealized ? "Realized" : "Planned";

                    var (orphanContactId, orphanMatchedName, orphanExternal) = MatchContact(supplyRowContractorRaw, contacts);

                    currentLabor = new LaborImportParsedLaborDto
                    {
                        RowIndex = r,
                        Date = orphanDate,
                        FieldName = cfg.ColEstablecimiento > 0 ? row.Cell(cfg.ColEstablecimiento).GetString().Trim() : string.Empty,
                        LotName = orphanLot,
                        Hectares = ReadSuperficie(row, cfg),
                        LaborTypeName = "Labor General",
                        Contractor = supplyRowContractorRaw,
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

                var (supplierContactId, matchedSupplierName, _) = MatchContact(supplyRowContractorRaw, contacts);

                currentLabor.Supplies.Add(new LaborImportParsedItemDto
                {
                    SupplyName = producLabor,
                    Dose = dose,
                    Unit = unit,
                    Total = total > 0 ? total : null,
                    Category = tipo,
                    SupplierRawName = string.IsNullOrWhiteSpace(supplyRowContractorRaw) ? null : supplyRowContractorRaw,
                    SupplierContactId = supplierContactId,
                    MatchedSupplierName = matchedSupplierName
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
        List<LaborTypeAlias> aliases,
        List<LaborType> laborTypes)
    {
        var typesGrouped = labors
            .Where(l => !string.IsNullOrWhiteSpace(l.LaborTypeName))
            .GroupBy(l => l.LaborTypeName.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToList();

        var aliasesByNorm = new Dictionary<string, LaborTypeAlias>(StringComparer.OrdinalIgnoreCase);
        foreach (var a in aliases)
        {
            if (!string.IsNullOrWhiteSpace(a.NormalizedName) && !aliasesByNorm.ContainsKey(a.NormalizedName))
            {
                aliasesByNorm[a.NormalizedName] = a;
            }
        }

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

            // Tier 0: Check LaborTypeAlias (100% confidence)
            if (aliasesByNorm.TryGetValue(normRaw, out var alias) && alias.LaborType != null)
            {
                result.Add(new LaborImportTypeMappingDto
                {
                    RawName = rawName,
                    NormalizedName = normRaw,
                    MatchedLaborTypeId = alias.LaborTypeId,
                    MatchedLaborTypeName = alias.LaborType.Name,
                    Confidence = 1.0,
                    ConfidenceLevel = "High",
                    IsFromAlias = true,
                    Occurrences = occurrences,
                    Action = "Match"
                });
                continue;
            }

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
                    IsFromAlias = false,
                    Occurrences = occurrences,
                    Action = "Match"
                });
                continue;
            }

            // Tier 2: Suggest possible match only! (Never auto-link without user action unless exact or alias)
            double bestScore = 0;
            LaborType? bestLt = null;

            foreach (var lt in laborTypes)
            {
                string normLt = NormalizeString(lt.Name);
                double score = CalculateSimilarity(normRaw, normLt);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestLt = lt;
                }
            }

            unmatchedCount++;
            if (bestScore >= 0.50 && bestLt != null)
            {
                result.Add(new LaborImportTypeMappingDto
                {
                    RawName = rawName,
                    NormalizedName = normRaw,
                    MatchedLaborTypeId = null, // Must be linked by user
                    MatchedLaborTypeName = null,
                    SuggestedLaborTypeId = bestLt.Id,
                    SuggestedLaborTypeName = bestLt.Name,
                    Confidence = Math.Round(bestScore, 2),
                    ConfidenceLevel = "Medium",
                    IsFromAlias = false,
                    Occurrences = occurrences,
                    Action = "Match"
                });
            }
            else
            {
                result.Add(new LaborImportTypeMappingDto
                {
                    RawName = rawName,
                    NormalizedName = normRaw,
                    MatchedLaborTypeId = null,
                    MatchedLaborTypeName = null,
                    SuggestedLaborTypeId = null,
                    SuggestedLaborTypeName = null,
                    Confidence = 0,
                    ConfidenceLevel = "None",
                    IsFromAlias = false,
                    Occurrences = occurrences,
                    Action = "Match"
                });
            }
        }

        // Sort: Non-matches first (so user sees them right away), then alphabetically
        return (result.OrderBy(r => r.ConfidenceLevel == "None" ? 0 : (r.ConfidenceLevel == "Medium" ? 1 : 2))
                      .ThenBy(r => r.RawName)
                      .ToList(), unmatchedCount);
    }

    /// <summary>
    /// Agrupa los proveedores de insumos detectados (columna "Contr/Prove" leída en filas de
    /// insumo, no de labor) contra el padrón de Contacts, usando el mismo matcheo por nombre
    /// libre que ya se usa para el responsable de la labor (MatchContact). No bloquea el import:
    /// un proveedor sin coincidencia queda con Action "Match" y MatchedContactId null, para que
    /// se muestre en la vista previa y el usuario lo corrija o lo deje sin asignar.
    /// </summary>
    private (List<LaborImportSupplierMappingDto> Mappings, int UnmatchedCount) BuildSupplierMappings(
        List<LaborImportParsedLaborDto> labors)
    {
        var suppliersGrouped = labors
            .SelectMany(l => l.Supplies)
            .Where(s => !string.IsNullOrWhiteSpace(s.SupplierRawName))
            .GroupBy(s => s.SupplierRawName!.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToList();

        var result = new List<LaborImportSupplierMappingDto>();
        int unmatchedCount = 0;

        foreach (var group in suppliersGrouped)
        {
            string rawName = group.Key;
            string normRaw = NormalizeString(rawName);
            int occurrences = group.Count();
            var first = group.First();

            bool matched = first.SupplierContactId.HasValue;
            if (!matched) unmatchedCount++;

            result.Add(new LaborImportSupplierMappingDto
            {
                RawName = rawName,
                NormalizedName = normRaw,
                MatchedContactId = first.SupplierContactId,
                MatchedContactName = first.MatchedSupplierName,
                Confidence = matched ? 1.0 : 0.0,
                ConfidenceLevel = matched ? "High" : "None",
                Occurrences = occurrences,
                Action = "Match"
            });
        }

        return (result.OrderBy(r => r.ConfidenceLevel == "None" ? 0 : 1)
                      .ThenBy(r => r.RawName)
                      .ToList(), unmatchedCount);
    }

    #endregion

    #region String & Number Helpers

    /// <summary>
    /// Aplana el encabezado: recorta, colapsa cualquier corrida de espacios (incluidos
    /// los saltos de línea que Excel mete dentro de una celda de título) en uno solo.
    /// </summary>
    private static string CollapseWhitespace(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        return Regex.Replace(text, @"\s+", " ").Trim();
    }

    /// <summary>
    /// Superficie real de la fila; si la planilla trae la columna real vacía (típico en
    /// labores planeadas) cae a la presupuestada.
    /// </summary>
    private static decimal ReadSuperficie(IXLRow row, ColumnConfig cfg)
    {
        decimal sup = cfg.ColSuperficie > 0 ? ParseDecimalCell(row.Cell(cfg.ColSuperficie)) : 0;
        if (sup == 0 && cfg.ColSuperficieFallback > 0)
            sup = ParseDecimalCell(row.Cell(cfg.ColSuperficieFallback));
        return sup;
    }

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
