# Bug 1 — Insumos Consolidados: faltan 3 columnas y mover "Retiro de Insumos" desde Labor a OT

> Módulo: **Detalle de OT — tab Insumos Consolidados + modal de Labor**
> Imágenes del PDF: **imagen1** (tab consolidados con 3 huecos rojos) + **imagen2** (campo "Retiro de Insumos" en modal de Labor a mover)
> Criticidad: **Alta** (la OT planeada actúa como "orden de retiro" — sin estas columnas no cumple su rol funcional)
> Estimación: **3-4h**

---

## 1. Qué pide el bug

> "En Insumos Consolidados de una OT faltan tres columnas. Una que te dé las unidades en las cuales estás midiendo la cantidad del insumo, otra que sea el total del retiro aprobado (que va en función de en qué formato viene) y otra con Centros para indicarle a dónde tiene permiso de ir a retirarlo. Entonces cuando se emite la OT planeada funciona como una 'orden de retiro'."
>
> "Hoy está dentro de el modal de Labores como un campo de texto que se llama [Retiro de Insumos]. Saquemoslo de ahí y pongamoslo acá en la parte de insumos consolidados de OT. Porque no tiene sentido a cada labor decirle de donde va a tener que ir a buscar los insumos. Se agrupan labores en una OT y saca todos los insumos juntos de donde se le indique."

Traducido:

| Cambio | Dónde |
|---|---|
| Agregar columna **Unidad** en tabla "Plan vs. Real" | `WorkOrderDetailFinal.razor` líneas 132-152 (tab "Insumos Consolidados") |
| Agregar columna **Total Retiro Aprobado** (editable) | mismo lugar |
| Agregar columna **Centros** (editable) | mismo lugar |
| Quitar campo **"Retiro de Insumos"** del modal de Labor | `LaborEditorForm.razor` líneas 250-257 |

## 2. Estado actual del código

**Bueno: lo más invasivo ya está hecho.**

- Entidad `WorkOrderSupplyApproval` (`Domain/Entities/WorkOrderSupplyApproval.cs`) ya tiene `ApprovedWithdrawal` (decimal) y `WithdrawalCenter` (string?).
- Endpoint `PUT /api/workorders/{id}/approvals` (`WorkOrdersController.cs` líneas 372-404) ya persiste ambos campos.
- DTO `WorkOrderSupplyApprovalDto` (`Shared/Dtos/LaborDto.cs` línea 111) ya expone `ApprovedWithdrawal` y `WithdrawalCenter`. **Falta solo `SupplyUnit`** — ver `00-README-plan-v3.md` sección 4.1.

Lo único que **no** está hecho:

- La UI no renderiza esas 3 columnas en la tabla de consolidados.
- El campo "Retiro de Insumos" sigue dentro de la Labor (`LaborEditorForm.razor` línea 252) y `Labor.SupplyWithdrawalNotes` sigue siendo un campo de la entidad Labor.

## 3. Pre-lectura obligatoria con context7 (MCP)

1. `context7` — **AntDesign Blazor v1.6.1**: `<Column TData="..." Title="...">`, `<AntDesign.InputNumber>`, `<AntDesign.Input>` con `@bind-Value`, `<Tooltip>` para labels largos en headers de tabla.
2. `context7` — **EF Core 10**: `[NotMapped]` (si decidimos no borrar la columna de BD), behavior de `IsRequired(false)` vs nullable strings.
3. `context7` — **System.Text.Json source generator**: agregar un campo nuevo a un DTO usado por `AppJsonSerializerContext` y regenerar contexto sin romper otras llamadas.

Registrar consultas en la descripción del PR.

## 4. Plan de implementación

### 4.1 Backend — agregar `SupplyUnit` al DTO (cambio compartido con Bug 4)

Aplicar el cambio descripto en `00-README-plan-v3.md` sección 4.1:

- `Shared/Dtos/LaborDto.cs` — agregar `public string? SupplyUnit { get; set; }` a `WorkOrderSupplyApprovalDto` y al constructor.
- `Infrastructure/Services/WorkOrderQueryService.cs` línea 205-216 — pasar `a.Supply?.UnitA` al constructor.

### 4.2 Frontend — agregar 3 columnas a la tabla

**Archivo:** `src/GestorOT.Client/Pages/WorkOrderDetailFinal.razor` líneas 132-152.

Reemplazar el bloque actual de la tabla por:

