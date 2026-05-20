# Fix: Distribución proporcional de insumos consolidados
## Feature: "Insumos Consolidados" con override del Total Real

**Rama sugerida:** `fix/insumos-proporcionales`  
**Archivos a modificar:**
- `src/GestorOT.Api/Controllers/WorkOrdersController.cs`
- `src/GestorOT.Infrastructure/Services/WorkOrderQueryService.cs` *(ya funciona, validar)*

---

## Diagnóstico completo del estado actual

### Lo que ya funciona ✅

1. **`WorkOrderQueryService.cs` (líneas 172-203)**: Al cargar la OT, el backend ya aplica la regla de tres. Para cada `SupplyApproval` con `RealTotalUsed`, calcula `CalculatedTotal` y `CalculatedDose` en los `LaborSupplyDto` del response. El DTO llega al frontend con los valores correctos.

2. **`OTDetalleFinalBase.cs` (`RecalculateProportionalSupplies`)**: El frontend también recalcula `CalculatedTotal`/`CalculatedDose` localmente cuando el usuario cambia el campo Real. Esto es redundante con el paso 1 pero no causa problemas.

3. **`OpenSupplyBreakdownDrawer`**: El drawer usa `s.CalculatedTotal ?? 0` y `s.CalculatedDose ?? 0` para las filas de "Realizados" — listo para mostrar los valores si llegan.

4. **`UpdateApprovalRealTotal` en el controller**: Cuando se llama, distribuye proporcionalmente `RealTotal` y `RealDose` a cada `LaborSupply` usando `PlannedTotal` como base de proporción. Esto persiste en la BD.

### El problema exacto ❌

El flujo tiene **dos caminos** que no están conectados:

**Camino A — "Override del Real Total" (columna editable en Insumos Consolidados):**
```
Usuario edita el campo Real → HandleRealTotalChange() → 
  context.RealTotalUsed = newValue (solo en memoria) →
  RecalculateProportionalSupplies() (calcula CalculatedTotal en memoria) →
  Cuando guarda: PUT /api/workorders/{id}/approvals (guarda RealTotalUsed en SupplyApproval) →
  [FALTA] Distribuir proporcionalmente a los LaborSupply y recalcular en el QueryService
```

**Camino B — `UpdateApprovalRealTotal` (el endpoint específico):**
- Distribuye `RealTotal`/`RealDose` a los `LaborSupply` ✓
- Pero **este endpoint no se llama en ningún lugar del flujo actual** del frontend

**Resultado:** El usuario cambia el Real, guarda, y:
- `SupplyApproval.RealTotalUsed` se guarda correctamente ✓
- Los `LaborSupply.RealTotal`/`RealDose` NO se actualizan ✗
- Cuando el `QueryService` recarga, sí recalcula `CalculatedTotal` desde `RealTotalUsed` ✓ (esto funciona)
- Pero el "Detalle de Insumos" en cada labor sigue mostrando `RealTotal = null` y `CalculatedTotal = 0` ✗

---

## El algoritmo correcto (según la spec del PDF)

> "El sistema no debe tener en cuenta el coeficiente planeado ya que romperá la relación, debe construir un nuevo coeficiente en función del total de htas en las labores y el total de insumo usado en toda la OT."

**Fórmula:**
```
nuevoCœfGlobal = RealTotalUsed / SumaDeHectáreasDeTodasLasLabores

Para cada labor:
  CalculatedTotal_labor = nuevoCœfGlobal × Hectáreas_de_esa_labor
  CalculatedDose_labor  = CalculatedTotal_labor / Hectáreas_de_esa_labor
                        = nuevoCœfGlobal  (mismo para todas)
```

**PERO** el prototipo de Replit usa la proporción por `PlannedTotal` (que es `Dosis × Ha`), no por `Ha` pura. Cuando el coeficiente planeado es igual en todas las labores, ambos dan el mismo resultado. Cuando difieren, la proporción por `PlannedTotal` es más correcta porque respeta la intensidad de aplicación planeada.

