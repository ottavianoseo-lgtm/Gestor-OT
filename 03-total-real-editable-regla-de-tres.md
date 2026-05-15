# Bug 3 — Total Real editable en Insumos Consolidados con regla de tres bidireccional

> Módulo: **Detalle de OT — tab Insumos Consolidados (interacción)**
> Imagen del PDF: **imagen4** (tabla "Planeado" con Ha / Coef/Ha / Cantidad / % del Total — ejemplo numérico del cálculo)
> Criticidad: **Alta** (sin esto, el usuario no puede registrar consumos cuando el contratista solo reporta el total)
> Estimación: **6-10h** (el más complejo de los 4 — toca lógica de cálculo + UI + endpoint)

---

## 1. Qué pide el bug

> "En la sección de Insumos consolidados falta una función muy importante que es que el campo de Total Real sea editable. Podes tener dos casos, un contratista muy prolijo que te dice cuanto aplicó realmente de cada labor, lo cuál te da el real, u otros que directamente te reportan el total final, que no necesariamente es igual que el planeado, y sin tener el detalle de en qué lote estuvo la diferencia (...). Entonces tengo que poder modificar este Valor, pero va enganchado con las suma de las dosis reales declaradas. Si toco las dosis reales declaradas, se modifica este valor, y si toco este valor, se modifican las dosis reales."

Ejemplo numérico (imagen4):

> Planeamiento: dos labores con Glifosato, coef 2 L/Ha.
> - Labor 1: 10 Ha × 2 = 20 L (66.66% del total)
> - Labor 2: 5 Ha × 2 = 10 L (33.33% del total)
> - Total planeado: 30 L
>
> Realización: el contratista reporta que en total se gastaron 29 L (en lugar de 30) trabajando sobre 13.5 Ha en lugar de 15.
>
> Resultado esperado (regla de tres por porcentaje de planeamiento):
> - Labor 1 (66.66%): 29 × 0.6666 = 19.33 L → coef calculado = 19.33 / 9 = 2.14 L/Ha
> - Labor 2 (33.33%): 29 × 0.3333 = 9.67 L → coef calculado = 9.67 / 4.5 = 2.15 L/Ha

Comportamiento bidireccional:

1. Si el usuario edita el **Total Real** en Insumos Consolidados → se recalculan `RealDose`/`RealTotal` de cada `LaborSupply` proporcionalmente.
2. Si el usuario edita las **dosis reales** dentro del modal de Labor → el **Total Real** en Consolidados se actualiza como suma.

## 2. Estado actual del código

### 2.1 Backend — la lógica del cálculo existe pero es **read-only**

`src/GestorOT.Infrastructure/Services/WorkOrderQueryService.cs` líneas 172-203 ya tiene la regla de tres implementada. **Pero** la usa solo para proyectar campos calculados (`CalculatedDose`, `CalculatedTotal`) al leer un detail — no escribe nada en BD.

```csharp
// Step 19: Rule of three for Realized labors
foreach (var approval in workOrder.SupplyApprovals.Where(a => a.RealTotalUsed.HasValue))
{
    var supplyId = approval.SupplyId;
    var realTotalUsed = approval.RealTotalUsed!.Value;

    var laborsWithSupply = laborsDto.Where(l => l.Supplies.Any(s => s.SupplyId == supplyId)).ToList();
    var totalPlannedForSupply = laborsWithSupply
        .Sum(l => l.Supplies.Where(s => s.SupplyId == supplyId).Sum(s => s.PlannedTotal));

    if (totalPlannedForSupply > 0)
    {
        foreach (var labor in laborsWithSupply)
        {
            foreach (var supply in labor.Supplies.Where(s => s.SupplyId == supplyId))
            {
                var proportion = supply.PlannedTotal / totalPlannedForSupply;
                supply.CalculatedTotal = realTotalUsed * proportion;
                var area = supply.RealHectares ?? labor.Hectares;
                if (area > 0)
                {
                    supply.CalculatedDose = supply.CalculatedTotal / area;
                }
            }
        }
    }
}
```

