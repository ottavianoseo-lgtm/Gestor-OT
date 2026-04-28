# GestorOT — Sub-planes Pendientes Ronda 2
> Ordenados de mayor a menor dificultad estimada

---

## 1. EST-02 — Validación de rotación en StrategyLaborWizard
**Dificultad:** Alta | **Sub-plan:** 08 - Estrategias

### Ítems pendientes

**EST-02: Validación de Rotación en la vista previa del StrategyLaborWizard**
- Verificar que el wizard chequea la rotación por combinación lote+fecha usando `api/campaigns/{campaignId}/lots/{campaignLotId}/rotations/active?date=...`
- Confirmar que las advertencias se muestran al usuario en la vista previa **antes** de confirmar la creación masiva
- Si `forceDateSeparation = true`, las fechas calculadas con `DayOffset` deben respetar las rotaciones de cada lote individualmente
- En el loop de generación de previews, validar rotación por cada lote×labor con su fecha calculada
- Agregar UI en la vista previa que destaque con icono de advertencia las combinaciones con conflicto
- El endpoint `bulk-from-strategy` debe ejecutar `ValidateLaborActivityMatchesRotationAsync` por cada labor y retornar advertencias en la respuesta (sin bloquear)
- **CRÍTICO:** en la vista previa, si la actividad del `StrategyItem` difiere de la actividad de la rotación activa del lote, marcarlo como advertencia específica

**Archivos a modificar:**
- `StrategyLaborWizard.razor`
- `LaborsController.cs`

---

## 2. PO-06 — Endpoint unpin-original-plan (con roles)
**Dificultad:** Alta | **Sub-plan:** 09 - Planeamiento Original

> ⚠️ Condición previa: implementar solo si ya existe infraestructura de roles en el sistema.

### Ítems pendientes

**PO-06: Rol con capacidad de reabrir Planeamiento (desanclar IsOriginalPlan)**
- Agregar endpoint `[HttpPost("{id:guid}/unpin-original-plan")]` que setee `IsOriginalPlan = false` y registre en `AuditLog`
- Agregar botón **Desanclar** en `PlaneamientoOriginal.razor` (solo visible para rol `AdminCampaña`)
- Registrar en `AuditLog` con usuario, timestamp e Id de labor modificada

**Archivos a modificar:**
- `LaborsController.cs`
- `PlaneamientoOriginal.razor`

---

## 3. LAB-07 — Aviso de Labores sin OT al crear desde OT
**Dificultad:** Media | **Sub-plan:** 03 - Labores

### Ítems pendientes

**LAB-07: Aviso de Labores sin OT al crear desde OT**
- En `OnAddLaborClicked()` de `WorkOrderDetailFinal.razor`, antes de abrir el `LaborEditorForm`:
  - Llamar `GET api/labors/unassigned/count` (endpoint ya existe en `LaborsController`, línea 652)
  - Si `count > 0`, mostrar `Modal.ConfirmAsync` con opciones: *Crear labor nueva* / *Revisar labores sin OT*
  - Si elige *Revisar*: abrir modal con tabla filtrable de labores sin OT y opción de asignarlas a la OT actual
  - Si elige *Crear*: flujo normal de `LaborEditorForm`
- Verificar que el endpoint `GET api/labors/unassigned/count` funciona con el tenant actual

**Archivos a modificar:**
- `WorkOrderDetailFinal.razor`
- `LaborsController.cs` (verificación)

---

## 4. LOT-05 — Highlight de Lote desde Mapa no expande la fila automáticamente
**Dificultad:** Media | **Sub-plan:** 04 - Lotes

### Ítems pendientes

**LOT-05: Auto-expandir fila en Lotes desde highlight**
- Al detectar `highlight.HasValue` en `OnInitializedAsync` o `OnAfterRenderAsync`, setear el estado de expansión de la fila correspondiente
- Verificar que la tabla AntDesign Blazor expone un mecanismo para expandir programáticamente una fila (`RowExpandable`, `DefaultExpandAllRows` o similar)
- Hacer scroll automático hasta la fila del lote destacado usando JS interop si es necesario