**Algoritmo final (el que ya tiene implementado `UpdateApprovalRealTotal`):**
```
totalPlanned = Σ supply.PlannedTotal  (para todas las labores con ese insumo)
Para cada labor:
  proporcion = supply.PlannedTotal / totalPlanned
  CalculatedTotal = RealTotalUsed × proporcion
  CalculatedDose  = CalculatedTotal / (RealHectares ?? PlannedHectares)
```

Este algoritmo ya está implementado en `UpdateApprovalRealTotal`. **El problema es que no se llama en el momento correcto.**

---

## Fix

### Cambio 1 — `WorkOrdersController.cs`: integrar la distribución proporcional en `UpdateApprovals`

El flujo del frontend llama `PUT /api/workorders/{id}/approvals` (el bulk) al guardar. Ese endpoint solo guarda `RealTotalUsed` en `SupplyApproval` pero no toca `LaborSupply`. Hay que agregar la distribución proporcional ahí:

```csharp
[HttpPut("{id:guid}/approvals")]
public async Task<IActionResult> UpdateApprovals(Guid id, List<WorkOrderSupplyApprovalDto> dtos)
{
    var workOrder = await _context.WorkOrders
        .Include(w => w.SupplyApprovals)
        // ✅ NUEVO: incluir las labores y sus supplies para poder distribuir
        .Include(w => w.Labors)
            .ThenInclude(l => l.Supplies)
        .FirstOrDefaultAsync(w => w.Id == id);

    if (workOrder == null) return NotFound();

    foreach (var dto in dtos)
    {
        var approval = workOrder.SupplyApprovals.FirstOrDefault(a => a.Id == dto.Id);
        if (approval == null)
        {
            approval = new WorkOrderSupplyApproval
            {
                Id = Guid.NewGuid(),
                WorkOrderId = id,
                SupplyId = dto.SupplyId
            };
            _context.WorkOrderSupplyApprovals.Add(approval);
        }

        approval.ApprovedWithdrawal = dto.ApprovedWithdrawal;
        approval.WithdrawalCenter = dto.WithdrawalCenter;
        approval.RealTotalUsed = dto.RealTotalUsed;
        approval.TotalCalculated = dto.TotalCalculated;

        // ✅ NUEVO: si hay un RealTotalUsed, distribuir proporcionalmente a los LaborSupply
        if (dto.RealTotalUsed.HasValue && dto.RealTotalUsed > 0)
        {
            DistribuirProporcionalmenteALabores(workOrder, dto.SupplyId, dto.RealTotalUsed.Value);
        }
        else if (dto.RealTotalUsed == null)
        {
            // Si se limpió el override, limpiar también CalculatedTotal/CalculatedDose
            LimpiarCalculadosEnLabores(workOrder, dto.SupplyId);
        }
    }

    await _context.SaveChangesAsync();
    return NoContent();
}

// ✅ NUEVO método privado — misma lógica que UpdateApprovalRealTotal pero reutilizable
private static void DistribuirProporcionalmenteALabores(
    WorkOrder workOrder, Guid supplyId, decimal realTotalUsed)
{
    var suppliesAcrossLabors = workOrder.Labors
        .SelectMany(l => l.Supplies)
        .Where(s => s.SupplyId == supplyId)
        .ToList();

    var totalPlanned = suppliesAcrossLabors.Sum(s => s.PlannedTotal);

    if (totalPlanned > 0)
    {
        foreach (var supply in suppliesAcrossLabors)
        {
            var proportion = supply.PlannedTotal / totalPlanned;
            supply.CalculatedTotal = Math.Round(realTotalUsed * proportion, 4);

            var parentLabor = workOrder.Labors
                .First(l => l.Supplies.Contains(supply));
            var effectiveArea = supply.RealHectares ?? supply.PlannedHectares;
            if (effectiveArea > 0)
            {
                supply.CalculatedDose = Math.Round(supply.CalculatedTotal.Value / effectiveArea, 4);
            }
        }
    }
    else if (suppliesAcrossLabors.Count > 0)
    {
        // Edge case: PlannedTotal = 0, distribuir uniformemente
        var perLabor = Math.Round(realTotalUsed / suppliesAcrossLabors.Count, 4);
        foreach (var supply in suppliesAcrossLabors)
        {
            supply.CalculatedTotal = perLabor;
        }
    }
}

// ✅ NUEVO método privado — limpiar calculados cuando se quita el override
private static void LimpiarCalculadosEnLabores(WorkOrder workOrder, Guid supplyId)
{
    var suppliesAcrossLabors = workOrder.Labors
        .SelectMany(l => l.Supplies)
        .Where(s => s.SupplyId == supplyId)
        .ToList();

    foreach (var supply in suppliesAcrossLabors)
    {
        supply.CalculatedTotal = null;
        supply.CalculatedDose = null;
    }
}
```