**Esto es exactamente lo que pide la imagen4**, pero como proyección read-only.

### 2.2 Frontend — el Total Real ya es editable visualmente

`src/GestorOT.Client/Pages/WorkOrderDetailFinal.razor` líneas 138-147:

```razor
<Column TData="decimal?" Title="Real">
    @if (_order.IsLocked)
    {
        @(context.RealTotalUsed?.ToString("N2") ?? "0.00")
    }
    else
    {
        <AntDesign.InputNumber TValue="decimal?" @bind-Value="context.RealTotalUsed" Size="InputSize.Small" Style="width: 100px;" />
    }
</Column>
```

Se puede tipear, y se persiste vía `PUT /api/workorders/{id}/approvals` cuando el usuario hace "Guardar Cambios".

**Lo que falta**: que el valor tipeado actualice **también** las dosis reales por labor en BD, no solo el `RealTotalUsed` agregado.

## 3. Decisión clave de diseño (afecta todo el plan)

Hay **dos modelos posibles** y la diferencia es enorme. El plan elige uno, pero el usuario tiene que confirmarlo antes de empezar.

### Modelo A — Cálculo on-the-fly (recomendado)

- `RealTotalUsed` es el **dato canónico** a nivel OT/insumo.
- `LaborSupply.RealDose` y `LaborSupply.RealTotal` quedan **read-only en la BD** (solo se setean si el usuario los edita directamente en el modal de Labor).
- Al leer la OT, `CalculatedDose` y `CalculatedTotal` se computan al vuelo (como ya hace el query service hoy).
- La UI muestra dos cosas distintas: lo que el usuario tipeó directamente (`RealDose`) o lo derivado del agregado (`CalculatedDose`).

**Ventaja**: sin riesgo de inconsistencias. Si el usuario sube `RealTotalUsed` de 29 → 30, los cálculos por labor se reproyectan solos. No hay que reescribir BD.

**Desventaja**: si después el usuario edita una `RealDose` específica en el modal de Labor, queda un mix (algunos calculados, otros editados). Hay que decidir qué gana.

### Modelo B — Persistencia bidireccional con escritura

- Cuando el usuario edita `RealTotalUsed`, se ejecuta la regla de tres en el backend y **se escriben** los nuevos `RealDose`/`RealTotal` en cada `LaborSupply`.
- Cuando el usuario edita una `RealDose` específica en el modal de Labor, el backend recalcula `RealTotalUsed` como `SUM(RealTotal)` y lo persiste.

**Ventaja**: BD siempre consistente. La auditoría es clara (qué se ejecutó).

**Desventaja**: edge cases peligrosos:

- Si el usuario edita el total y después edita una labor individual, ¿qué pasa con las demás? ¿Se reproporcionan?
- Concurrencia: dos usuarios editando al mismo tiempo → race condition.
- Pérdida de información: si el contratista declaró 19.33 L para Labor 1 y después editás el total y la regla devuelve 19.40, perdés el dato original.

### Decisión del plan

**Modelo A**. Razones:

1. Es coherente con lo que ya hace el query service (la regla de tres ya proyecta).
2. Evita reescritura masiva de filas (y los problemas de concurrencia).
3. Permite distinguir entre "lo declaró el contratista por labor" (RealDose escrito) y "lo derivamos del total" (CalculatedDose).
4. Mantiene `RealTotalUsed` como source of truth simple a nivel OT.

**Si el usuario confirma que quiere Modelo B**, este `.md` no aplica tal cual — abrir issue para rediseñar el plan. **Antes de empezar a codear, el agente debe confirmar con el usuario** cuál modelo se elige.

## 4. Pre-lectura obligatoria con context7 (MCP)

1. `context7` — **Blazor WASM**: patrón de evento `EventCallback<decimal>` para escuchar cambios en `InputNumber` y disparar recálculo del lado cliente.
2. `context7` — **AntDesign Blazor v1.6.1**: `<AntDesign.InputNumber>` — eventos `OnChange` vs `ValueChanged`, comportamiento de debounce.
3. `context7` — **EF Core 10**: behavior de `SaveChangesAsync` cuando se modifican múltiples entidades, transacciones implícitas.
4. `context7` — **System.Text.Json**: agregar campos al DTO sin romper serialización pre-existente.