**Archivos a modificar:**
- `Lotes.razor`

---

## 5. BUG-04 — Filtro de columna (sort) rompe el visual de la tabla
**Dificultad:** Media | **Sub-plan:** 01 - Bugs Críticos

### Ítems pendientes

**BUG-04: Sort rompe visual de tabla**
- Diagnosticar el problema: ¿es CSS (`z-index`, `overflow hidden` en la tabla padre) o de estado (lista no se re-renderiza)?
- **Si es CSS:** agregar regla en `wwwroot/css/main-layout.css` para resolver el conflicto de overflow al activar el dropdown de sort
- **Si es de estado:** reemplazar `Sortable` por ordenamiento manual con `IComparer` y `StateHasChanged()` en las tres tablas
- Probar en las tres tablas principales: Lotes, OTs y Labores

**Archivos a modificar:**
- `Lotes.razor`
- `OrdenesTrabajos.razor`
- `LaboresSueltas.razor`
- `wwwroot/css/main-layout.css` (si aplica)

---

## 6. BUG-03 — Lote con Geometría aparece como huérfano en el Mapa
**Dificultad:** Baja | **Sub-plan:** 01 - Bugs Críticos

### Ítems pendientes

**BUG-03: Verificar WktGeometry en API**
- Verificar que `MapToDto` en `LotsController` serialice correctamente el campo `WktGeometry` con la geometría WKT cuando `Geometry != null`
- Agregar test de regresión: crear lote con geometría → consultar `/api/lots` → confirmar que `WktGeometry` no es null/empty en la respuesta

**Archivos a modificar:**
- `LotsController.cs`

---

## 7. OT-01 — Selector de Campo presente en el modal de creación de OT
**Dificultad:** Baja | **Sub-plan:** 02 - OT

### Ítems pendientes

**OT-01: Quitar selector Campo del modal OT**
- Eliminar la carga de `_availableFields` en `OnInitializedAsync` si ya no se usa en el modal
- Verificar si hay algún `<FormItem Label="Campo">` renderizado en el modal y eliminarlo
- Mantener `FieldId` en el modelo solo si otros módulos lo necesitan (sin renderizar el selector)
- Confirmar que `FieldId` ya es nullable (`Guid?`) en `WorkOrder.cs` — previamente verificado OK

**Archivos a modificar:**
- `OrdenesTrabajos.razor`
- `WorkOrdersController.cs` (verificación)

---

## 8. BUG-06 — Botones GIS se superponen al menú Polígono Dibujado
**Dificultad:** Baja | **Sub-plan:** 01 - Bugs Críticos

### Ítems pendientes

**BUG-06: Ocultar GIS toolbar con polígono dibujado**
- Agregar variable de estado `_polygonMenuVisible` (bool) en `Mapa.razor`
- Setear `_polygonMenuVisible = true` cuando `_drawnWkt != null` y se muestra el rail de polígono dibujado
- En el markup del `gis-toolbar`, cambiar de ajustar `z-index` a condicionar visibilidad: `@if (!_polygonMenuVisible)`
- Resetear `_polygonMenuVisible = false` al descartar o guardar el polígono

**Archivos a modificar:**
- `Mapa.razor`

---

## 9. OT-05 — Estado de OT no se refleja correctamente en el modal de edición
**Dificultad:** Baja | **Sub-plan:** 02 - OT

### Ítems pendientes

**OT-05: Verificar binding de Estado en modal edición**
- En `OpenEditModal`, agregar log o assert para verificar que `wo.Status` coincida con alguno de los `_workOrderStatuses[i].Name`
- Si los estados se guardan como enum string (ej: `"InProgress"`) pero `WorkOrderStatusDto.Name` es texto libre (ej: `"En Proceso"`), unificar criterio o usar `Id` como binding value
- Probar el flujo completo: crear OT con estado X → cerrar y reabrir modal → verificar que el estado X aparezca seleccionado