**También refactorizar `UpdateApprovalRealTotal` para reutilizar el mismo método privado:**

```csharp
[HttpPut("{id:guid}/approvals/{approvalId:guid}/real-total")]
public async Task<IActionResult> UpdateApprovalRealTotal(
    Guid id, Guid approvalId, [FromBody] decimal? realTotalUsed)
{
    var workOrder = await _context.WorkOrders
        .Include(w => w.SupplyApprovals)
        .Include(w => w.WorkOrderStatus)
        .Include(w => w.Labors)
            .ThenInclude(l => l.Supplies)
        .FirstOrDefaultAsync(w => w.Id == id);

    if (workOrder == null) return NotFound();
    if (workOrder.WorkOrderStatus?.IsEditable == false)
        return Conflict("La OT no es editable.");

    var approval = workOrder.SupplyApprovals.FirstOrDefault(a => a.Id == approvalId);
    if (approval == null) return NotFound("Approval no encontrado.");

    approval.RealTotalUsed = realTotalUsed;

    if (realTotalUsed.HasValue && realTotalUsed > 0)
        DistribuirProporcionalmenteALabores(workOrder, approval.SupplyId, realTotalUsed.Value);
    else if (realTotalUsed == null)
        LimpiarCalculadosEnLabores(workOrder, approval.SupplyId);

    await _context.SaveChangesAsync();
    return NoContent();
}
```

---

### Cambio 2 — `WorkOrdersController.cs`: `ConsolidateSupplies` debe preservar `RealTotalUsed`

**Problema actual en `ConsolidateSuppliesAsync`:** cuando se recalculan los insumos (por ejemplo, al guardar una labor), la consolidación sobreescribe `TotalCalculated` pero deja intacto `RealTotalUsed`. Esto está bien. **Pero NO recalcula `CalculatedTotal`/`CalculatedDose` en los `LaborSupply`** cuando `RealTotalUsed` ya existe.

Agregar esta lógica al final de `ConsolidateSuppliesAsync`:

```csharp
public async Task ConsolidateSuppliesAsync(Guid workOrderId)
{
    var workOrder = await _context.WorkOrders
        .Include(w => w.Labors)
            .ThenInclude(l => l.Supplies)
        .Include(w => w.SupplyApprovals)
        .FirstOrDefaultAsync(w => w.Id == workOrderId);

    if (workOrder == null) return;

    // ...código existente (actualizar TotalCalculated en SupplyApprovals)...
    var suppliesInLabors = workOrder.Labors
        .SelectMany(l => l.Supplies)
        .GroupBy(s => s.SupplyId)
        .Select(g => new { 
            SupplyId = g.Key, 
            Total = g.Sum(s => s.PlannedTotal > 0 ? s.PlannedTotal : (s.RealTotal ?? 0)) 
        })
        .ToList();

    foreach (var item in suppliesInLabors)
    {
        var approval = workOrder.SupplyApprovals.FirstOrDefault(a => a.SupplyId == item.SupplyId);
        if (approval == null)
        {
            approval = new WorkOrderSupplyApproval
            {
                Id = Guid.NewGuid(),
                WorkOrderId = workOrderId,
                SupplyId = item.SupplyId,
                ApprovedWithdrawal = item.Total
            };
            _context.WorkOrderSupplyApprovals.Add(approval);
        }
        approval.TotalCalculated = item.Total;
    }

    var laborSupplyIds = suppliesInLabors.Select(s => s.SupplyId).ToHashSet();
    var toRemove = workOrder.SupplyApprovals.Where(a => !laborSupplyIds.Contains(a.SupplyId)).ToList();
    _context.WorkOrderSupplyApprovals.RemoveRange(toRemove);

    // ✅ NUEVO: re-distribuir proporcionalmente para todos los approvals con RealTotalUsed
    foreach (var approval in workOrder.SupplyApprovals.Where(a => a.RealTotalUsed.HasValue && a.RealTotalUsed > 0))
    {
        DistribuirProporcionalmenteALabores(workOrder, approval.SupplyId, approval.RealTotalUsed!.Value);
    }

    await _context.SaveChangesAsync();
}

// El mismo método privado, pero acá como método del service:
private static void DistribuirProporcionalmenteALabores(
    WorkOrder workOrder, Guid supplyId, decimal realTotalUsed)
{
    var suppliesAcrossLabors = workOrder.Labors
        .SelectMany(l => l.Supplies)
        .Where(s => s.SupplyId == supplyId)
        .ToList();

    var totalPlanned = suppliesAcrossLabors.Sum(s => s.PlannedTotal);
    if (totalPlanned <= 0) return;

    foreach (var supply in suppliesAcrossLabors)
    {
        var proportion = supply.PlannedTotal / totalPlanned;
        supply.CalculatedTotal = Math.Round(realTotalUsed * proportion, 4);

        var parentLabor = workOrder.Labors.First(l => l.Supplies.Contains(supply));
        var area = supply.RealHectares ?? supply.PlannedHectares;
        if (area > 0)
            supply.CalculatedDose = Math.Round(supply.CalculatedTotal.Value / area, 4);
    }
}
```

> **Nota:** para no duplicar el método privado entre el controller y el service, se puede mover a un helper estático en `GestorOT.Application` (por ejemplo `SupplyDistributionHelper.cs`) e importarlo en ambos lugares.

---

### Cambio 3 — `WorkOrderQueryService.cs`: verificar que el paso "Rule of Three" usa `CalculatedTotal` de BD cuando existe

El QueryService ya calcula `CalculatedTotal` en memoria desde `RealTotalUsed`. Con los cambios 1 y 2, la BD ya tendrá los valores actualizados. Para consistencia, el QueryService puede simplemente leer `s.CalculatedTotal` desde la BD (ya lo hace en las líneas 148-149) y el bucle "Rule of Three" (líneas 172-203) puede quedar como fallback solo cuando `CalculatedTotal == null`:

```csharp
// En WorkOrderQueryService.cs, Step 19:
// ✅ MODIFICAR: solo recalcular si CalculatedTotal no viene de la BD
foreach (var approval in workOrder.SupplyApprovals.Where(a => a.RealTotalUsed.HasValue))
{
    var supplyId = approval.SupplyId;
    var realTotalUsed = approval.RealTotalUsed!.Value;
    var laborsWithSupply = laborsDto
        .Where(l => l.Supplies.Any(s => s.SupplyId == supplyId))
        .ToList();
    
    var totalPlannedForSupply = laborsWithSupply
        .Sum(l => l.Supplies.Where(s => s.SupplyId == supplyId).Sum(s => s.PlannedTotal));

    if (totalPlannedForSupply <= 0) continue;

    foreach (var labor in laborsWithSupply)
    {
        foreach (var supply in labor.Supplies.Where(s => s.SupplyId == supplyId))
        {
            // ✅ Si ya tiene valor de la BD, usarlo; si no, calcular
            if (!supply.CalculatedTotal.HasValue)
            {
                var proportion = supply.PlannedTotal / totalPlannedForSupply;
                supply.CalculatedTotal = realTotalUsed * proportion;

                var area = supply.RealHectares ?? labor.Hectares;
                if (area > 0)
                    supply.CalculatedDose = supply.CalculatedTotal / area;
            }
        }
    }
}
```

> **Alternativa más simple:** dejar el QueryService exactamente como está. El recálculo en memoria es idempotente con el de la BD. Solo importa que la BD tenga los valores para cuando el PDF o exportaciones los lean directamente desde la entidad.