## 5. Plan de implementación (Modelo A)

### 5.1 Backend — endpoint que recalcula al editar `RealTotalUsed`

**Nuevo endpoint** en `WorkOrdersController.cs`:

```csharp
[HttpPut("{id:guid}/approvals/{approvalId:guid}/real-total")]
public async Task<IActionResult> UpdateApprovalRealTotal(
    Guid id, Guid approvalId, [FromBody] decimal? realTotalUsed)
{
    var workOrder = await _context.WorkOrders
        .Include(w => w.SupplyApprovals)
        .FirstOrDefaultAsync(w => w.Id == id);

    if (workOrder == null) return NotFound();
    if (workOrder.WorkOrderStatus?.IsEditable == false)
        return Conflict("La OT no es editable.");

    var approval = workOrder.SupplyApprovals.FirstOrDefault(a => a.Id == approvalId);
    if (approval == null) return NotFound("Approval no encontrado.");

    approval.RealTotalUsed = realTotalUsed;
    await _context.SaveChangesAsync();

    return NoContent();
}
```

Este endpoint **solo** persiste `RealTotalUsed`. Los `CalculatedDose`/`CalculatedTotal` se proyectan al siguiente `GET /api/workorders/{id}` vía el código existente en `WorkOrderQueryService.cs` línea 172.

### 5.2 Frontend — recálculo en vivo del lado cliente al editar `RealTotalUsed`

**Archivo:** `WorkOrderDetailFinal.razor` y `OTDetalleFinalBase.cs`.

Cuando el usuario tipea un nuevo `RealTotalUsed`, además de actualizar el bind, hay que **proyectar la regla de tres en memoria** para mostrar las dosis derivadas en tiempo real (antes de guardar).

Agregar al `OTDetalleFinalBase.cs` (o crearlo si no existe la función):

```csharp
protected void RecalculateProportionalSupplies(Guid supplyId)
{
    if (_order == null) return;

    var approval = _order.SupplyApprovals.FirstOrDefault(a => a.SupplyId == supplyId);
    if (approval?.RealTotalUsed == null) return;

    var laborsWithSupply = _order.Labors
        .Where(l => l.Supplies.Any(s => s.SupplyId == supplyId))
        .ToList();

    var totalPlanned = laborsWithSupply
        .Sum(l => l.Supplies.Where(s => s.SupplyId == supplyId).Sum(s => s.PlannedTotal));

    if (totalPlanned <= 0) return;

    foreach (var labor in laborsWithSupply)
    {
        foreach (var supply in labor.Supplies.Where(s => s.SupplyId == supplyId))
        {
            var proportion = supply.PlannedTotal / totalPlanned;
            supply.CalculatedTotal = approval.RealTotalUsed.Value * proportion;

            var area = supply.RealHectares ?? labor.Hectares;
            if (area > 0)
            {
                supply.CalculatedDose = supply.CalculatedTotal / area;
            }
        }
    }

    StateHasChanged();
}
```

Esto **replica exactamente** la lógica de `WorkOrderQueryService.cs` líneas 172-203 pero en el cliente, sobre el modelo en memoria. Cuando el usuario guarde, el servidor recalcula igual al servir el siguiente GET — los valores ya son consistentes.

**Modificar la columna Real** para disparar el recálculo:

```razor
<Column TData="decimal?" Title="Real">
    @if (_order.IsLocked)
    {
        @(context.RealTotalUsed?.ToString("N2") ?? "0.00")
    }
    else
    {
        <AntDesign.InputNumber TValue="decimal?"
                               Value="@context.RealTotalUsed"
                               ValueChanged="@(v => { context.RealTotalUsed = v; RecalculateProportionalSupplies(context.SupplyId); })"
                               Size="InputSize.Small" Style="width: 100px;" />
    }
</Column>
```

### 5.3 Frontend — mostrar el desglose proporcional en un drawer/modal

