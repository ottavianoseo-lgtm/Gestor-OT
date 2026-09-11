using GestorOT.Application.Interfaces;
using GestorOT.Domain.Entities;
using GestorOT.Domain.Enums;
using GestorOT.Shared.Dtos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GestorOT.Infrastructure.Services;

public sealed class PaseBuilderService : IPaseBuilderService
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<PaseBuilderService> _logger;

    public PaseBuilderService(
        IApplicationDbContext context,
        ILogger<PaseBuilderService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<PaseLoteResult> GenerarLoteAsync(
        Guid tenantId,
        List<Guid>? workOrderIds,
        List<Guid>? laborIds,
        string? descripcion = null,
        CancellationToken ct = default)
    {
        workOrderIds ??= new List<Guid>();
        laborIds ??= new List<Guid>();

        if (!workOrderIds.Any() && !laborIds.Any())
        {
            return new PaseLoteResult(Guid.Empty, 0, new List<string> { "No se especificaron OTs ni Labores." }, false, "Seleccione al menos una OT o labor.");
        }

        // Fetch labors directly selected OR belonging to selected WorkOrders
        var laborsFromWos = await _context.Labors
            .Include(l => l.Type)
            .Include(l => l.WorkOrder)
            .Include(l => l.Contact)
            .Include(l => l.Lot)
            .Include(l => l.CampaignLot)
            .Include(l => l.Supplies).ThenInclude(s => s.Supply)
            .Where(l => l.TenantId == tenantId && l.WorkOrderId != null && workOrderIds.Contains(l.WorkOrderId.Value))
            .ToListAsync(ct);

        var laborsDirect = await _context.Labors
            .Include(l => l.Type)
            .Include(l => l.WorkOrder)
            .Include(l => l.Contact)
            .Include(l => l.Lot)
            .Include(l => l.CampaignLot)
            .Include(l => l.Supplies).ThenInclude(s => s.Supply)
            .Where(l => l.TenantId == tenantId && laborIds.Contains(l.Id))
            .ToListAsync(ct);

        var allLabors = laborsFromWos.Concat(laborsDirect).DistinctBy(l => l.Id).ToList();

        if (!allLabors.Any())
        {
            return new PaseLoteResult(Guid.Empty, 0, new List<string> { "No se encontraron labores asociadas a los IDs especificados." }, false, "No hay labores para procesar.");
        }

        // Load account configurations
        var configs = await _context.AccountConfigurations
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId && c.IsActive)
            .ToListAsync(ct);

        var configsById = configs.ToDictionary(c => c.Id);

        var defaultConfigs = configs.Where(c => c.LaborTypeId == null).ToList();
        var defaultConfig = defaultConfigs.FirstOrDefault() ?? configs.FirstOrDefault();
        
        // Default fallback configuration if no custom configuration has been created yet
        defaultConfig ??= new AccountConfiguration
        {
            TenantId = tenantId,
            CodEmpresa = 1,
            CodComprobante = 1,
            PuntoVenta = 1,
            CodMoneda = 1,
            DebitAccountCode = "1000",
            CreditAccountCode = "2000",
            IsActive = true
        };

        // Load ERP concepts
        var erpConcepts = await _context.ErpConcepts
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId)
            .ToListAsync(ct);

        // Load ERP people
        var erpPeople = await _context.ErpPeople
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId)
            .ToListAsync(ct);

        var now = DateTime.UtcNow;
        var lote = new PaseLote
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            GeneradoEn = now,
            Descripcion = descripcion,
            Estado = "Generado"
        };

        var pases = new List<PaseImputacion>();
        var warnings = new List<string>();

        int seqGroup = 1;

        foreach (var labor in allLabors)
        {
            // Cadena de precedencia, de lo mas especifico a lo mas general:
            //   1. la regla que la labor fija a mano
            //   2. la del tipo de labor acotada al modo (propia / contratista)
            //   3. la del tipo de labor sin modo
            //   4. la general
            // Antes era un FirstOrDefault() sobre las del tipo: con mas de una regla elegia
            // una arbitraria y en silencio.
            AccountConfiguration? config = ResolveConfig(
                labor, configsById, configs, defaultConfig, warnings);

            if (config == null)
            {
                warnings.Add($"Labor {labor.Id} ({labor.Type?.Name ?? "Sin Tipo"}): Sin AccountConfiguration configurada. Se omitió.");
                continue;
            }

            if (!config.CodEmpresa.HasValue || !config.CodComprobante.HasValue || !config.CodMoneda.HasValue)
            {
                warnings.Add($"Labor {labor.Id}: AccountConfiguration incompleta (falta CodEmpresa, CodComprobante o CodMoneda). Se omitió.");
                continue;
            }

            // El centro de costo es el lote, no el tipo de labor. Se resuelve como en
            // Ganaderia (registro pisa plantilla): la campania pisa al lote, y el lote pisa a
            // la config. Sin esto toda labor del mismo tipo imputaba al mismo centro sin
            // importar donde se hizo, y el pase no servia para costo por lote.
            long? codCentroDebe = labor.CampaignLot?.CodCentro
                ?? labor.Lot?.CodCentro
                ?? config.CodCuentaDebeCentro;
            long? codCentroHaber = labor.CampaignLot?.CodCentro
                ?? labor.Lot?.CodCentro
                ?? config.CodCuentaHaberCentro;

            if (!config.NoImputaCentro && codCentroDebe is null && codCentroHaber is null)
            {
                warnings.Add($"Labor {labor.Id} ({labor.Type?.Name ?? "Sin Tipo"}) en lote {labor.Lot?.Name ?? "S/N"}: el comprobante imputa centro pero el lote no tiene centro asignado y la regla contable tampoco. Asignale el centro al lote.");
            }

            // Resolve ErpConcept for LaborType
            long codConcepto = 0;
            string? codigoConcepto = null;

            if (labor.Type != null)
            {
                var matchedConcept = erpConcepts.FirstOrDefault(c =>
                    string.Equals(c.ExternalErpId, labor.Type.ExternalErpId, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(c.Description, labor.Type.Name, StringComparison.OrdinalIgnoreCase));

                if (matchedConcept != null && long.TryParse(matchedConcept.ExternalErpId, out var parsedConceptId))
                {
                    codConcepto = parsedConceptId;
                }
                else if (!string.IsNullOrEmpty(labor.Type.ExternalErpId) && long.TryParse(labor.Type.ExternalErpId, out var directConceptId))
                {
                    codConcepto = directConceptId;
                }
            }

            // Resolve CodPersona. Se prioriza el vinculo explicito Contact -> ErpPerson y el
            // codigo que el contacto ya tiene guardado; el matcheo por nombre queda como ultimo
            // recurso porque dos personas homonimas en el ERP imputan a la cuenta equivocada.
            long? codPersona = config.CodPersona;
            if (labor.Contact != null)
            {
                ErpPerson? matchedPerson = null;

                if (labor.Contact.ErpPersonId is Guid erpPersonId)
                {
                    matchedPerson = erpPeople.FirstOrDefault(p => p.Id == erpPersonId);
                }

                matchedPerson ??= erpPeople.FirstOrDefault(p =>
                    !string.IsNullOrEmpty(labor.Contact.ExternalErpId) &&
                    string.Equals(p.ExternalErpId, labor.Contact.ExternalErpId, StringComparison.OrdinalIgnoreCase));

                matchedPerson ??= erpPeople.FirstOrDefault(p =>
                    string.Equals(p.FullName, labor.Contact.FullName, StringComparison.OrdinalIgnoreCase));

                if (matchedPerson != null && long.TryParse(matchedPerson.ExternalErpId, out var parsedPersonId))
                {
                    codPersona = parsedPersonId;
                }
            }

            var refId = labor.WorkOrder != null && !string.IsNullOrEmpty(labor.WorkOrder.OTNumber)
                ? labor.WorkOrder.OTNumber
                : labor.Id.ToString()[..8];

            var fechaImputacion = labor.ExecutionDate ?? labor.EstimatedDate ?? DateTime.UtcNow;

            var paseLabor = new PaseImputacion
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                PaseLoteId = lote.Id,
                WorkOrderId = labor.WorkOrderId,
                LaborId = labor.Id,
                IdReferencia = refId,
                IdAgrupacionPase = seqGroup,
                CodEmpresa = config.CodEmpresa.Value,
                CodComprobante = config.CodComprobante.Value,
                NoImputaGestion = config.NoImputaGestion,
                NoImputaContabilidad = config.NoImputaContabilidad,
                NoImputaCentro = config.NoImputaCentro,
                NoImputaAuxiliar = config.NoImputaAuxiliar,
                PuntoVenta = config.PuntoVenta,
                Fecha = fechaImputacion,
                CodPersona = codPersona,
                CodMoneda = config.CodMoneda.Value,
                CodListaDePrecios = null,
                CodConcepto = codConcepto,
                CodigoConcepto = codigoConcepto,
                CantidadAuxiliar = labor.Hectares > 0 ? labor.Hectares : null,
                Cantidad = labor.EffectiveArea > 0 ? labor.EffectiveArea : labor.Hectares,
                Precio = labor.Rate,
                CodPerfilImputacionDebe = config.CodPerfilDebe,
                CodPerfilImputacionHaber = config.CodPerfilHaber,
                CodCuentaDebeGestion = config.CodCuentaDebeGestion,
                CodCuentaHaberGestion = config.CodCuentaHaberGestion,
                CodCuentaDebeCentro = codCentroDebe,
                CodCuentaHaberCentro = codCentroHaber,
                CodCuentaDebeContabilidad = config.CodCuentaDebeContabilidad,
                CodCuentaHaberContabilidad = config.CodCuentaHaberContabilidad,
                CodCuentaDebeAuxiliar = config.CodCuentaDebeAuxiliar,
                CodCuentaHaberAuxiliar = config.CodCuentaHaberAuxiliar,
                Notas = $"Labor: {labor.Type?.Name} | Lote: {labor.Lot?.Name ?? "S/N"}"
            };

            pases.Add(paseLabor);

            // Also check supplies if available
            foreach (var supply in labor.Supplies)
            {
                if (supply.Supply == null) continue;

                long supplyConceptId = 0;
                if (long.TryParse(supply.Supply.ExternalErpId, out var parsedSupplyConcept))
                {
                    supplyConceptId = parsedSupplyConcept;
                }

                var cantSupply = supply.RealTotal ?? supply.CalculatedTotal ?? supply.PlannedTotal;

                var paseSupply = new PaseImputacion
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    PaseLoteId = lote.Id,
                    WorkOrderId = labor.WorkOrderId,
                    LaborId = labor.Id,
                    IdReferencia = refId,
                    IdAgrupacionPase = seqGroup,
                    CodEmpresa = config.CodEmpresa.Value,
                    CodComprobante = config.CodComprobante.Value,
                    NoImputaGestion = config.NoImputaGestion,
                    NoImputaContabilidad = config.NoImputaContabilidad,
                    NoImputaCentro = config.NoImputaCentro,
                    NoImputaAuxiliar = config.NoImputaAuxiliar,
                    PuntoVenta = config.PuntoVenta,
                    Fecha = fechaImputacion,
                    CodPersona = codPersona,
                    CodMoneda = config.CodMoneda.Value,
                    CodConcepto = supplyConceptId,
                    CodigoConcepto = null,
                    CantidadAuxiliar = supply.RealDose ?? supply.PlannedDose,
                    Cantidad = cantSupply,
                    Precio = 0, // Insumo consumido
                    CodPerfilImputacionDebe = config.CodPerfilDebe,
                    CodPerfilImputacionHaber = config.CodPerfilHaber,
                    CodCuentaDebeGestion = config.CodCuentaDebeGestion,
                    CodCuentaHaberGestion = config.CodCuentaHaberGestion,
                    CodCuentaDebeCentro = codCentroDebe,
                    CodCuentaHaberCentro = codCentroHaber,
                    CodCuentaDebeContabilidad = config.CodCuentaDebeContabilidad,
                    CodCuentaHaberContabilidad = config.CodCuentaHaberContabilidad,
                    CodCuentaDebeAuxiliar = config.CodCuentaDebeAuxiliar,
                    CodCuentaHaberAuxiliar = config.CodCuentaHaberAuxiliar,
                    Notas = $"Insumo: {supply.Supply.ItemName} en Labor {labor.Type?.Name}"
                };

                pases.Add(paseSupply);
            }

            seqGroup++;

        }

        if (!pases.Any())
        {
            return new PaseLoteResult(Guid.Empty, 0, warnings, false, "Ninguna labor pudo generar un pase válido. Verifique las configuraciones de cuenta.");
        }

        lote.TotalPases = pases.Count;

        _context.PasesLote.Add(lote);
        // El idAgrupacionPase separa un mismo origen en varios pases del G4. Se calcula
        // agrupando, como en el modulo oficial de Ganaderia (PaseBuilder.AssignGroups): con el
        // contador por labor, una labor y su insumo con distinto comprobante o moneda caian en
        // el mismo pase y el G4 lo rechaza.
        AssignGroups(pases);

        _context.PasesImputacion.AddRange(pases);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Lote Pases G4 generado | LoteId={LoteId} Pases={PasesCount}", lote.Id, pases.Count);

        return new PaseLoteResult(lote.Id, pases.Count, warnings, true);
    }

    public async Task<IReadOnlyList<PaseLoteDto>> GetLotesAsync(Guid tenantId, CancellationToken ct = default)
    {
        var lotes = await _context.PasesLote
            .AsNoTracking()
            .Where(l => l.TenantId == tenantId)
            .OrderByDescending(l => l.GeneradoEn)
            .ToListAsync(ct);

        return lotes.Select(l => new PaseLoteDto(
            l.Id, l.TenantId, l.GeneradoEn, l.Descripcion,
            l.TotalPases, l.Estado, new List<PaseImputacionDto>())).ToList();
    }

    public async Task<PaseLoteDto?> GetLoteAsync(Guid tenantId, Guid loteId, CancellationToken ct = default)
    {
        var lote = await _context.PasesLote
            .AsNoTracking()
            .Include(l => l.Pases)
            .Where(l => l.TenantId == tenantId && l.Id == loteId)
            .FirstOrDefaultAsync(ct);

        if (lote == null) return null;

        return new PaseLoteDto(
            lote.Id, lote.TenantId, lote.GeneradoEn, lote.Descripcion,
            lote.TotalPases, lote.Estado,
            lote.Pases.Select(p => new PaseImputacionDto(
                p.Id, p.PaseLoteId, p.WorkOrderId, p.LaborId,
                p.IdReferencia, p.IdAgrupacionPase,
                p.CodEmpresa, p.CodComprobante,
                p.NoImputaGestion, p.NoImputaContabilidad, p.NoImputaCentro, p.NoImputaAuxiliar,
                p.PuntoVenta, p.NumeroComprobante, p.Fecha,
                p.CodPersona, p.CodMoneda, p.CodListaDePrecios, p.CodConcepto, p.CodigoConcepto,
                p.CantidadAuxiliar, p.Cantidad, p.Precio,
                p.CodPerfilImputacionDebe, p.CodPerfilImputacionHaber,
                p.CodCuentaDebeGestion, p.CodCuentaHaberGestion,
                p.CodCuentaDebeCentro, p.CodCuentaHaberCentro,
                p.CodCuentaDebeContabilidad, p.CodCuentaHaberContabilidad,
                p.CodCuentaDebeAuxiliar, p.CodCuentaHaberAuxiliar,
                p.Notas)).ToList());
    }

    public async Task<IReadOnlyList<PendingImputacionItemDto>> GetPendingItemsAsync(Guid tenantId, CancellationToken ct = default)
    {
        var items = new List<PendingImputacionItemDto>();

        // 1. Work Orders with executed/realized labors
        var wos = await _context.WorkOrders
            .AsNoTracking()
            .Include(w => w.Labors).ThenInclude(l => l.Type)
            .Where(w => w.TenantId == tenantId)
            .ToListAsync(ct);

        foreach (var wo in wos)
        {
            var executedLaborsCount = wo.Labors.Count(l => l.Status == LaborStatus.Realized || l.Status == LaborStatus.Validated);
            var totalArea = wo.Labors.Sum(l => l.EffectiveArea > 0 ? l.EffectiveArea : l.Hectares);
            var identifier = !string.IsNullOrEmpty(wo.OTNumber) ? $"OT #{wo.OTNumber}" : (wo.Name ?? "OT sin nombre");

            items.Add(new PendingImputacionItemDto(
                wo.Id,
                "WorkOrder",
                identifier,
                wo.PlannedDate,
                $"{wo.Labors.Count} labores ({executedLaborsCount} ejec.)",
                totalArea,
                wo.Status
            ));
        }

        // 2. Standalone labors (Labores sueltas)
        var standaloneLabors = await _context.Labors
            .AsNoTracking()
            .Include(l => l.Type)
            .Include(l => l.Lot)
            .Include(l => l.CampaignLot)
            .Where(l => l.TenantId == tenantId && l.WorkOrderId == null)
            .ToListAsync(ct);

        foreach (var labor in standaloneLabors)
        {
            var area = labor.EffectiveArea > 0 ? labor.EffectiveArea : labor.Hectares;
            var laborTypeName = labor.Type?.Name ?? "Labor General";
            var lotName = labor.Lot?.Name ?? "Sin Lote";

            items.Add(new PendingImputacionItemDto(
                labor.Id,
                "Labor",
                $"{laborTypeName} ({lotName})",
                labor.ExecutionDate ?? labor.EstimatedDate ?? labor.CreatedAt,
                $"Labor suelta en {lotName}",
                area,
                labor.Status.ToString()
            ));
        }

        return items.OrderByDescending(i => i.Date).ToList();
    }

    public async Task<IReadOnlyList<AccountConfigurationDto>> GetAccountConfigurationsAsync(Guid tenantId, CancellationToken ct = default)
    {
        var configs = await _context.AccountConfigurations
            .AsNoTracking()
            .Include(c => c.LaborType)
            .Include(c => c.ErpActivity)
            .Where(c => c.TenantId == tenantId)
            .ToListAsync(ct);

        return configs.Select(c => new AccountConfigurationDto(
            c.Id, c.TenantId, c.LaborTypeId, c.LaborType?.Name,
            c.DebitAccountCode, c.CreditAccountCode, c.Description, c.IsActive,
            c.CodEmpresa, c.CodComprobante, c.PuntoVenta, c.CodMoneda,
            c.CodPerfilDebe, c.CodPerfilHaber, c.CodPersona,
            c.NoImputaGestion, c.NoImputaContabilidad, c.NoImputaCentro, c.NoImputaAuxiliar,
            c.CodCuentaDebeGestion, c.CodCuentaHaberGestion,
            c.CodCuentaDebeCentro, c.CodCuentaHaberCentro,
            c.CodCuentaDebeContabilidad, c.CodCuentaHaberContabilidad,
            c.CodCuentaDebeAuxiliar, c.CodCuentaHaberAuxiliar
        )
        {
            ErpActivityId = c.ErpActivityId,
            ErpActivityName = c.ErpActivity?.Name,
            ExecutionMode = c.ExecutionMode
        }).ToList();
    }

    public async Task SaveAccountConfigurationAsync(Guid tenantId, AccountConfigurationDto dto, CancellationToken ct = default)
    {
        var entity = await _context.AccountConfigurations
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == dto.Id, ct);

        if (entity == null)
        {
            entity = new AccountConfiguration
            {
                Id = dto.Id == Guid.Empty ? Guid.NewGuid() : dto.Id,
                TenantId = tenantId
            };
            _context.AccountConfigurations.Add(entity);
        }

        entity.LaborTypeId = dto.LaborTypeId;
        entity.ErpActivityId = dto.ErpActivityId;
        entity.ExecutionMode = dto.ExecutionMode;
        entity.DebitAccountCode = dto.DebitAccountCode;
        entity.CreditAccountCode = dto.CreditAccountCode;
        entity.Description = dto.Description;
        entity.IsActive = dto.IsActive;
        entity.CodEmpresa = dto.CodEmpresa;
        entity.CodComprobante = dto.CodComprobante;
        entity.PuntoVenta = dto.PuntoVenta;
        entity.CodMoneda = dto.CodMoneda;
        entity.CodPerfilDebe = dto.CodPerfilDebe;
        entity.CodPerfilHaber = dto.CodPerfilHaber;
        entity.CodPersona = dto.CodPersona;
        entity.NoImputaGestion = dto.NoImputaGestion;
        entity.NoImputaContabilidad = dto.NoImputaContabilidad;
        entity.NoImputaCentro = dto.NoImputaCentro;
        entity.NoImputaAuxiliar = dto.NoImputaAuxiliar;
        entity.CodCuentaDebeGestion = dto.CodCuentaDebeGestion;
        entity.CodCuentaHaberGestion = dto.CodCuentaHaberGestion;
        entity.CodCuentaDebeCentro = dto.CodCuentaDebeCentro;
        entity.CodCuentaHaberCentro = dto.CodCuentaHaberCentro;
        entity.CodCuentaDebeContabilidad = dto.CodCuentaDebeContabilidad;
        entity.CodCuentaHaberContabilidad = dto.CodCuentaHaberContabilidad;
        entity.CodCuentaDebeAuxiliar = dto.CodCuentaDebeAuxiliar;
        entity.CodCuentaHaberAuxiliar = dto.CodCuentaHaberAuxiliar;

        await _context.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Resuelve que regla contable le corresponde a una labor.
    ///
    /// Una regla declara hasta tres dimensiones opcionales: tipo de labor, actividad del ERP y
    /// modo propia/contratista. Aplica a la labor si todas las que tiene declaradas coinciden;
    /// las que deja en null son comodines. Entre las que aplican gana la mas especifica, o sea
    /// la que declara mas dimensiones, asi que una regla de (COSECHA + SOJA) le gana a una de
    /// (COSECHA) y esa a la general.
    ///
    /// Se prefiere esto a una escalera de casos fijos porque cada dimension nueva multiplicaria
    /// las ramas, y porque permite combinaciones que una escalera no cubre, como una regla que
    /// aplique a una actividad entera sin importar la tarea.
    ///
    /// Si empatan dos reglas igual de especificas avisa, en vez de elegir una en silencio como
    /// hacia el FirstOrDefault() anterior.
    /// </summary>
    private static AccountConfiguration? ResolveConfig(
        Labor labor,
        Dictionary<Guid, AccountConfiguration> configsById,
        List<AccountConfiguration> configs,
        AccountConfiguration? defaultConfig,
        List<string> warnings)
    {
        var etiqueta = $"Labor {labor.Id} ({labor.Type?.Name ?? "Sin Tipo"})";

        // El override explicito de la labor gana sobre cualquier regla.
        if (labor.AccountConfigurationId is Guid explicitId)
        {
            if (configsById.TryGetValue(explicitId, out var explicitConfig))
            {
                return explicitConfig;
            }

            warnings.Add($"{etiqueta}: tiene una regla contable asignada que ya no existe o esta inactiva. Se resolvio por las reglas generales.");
        }

        var modo = labor.IsExternalBilling
            ? LaborExecutionMode.Contractor
            : LaborExecutionMode.Own;

        var aplicables = configs
            .Where(c => c.LaborTypeId is null || c.LaborTypeId == labor.LaborTypeId)
            .Where(c => c.ErpActivityId is null || c.ErpActivityId == labor.ErpActivityId)
            .Where(c => c.ExecutionMode is null || c.ExecutionMode == modo)
            .ToList();

        if (aplicables.Count == 0)
        {
            return defaultConfig;
        }

        var maxEspecificidad = aplicables.Max(Especificidad);
        var ganadoras = aplicables.Where(c => Especificidad(c) == maxEspecificidad).ToList();

        if (ganadoras.Count > 1)
        {
            var nombres = string.Join(", ", ganadoras.Select(c =>
                string.IsNullOrWhiteSpace(c.Description) ? c.Id.ToString()[..8] : c.Description));
            warnings.Add($"{etiqueta}: hay {ganadoras.Count} reglas contables igual de especificas que aplican ({nombres}). Se uso la primera; dejá una sola o asignale la regla a la labor.");
        }

        return ganadoras[0];
    }

    /// <summary>Cuantas dimensiones declara la regla: a mas dimensiones, mas especifica.</summary>
    private static int Especificidad(AccountConfiguration config)
    {
        int n = 0;
        if (config.LaborTypeId is not null) n++;
        if (config.ErpActivityId is not null) n++;
        if (config.ExecutionMode is not null) n++;
        return n;
    }

    /// <summary>
    /// Un pase del G4 por combinacion de origen, empresa, comprobante, moneda y lista de
    /// precios. Replica PaseBuilder.AssignGroups del modulo oficial de Ganaderia.
    /// </summary>
    private static void AssignGroups(List<PaseImputacion> pases)
    {
        int grupo = 1;

        foreach (var group in pases.GroupBy(p => (p.LaborId, p.CodEmpresa, p.CodComprobante, p.CodMoneda, p.CodListaDePrecios)))
        {
            foreach (var pase in group)
            {
                pase.IdAgrupacionPase = grupo;
            }

            grupo++;
        }
    }

}