```razor
<TabPane Tab="Insumos Consolidados" Key="3">
    <div class="section-header" style="margin-top: 16px;">
        <h3 class="section-title">Plan vs. Real — Orden de Retiro</h3>
        <Button Size="@ButtonSize.Small" Ghost Icon="sync" OnClick="ConsolidateSupplies">Recalcular de Labores</Button>
    </div>
    <div class="glass-card table-container">
        <Table TItem="WorkOrderSupplyApprovalDto" DataSource="_order.SupplyApprovals" Size="TableSize.Small" HidePagination ScrollX="1100">

            <Column TData="string" Title="Insumo">@context.SupplyName</Column>

            <Column TData="string" Title="Unidad">
                <span style="color: rgba(255,255,255,0.6);">@(context.SupplyUnit ?? "—")</span>
            </Column>

            <Column TData="decimal" Title="Planeado">@context.TotalCalculated.ToString("N2")</Column>

            <Column TData="decimal" Title="Total Retiro Aprobado">
                @if (_order.IsLocked)
                {
                    @context.ApprovedWithdrawal.ToString("N2")
                }
                else
                {
                    <AntDesign.InputNumber TValue="decimal"
                                           @bind-Value="context.ApprovedWithdrawal"
                                           Min="0m" Step="0.01m"
                                           Size="InputSize.Small"
                                           Style="width: 110px;" />
                }
            </Column>

            <Column TData="string" Title="Centro de Retiro">
                @if (_order.IsLocked)
                {
                    <span>@(context.WithdrawalCenter ?? "—")</span>
                }
                else
                {
                    <AntDesign.Input @bind-Value="context.WithdrawalCenter"
                                     Placeholder="Ej: Depósito Norte"
                                     Size="InputSize.Small"
                                     Style="width: 160px;" />
                }
            </Column>

            <Column TData="decimal?" Title="Real">
                @if (_order.IsLocked)
                {
                    @(context.RealTotalUsed?.ToString("N2") ?? "0.00")
                }
                else
                {
                    <AntDesign.InputNumber TValue="decimal?"
                                           @bind-Value="context.RealTotalUsed"
                                           Size="InputSize.Small"
                                           Style="width: 100px;" />
                }
            </Column>

            <Column TData="decimal" Title="Delta">
                @{
                    var delta = (context.RealTotalUsed ?? 0) - context.TotalCalculated;
                    var color = delta > 0 ? "#ff4d4f" : (delta < 0 ? "#52c41a" : "rgba(255,255,255,0.4)");
                }
                <span style="color: @color; font-weight: 600;">@(delta > 0 ? "+" : "")@delta.ToString("N2")</span>
            </Column>

            <Column TData="string" Title="% Uso">
                @{
                    var usage = context.TotalCalculated > 0 ? ((context.RealTotalUsed ?? 0) / context.TotalCalculated) * 100 : 0;
                }
                <Progress Percent='(double)usage' Size="ProgressSize.Small" Steps="5"
                          StrokeColor='usage > 100 ? "#ff4d4f" : "#52c41a"' />
            </Column>
        </Table>
    </div>
</TabPane>
```

**Nota sobre `ScrollX="1100"`**: con 8 columnas en lugar de 5, el ancho mínimo crece. Si el viewport es chico, AntDesign activa scroll horizontal. Sin esto se ven todas amontonadas.

**Nota sobre el orden de columnas**: pongo `Unidad` justo después de `Insumo` (consistente con la imagen5 del Bug 4). Luego van Planeado → Total Retiro Aprobado → Centro porque es el "flujo de retiro" (qué planeé, qué autorizo, dónde). Real / Delta / % Uso van al final porque son post-ejecución.

### 4.3 Frontend — quitar campo "Retiro de Insumos" del modal de Labor

**Archivo:** `src/GestorOT.Client/Components/LaborEditorForm.razor` líneas 250-257.

**Borrar** el bloque:

```razor
@if (_model.Mode == "Planned" || _model.Status == "Planned")
{
    <FormItem Label="Retiro de Insumos" Style="margin-top: 16px;">
        <TextArea @bind-Value="_model.SupplyWithdrawalNotes" Rows="2"
                  Placeholder="Ej: Retirar de depósito norte el 15/05..." MaxLength="1000"
                  Style="color: var(--text-primary); background: rgba(255,255,255,0.05);" />
    </FormItem>
}
```

**No** borrar `SupplyWithdrawalNotes` de la entidad `Labor` ni de `LaborDto` en este PR. Razones:

1. Pueden existir labores ya guardadas con texto ahí — borrar la columna pierde data.
2. Borrar el campo del DTO impacta `WorkOrderQueryService.cs` línea 167, `LaborsController.cs` líneas 134, 275, 1251, `SearchController.cs` línea 62. Tocar todos esos lados aumenta el riesgo de regresión.

En su lugar, este PR deja la columna en BD y el campo en el DTO, pero **inaccesible desde la UI** (la única forma de leerlo era el modal). En un PR posterior de limpieza se puede deprecar y migrar.

**Acción opcional adicional (recomendada)**: si alguna labor existente tiene contenido en `SupplyWithdrawalNotes`, considerar una migración **read-only** (sin DROP COLUMN) que copie ese texto al `WithdrawalCenter` del `WorkOrderSupplyApproval` correspondiente. Pero solo si el usuario lo confirma — no asumir.

### 4.4 Persistencia desde la UI — confirmar el flujo del save

`WorkOrderDetailFinal.razor` línea 29 — botón "Guardar Cambios" llama a `SaveAllChanges` (definido en `OTDetalleFinalBase.cs`). Hay que verificar que ese método ya invoca `PUT /api/workorders/{id}/approvals` con la lista completa de approvals.