La imagen4 muestra una tabla con columnas `Ha | Coef/Ha | Cantidad | % del Total`. El plan recomienda que esta tabla aparezca en un **drawer lateral** que se abre con un icono en cada fila de "Insumos Consolidados", mostrando el desglose por labor del insumo seleccionado.

Agregar al final de la fila de la tabla principal una columna de acción:

```razor
<ActionColumn Title="" Width="50">
    <Tooltip Title="Ver desglose por labor">
        <Button Type="@ButtonType.Text" Size="@ButtonSize.Small"
                OnClick="() => OpenSupplyBreakdownDrawer(context.SupplyId)">
            <Icon Type="unordered-list" Style="color: #3498DB;" />
        </Button>
    </Tooltip>
</ActionColumn>
```

Y un drawer al final del archivo:

```razor
<Drawer Title="Desglose por labor"
        Visible="_breakdownDrawerVisible"
        Width="600"
        OnClose="() => _breakdownDrawerVisible = false">
    @if (_breakdownSupply != null)
    {
        <h4 style="color: #fff;">@_breakdownSupply.SupplyName (@_breakdownSupply.SupplyUnit)</h4>
        <p style="color: rgba(255,255,255,0.6);">
            Total planeado: @_breakdownPlannedTotal.ToString("N2") · Total real: @(_breakdownSupply.RealTotalUsed?.ToString("N2") ?? "—")
        </p>

        <h5 style="color: #3498DB; margin-top: 20px;">Planeado</h5>
        <Table TItem="SupplyBreakdownRow" DataSource="@_plannedRows" Size="TableSize.Small" HidePagination>
            <PropertyColumn Property="r => r.LotName" Title="Lote" />
            <PropertyColumn Property="r => r.Hectares" Title="Ha" Format="N2" />
            <PropertyColumn Property="r => r.Coef" Title="Coef/Ha" Format="N2" />
            <PropertyColumn Property="r => r.Cantidad" Title="Cantidad" Format="N2" />
            <PropertyColumn Property="r => r.Percent" Title="% del Total" Format="N2" />
        </Table>

        <h5 style="color: #2ECC71; margin-top: 20px;">Realizado (calculado)</h5>
        <Table TItem="SupplyBreakdownRow" DataSource="@_realizedRows" Size="TableSize.Small" HidePagination>
            <PropertyColumn Property="r => r.LotName" Title="Lote" />
            <PropertyColumn Property="r => r.Hectares" Title="Ha Real" Format="N2" />
            <PropertyColumn Property="r => r.Coef" Title="Coef. Calc." Format="N2" />
            <PropertyColumn Property="r => r.Cantidad" Title="Total Calc." Format="N2" />
            <PropertyColumn Property="r => r.Percent" Title="% del Total" Format="N2" />
        </Table>
    }
</Drawer>
```

`SupplyBreakdownRow` es un record privado en el code-behind:

```csharp
private record SupplyBreakdownRow(string? LotName, decimal Hectares, decimal Coef, decimal Cantidad, decimal Percent);

protected void OpenSupplyBreakdownDrawer(Guid supplyId)
{
    _breakdownSupply = _order!.SupplyApprovals.FirstOrDefault(a => a.SupplyId == supplyId);
    if (_breakdownSupply == null) return;

    var laborsWithSupply = _order.Labors
        .Where(l => l.Supplies.Any(s => s.SupplyId == supplyId))
        .ToList();

    _breakdownPlannedTotal = laborsWithSupply
        .Sum(l => l.Supplies.Where(s => s.SupplyId == supplyId).Sum(s => s.PlannedTotal));

    _plannedRows = laborsWithSupply.SelectMany(l => l.Supplies
        .Where(s => s.SupplyId == supplyId)
        .Select(s => new SupplyBreakdownRow(
            l.LotName,
            s.PlannedHectares > 0 ? s.PlannedHectares : l.Hectares,
            s.PlannedDose,
            s.PlannedTotal,
            _breakdownPlannedTotal > 0 ? (s.PlannedTotal / _breakdownPlannedTotal) * 100 : 0
        ))).ToList();

    _realizedRows = laborsWithSupply.SelectMany(l => l.Supplies
        .Where(s => s.SupplyId == supplyId)
        .Select(s => new SupplyBreakdownRow(
            l.LotName,
            s.RealHectares ?? l.Hectares,
            s.CalculatedDose ?? 0,
            s.CalculatedTotal ?? 0,
            _breakdownPlannedTotal > 0 ? (s.PlannedTotal / _breakdownPlannedTotal) * 100 : 0
        ))).ToList();

    _breakdownDrawerVisible = true;
    StateHasChanged();
}
```