---

## Flujo completo después del fix

```
1. Usuario está en OT / Tab "Insumos Consolidados"
2. Ve: Insumo 24DAMINA · Plan: 260 · Real: [campo editable]
3. Escribe 290 en el campo Real
4. Aparece confirm: "Este valor modificará las labores proporcionalmente. ¿Estás seguro?"
5. Confirma → HandleRealTotalChange() actualiza context.RealTotalUsed = 290 en memoria
6. Usuario hace click "Guardar Cambios"
7. PUT /api/workorders/{id}/approvals →
     approval.RealTotalUsed = 290 ✓
     LaborA.supply_24DAMINA.CalculatedTotal = 290 * (110/260) = 122.69 ✓
     LaborA.supply_24DAMINA.CalculatedDose  = 122.69 / 55 = 2.23 ✓
     LaborB.supply_24DAMINA.CalculatedTotal = 290 * (150/260) = 167.31 ✓
     LaborB.supply_24DAMINA.CalculatedDose  = 167.31 / 50 = 3.35 ✓
8. await LoadData() recarga → QueryService devuelve CalculatedTotal desde BD ✓
9. El drawer de desglose muestra:
     Labor A · Ha: 55 · Coef. Calc.: 2.23 · Total Calc.: 122.69
     Labor B · Ha: 50 · Coef. Calc.: 3.35 · Total Calc.: 167.31
```

---

## Ejemplo numérico del PDF — verificación

**Datos:**
- Labor A: Ha=55, PlannedDose=2, PlannedTotal=110
- Labor B: Ha=50, PlannedDose=3, PlannedTotal=150
- TotalPlanned = 260
- RealTotalUsed ingresado = 290

**Cálculo:**
```
proporcionA = 110 / 260 = 42.31%
proporcionB = 150 / 260 = 57.69%

CalculatedTotal_A = 290 × 0.4231 = 122.69
CalculatedTotal_B = 290 × 0.5769 = 167.31

CalculatedDose_A = 122.69 / 55 = 2.23 L/ha
CalculatedDose_B = 167.31 / 50 = 3.35 L/ha
```

**Verificación:** 122.69 + 167.31 = **290.00** ✓

---

## Criterios de aceptación

- [ ] Usuario ingresa 290 como Real Total del insumo 24DAMINA en Insumos Consolidados
- [ ] Confirma el dialog
- [ ] Hace "Guardar Cambios"
- [ ] Abre el drawer "Ver desglose por labor" → sección "Realizados" muestra:
  - Labor A: Coef. Calc. = 2.23, Total Calc. = 122.69
  - Labor B: Coef. Calc. = 3.35, Total Calc. = 167.31
- [ ] Recarga la página → los valores persisten (vienen de la BD, no de memoria)
- [ ] Abrir cada labor individualmente → en "Detalle de Insumos" muestra CalculatedTotal correcto
- [ ] Borrar el override (botón ✕) → CalculatedTotal y CalculatedDose vuelven a null en las labores
- [ ] "Recalcular de Labores" no pisa el override cuando RealTotalUsed está activo

---

## Resumen de archivos y cambios

| Archivo | Tipo de cambio |
|---|---|
| `WorkOrdersController.cs` | Agregar `DistribuirProporcionalmenteALabores()` como método privado estático. Llamarlo desde `UpdateApprovals()` y refactorizar `UpdateApprovalRealTotal()` para reutilizarlo. |
| `WorkOrderService.cs` | Agregar llamada a la distribución proporcional al final de `ConsolidateSuppliesAsync()`, después de actualizar `TotalCalculated`. |
| `WorkOrderQueryService.cs` | Opcional: agregar guard `if (!supply.CalculatedTotal.HasValue)` en el Step 19 para no sobreescribir valores de BD. |

**No se requieren cambios en el frontend.** El frontend ya tiene toda la lógica correcta: `HandleRealTotalChange`, `RecalculateProportionalSupplies`, `OpenSupplyBreakdownDrawer` y `SaveAllChanges` ya llaman los endpoints correctos. El único gap era que el backend no completaba el ciclo al persistir los valores.