**Acción del agente**: abrir `OTDetalleFinalBase.cs`, buscar `SaveAllChanges`, confirmar que pasa `_order.SupplyApprovals` al endpoint. Si no lo hace, agregarlo. Si lo hace, asegurarse de que los nuevos valores que el usuario tipeó en los `InputNumber`/`Input` (que están `@bind-Value="context.ApprovedWithdrawal"`) **se persisten en la misma colección** que se envía.

> **Riesgo de regresión**: si `SaveAllChanges` hace `_order.SupplyApprovals.Select(...)` y construye un DTO nuevo dejando afuera los nuevos campos, el save fallará silenciosamente. Verificar el `Select` línea por línea.

## 5. Tests

**Archivo nuevo:** `src/GestorOT.Tests/Regression/SupplyApprovalDtoTests.cs`

Casos:

1. **`WorkOrderSupplyApprovalDto_HasSupplyUnitField`** — reflection-based: confirma que la propiedad existe (prevención de regresión si alguien la borra).
2. **`WorkOrderQueryService_GetById_PopulatesSupplyUnit`** — crear OT con approval con Supply.UnitA = "L", traer el detail y confirmar que `dto.SupplyApprovals[0].SupplyUnit == "L"`.
3. **`WorkOrderQueryService_GetById_NullSupplyUnit_DoesNotCrash`** — Supply.UnitA = null, el DTO sale con `SupplyUnit = null` y la query no explota.
4. **`UpdateApprovals_PersistsApprovedWithdrawalAndWithdrawalCenter`** — POST con valores nuevos, GET confirma que se guardaron. **Regresión** del comportamiento previo.

**Tests existentes que deben seguir verdes**: `WorkOrderStatusTests`, `FileAssetTests`, `PlaneamientoOriginalRegressionTests`, `LotDtoRegressionTests`.

## 6. Smoke test manual

Prerrequisito: en `Inventories`, al menos un insumo con `UnitA` poblado (ej. "L" o "kg").

1. Abrir una OT con al menos una labor que tenga supplies.
2. Tab "Insumos Consolidados" — verificar que se ven **8 columnas** (Insumo, Unidad, Planeado, Total Retiro Aprobado, Centro de Retiro, Real, Delta, % Uso).
3. Columna **Unidad** muestra "L" / "kg" / lo que sea según `Inventory.UnitA`. Si está vacía → "—".
4. Tipear "25" en **Total Retiro Aprobado** y "Depósito Norte" en **Centro de Retiro**. Click "Guardar Cambios".
5. Refrescar la página → los valores tipeados aparecen.
6. Modal de Labor (clic en una labor) — **no** debe aparecer el campo "Retiro de Insumos" como TextArea.
7. **OT bloqueada** (estado con `IsEditable=false`): los `InputNumber`/`Input` de las 3 columnas editables aparecen como texto plano, no editables.
8. **Cambio de campaña** — cambiar de campaña, volver a la OT original, los valores siguen ahí (no se cruzan datos).
9. **Multi-tenant** — login con otro tenant que tenga una OT distinta — la tabla muestra los approvals de ESE tenant, no del anterior.

## 7. Definition of Done específica

- [ ] Build limpio (paso 0 del README).
- [ ] `SupplyUnit` agregado al DTO + al query service.
- [ ] Tabla de consolidados con 8 columnas, las 3 nuevas editables solo cuando `!IsLocked`.
- [ ] Campo "Retiro de Insumos" eliminado del modal de Labor.
- [ ] `SaveAllChanges` verificado: persiste `ApprovedWithdrawal` y `WithdrawalCenter` al editar.
- [ ] 4 tests nuevos en `SupplyApprovalDtoTests.cs`, en verde.
- [ ] Smoke test manual de los 9 pasos completado.
- [ ] PR description lista las consultas a `context7`.
- [ ] Screenshot del antes y después en la descripción.

## 8. Lo que NO se cambia en este PR

- Entidad `Labor` ni columna `SupplyWithdrawalNotes` en BD (queda para un PR de limpieza posterior).
- `Labor.SupplyWithdrawalNotes` en el DTO (sigue serializándose pero no se renderiza).
- Endpoints `WorkOrdersController` y `LaborsController` (no se modifican).
- El servicio `ConsolidateSuppliesAsync` (`WorkOrderService.cs`) — sigue calculando `TotalCalculated` y inicializando `ApprovedWithdrawal = item.Total` al crear approvals nuevos.
- La columna de regla de tres `CalculatedDose`/`CalculatedTotal` (eso es Bug 3).
- El export HTML interactivo (botón "Compartir Reporte") — eso es Bug 4 si se va a PDF.

---

## Notas finales

El backend ya tenía casi todo el laburo hecho. Este PR es 70% UI + agregar 1 campo al DTO. Si compila y los smoke tests pasan, este es el bug **más barato** de los 4 prioritarios y el **mejor candidato para ir primero**.