### 5.4 Manejo del caso "el usuario edita una RealDose específica en modal Labor"

Comportamiento esperado: cuando se guarda una labor con `RealDose` editada manualmente, el `RealTotalUsed` de la OT en consolidados debería actualizarse al recargar la página.

**Lo que ya pasa hoy**: al guardar la labor, no se recalcula `RealTotalUsed`. El consolidado queda con el valor anterior hasta que el usuario hace click en "Recalcular de Labores" (botón en línea 133 de `WorkOrderDetailFinal.razor` → llama a `ConsolidateSuppliesAsync`).

**Decisión del plan**: dejar el botón "Recalcular de Labores" como mecanismo explícito. **No** disparar el recálculo automático en cada save de labor (riesgo: si el usuario edita 5 labores seguidas, son 5 recálculos completos). Documentar en el tooltip del botón qué hace.

**Mejora opcional**: hacer que `WorkOrderService.ConsolidateSuppliesAsync` (en `Application/Services/WorkOrderService.cs` líneas 21-62) **no sobreescriba** `RealTotalUsed` si ya tiene valor. Hoy esa función inicializa approvals pero no respeta el `RealTotalUsed` manual. Verificar en el código actual:

```csharp
// WorkOrderService.cs línea 54
approval.TotalCalculated = item.Total;
```

Solo modifica `TotalCalculated`, no `RealTotalUsed`. ✅ Ya respeta el manual. Confirmar con un test.

## 6. Tests

**Archivo nuevo:** `src/GestorOT.Tests/Regression/RuleOfThreeProjectionTests.cs`

Casos:

1. **`GetById_ProjectsCalculatedDoseAndTotal_WhenRealTotalUsedIsSet`** — escenario del ejemplo del bug (2 labores Glifosato 10Ha/5Ha coef 2, RealTotalUsed=29) → `CalculatedTotal[Labor1] ≈ 19.33`, `CalculatedTotal[Labor2] ≈ 9.67`.

2. **`GetById_DoesNotProjectCalculated_WhenRealTotalUsedIsNull`** — mismo setup con `RealTotalUsed = null` → `CalculatedDose` y `CalculatedTotal` quedan en null/default.

3. **`GetById_HandlesZeroPlannedTotal_Gracefully`** — caso borde: una labor con `PlannedTotal = 0` y otra con 10. Total planeado = 10, total real = 5. No debe dividir por cero.

4. **`UpdateApprovalRealTotal_Endpoint_PersistsValue`** — POST con `realTotalUsed = 25`, GET confirma que se guardó.

5. **`UpdateApprovalRealTotal_OnLockedOT_ReturnsConflict`** — OT en estado no-editable → 409.

6. **`ConsolidateSupplies_DoesNotOverwriteRealTotalUsed`** — escenario: approval ya tiene `RealTotalUsed = 25`. Click en "Recalcular de Labores" → el approval sigue con `RealTotalUsed = 25` (no se pisa).

7. **`Frontend_RecalculateProportionalSupplies_MatchesBackend`** — si se introduce bUnit: setear el modelo en memoria, llamar `RecalculateProportionalSupplies(supplyId)`, comparar con lo que devuelve el servicio backend. Deben coincidir hasta 4 decimales.

## 7. Smoke test manual

### Escenario del bug (regla de tres bidireccional)