**Archivos a modificar:**
- `OrdenesTrabajos.razor`

---

## 10. LAB-06 — Ejecutar Labor Planeada sin InitialStatus en WorkOrderDetail
**Dificultad:** Baja | **Sub-plan:** 03 - Labores

### Ítems pendientes

**LAB-06: InitialStatus Realized en WorkOrderDetail**
- En `LaborEditorForm.razor`, confirmar que el parámetro `[Parameter] InitialStatus` está declarado y que en `OnParametersSetAsync` se hace `_model.Status = InitialStatus` cuando el valor no es null
- En `WorkOrderDetailFinal.razor`, verificar que el botón **Ejecutar** también use `InitialStatus="Realized"` al abrir el form

**Archivos a modificar:**
- `LaborEditorForm.razor`
- `WorkOrderDetailFinal.razor`

---

## 11. BUG-02 — Campaña 28/29 aparece en selector pero no en lista de Campañas
**Dificultad:** Baja | **Sub-plan:** 01 - Bugs Críticos

### Ítems pendientes

**BUG-02: Análisis campaña en selector vs lista**
- Verificar que `GET /api/campaigns` (usado en `Campanias.razor`) y `GET /api/campaigns/active` (usado en `CampaignSelector`) apliquen el mismo criterio de filtro por `Status`
- Confirmar qué endpoint usa `Campanias.razor` para listar campañas
- Agregar log o test que compare las listas retornadas por ambos endpoints para el mismo tenant

**Archivos a revisar:**
- `CampaignsController.cs`
- `Campanias.razor`

---

## 12. PO-05 — Badge IsOriginalPlan ausente en LaboresSueltas y WorkPlanner
**Dificultad:** Baja | **Sub-plan:** 09 - Planeamiento Original

### Ítems pendientes

**PO-05: Badge BASE en LaboresSueltas y WorkPlanner**

En `LaboresSueltas.razor`, agregar en la columna de tipo/nombre de labor:
```razor
@if (context.IsOriginalPlan) {
    <Tag Color="gold" Style="font-size: 10px; margin-left: 6px;">BASE</Tag>
}
```

En `WorkPlanner.razor`, agregar borde/fondo ámbar en el bloque de cada labor con `IsOriginalPlan == true`:
```html
style="border-left: 3px solid #DAA520;"
```

**Archivos a modificar:**
- `LaboresSueltas.razor`
- `WorkPlanner.razor`

---

## 13. LOT-01 — Columna Ha (CadastralArea) redundante en el desplegable de Lote
**Dificultad:** Baja | **Sub-plan:** 04 - Lotes

### Ítems pendientes

**LOT-01: Verificar tabla interna sin columna Ha**
- En `Lotes.razor`, dentro del `<ExpandTemplate>` → `<Table TItem="SurfaceHistoryDto">`, confirmar que **no** hay una `<PropertyColumn>` apuntando a `CadastralArea` o `Ha`
- Si existe, eliminarla

**Archivos a modificar:**
- `Lotes.razor`

---

## 14. LOT-04 — Botones GIS y Rotaciones navegan fuera en lugar de abrir modal inline
**Dificultad:** Diseño / Decisión | **Sub-plan:** 04 - Lotes

### Ítems pendientes

**LOT-04: Decidir modal vs navegación GIS/Rotaciones**
- Evaluar si la navegación a otra pantalla (`GoToGis` → `/mapa?lotId=X`, `GoToRotations` → `/rotaciones`) satisface la intención del usuario o si se prefiere un modal in-page
- **Si se decide modal in-page:** crear o reutilizar modales de GIS y Rotaciones que acepten solo `LotId` como parámetro, sin depender del formulario completo del lote
- **Si la navegación es aceptable:** documentar la decisión en `10_diferidos_y_descartados.md`

**Archivos a modificar:**
- `Lotes.razor` (si se decide modal)
- `10_diferidos_y_descartados.md` (si se documenta como decisión)

---

*Informe generado a partir de auditoría del 27/04/2026 — GestorOT Ronda 2*