1. Crear OT con 2 labores: Labor A en Lote 1 (10 Ha), Labor B en Lote 2 (5 Ha). Ambas con insumo Glifosato, `PlannedDose = 2`.
2. Click "Recalcular de Labores" → en Consolidados aparece Glifosato: Planeado = 30 (20 + 10).
3. Marcar ambas labores como Realizadas con sus mismos Ha planeados.
4. En Consolidados, en la columna **Real** del Glifosato, tipear `29` y blur (o tab para que se dispare el cambio).
5. **Inmediatamente** (sin recargar página):
   - Click en el icono de "Ver desglose por labor" del Glifosato.
   - Drawer abre, muestra:
     - Planeado: Labor A = 20 (66.66%), Labor B = 10 (33.33%).
     - Realizado calculado: Labor A ≈ 19.33, Labor B ≈ 9.67.
6. Click "Guardar Cambios" → confirma que se persistió.
7. Recargar página → los valores siguen ahí. Abrir el drawer de nuevo → mismos valores calculados (verificación de que el backend proyecta correctamente).

### Escenario inverso (editar dosis real por labor)

8. Abrir el modal de Labor A, cambiar Coef Real de la dosis a 2.5 (manualmente). Guardar.
9. Click en "Recalcular de Labores" → ahora el Real de Glifosato en Consolidados = `2.5 × 9 + (calculado para Labor B)`.

### Casos borde

10. **OT bloqueada**: si el estado no es editable, el InputNumber del Real aparece como texto plano. El drawer sigue funcionando en read-only.
11. **División por cero**: una labor sin insumos planeados (PlannedTotal = 0). No debe explotar al abrir el drawer.
12. **Recálculo y cambio de campaña**: dejar editado el RealTotalUsed sin guardar, cambiar de campaña → ¿el cambio se pierde? Confirmar que sí (no hay autoguardado).

## 8. Definition of Done específica

- [ ] Build limpio.
- [ ] Endpoint `PUT /api/workorders/{id}/approvals/{approvalId}/real-total` agregado y testeado.
- [ ] Frontend: `RecalculateProportionalSupplies` agregada y disparada en el `ValueChanged` del InputNumber del Real.
- [ ] Drawer de "Desglose por labor" implementado con tablas Planeado / Realizado.
- [ ] Botón "Recalcular de Labores" sigue funcionando y NO pisa `RealTotalUsed` manual.
- [ ] 7 tests nuevos en `RuleOfThreeProjectionTests.cs`, en verde.
- [ ] Smoke test de los 12 pasos completado.
- [ ] PR description con consultas a `context7`.
- [ ] Modelo elegido (A o B) **confirmado con el usuario** y documentado en la descripción del PR.

## 9. Lo que NO se cambia en este PR

- La fórmula de regla de tres del query service (sigue siendo la misma, ya estaba bien).
- La persistencia de `LaborSupply.RealDose` / `RealTotal` cuando el usuario las edita directamente en el modal Labor (sigue funcionando como hoy).
- Los campos `CalculatedDose` / `CalculatedTotal` en la BD (siguen siendo columnas, pero el plan no las escribe — solo se proyectan al leer).
- El export a HTML/PDF (eso es Bug 4).

---

## Riesgos identificados y mitigaciones

| Riesgo | Probabilidad | Impacto | Mitigación |
|---|---|---|---|
| El usuario edita una dosis y después el total → inconsistencia | Media | Media | El drawer muestra siempre el "calculado" vs el "declarado" → educación visual. Documentar en tooltip. |
| Concurrencia: 2 usuarios editando el mismo `RealTotalUsed` | Baja | Baja | Last-write-wins (default EF). Aceptable. |
| Performance al abrir el drawer con OT de 100 labores | Baja | Baja | La proyección es O(n labors × m supplies), no hay queries adicionales. Si se mide >100ms, paginar. |
| Si Modelo B se requiere después → reescritura grande | Media | Alta | **Confirmar Modelo con usuario antes de empezar**. Es la mitigación más importante. |
| Los datos viejos de `LaborSupply.RealTotal` (sin calculado vía regla de tres) podrían divergir | Media | Baja | Aceptable mientras `CalculatedDose`/`CalculatedTotal` sean los que se muestran en el drawer. El modal Labor sigue mostrando `RealDose` directamente para edición manual. |
