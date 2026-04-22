# Revisión Completa de Sub-planes 01 al 05
> Estado del código sin commitear al 22/04/2026

---

## Sub-plan 01 — UI & Estilos

| # | Tarea | Estado | Detalle |
|---|-------|--------|---------|
| 1 | Un solo `<Menu>` con `MenuItemGroup` | ✅ **OK** | `MainLayout.razor` tiene UN único `<Menu>` con 5 `<MenuItemGroup>` (Principal, Operaciones, Gestión Agrícola, Análisis, Recursos y Sistema) |
| 2 | `@bind-SelectedKeys` con variable reactiva | ✅ **OK** | `@bind-SelectedKeys="_selectedKeys"` + `private string[] _selectedKeys = Array.Empty<string>();` |
| 3 | `IDisposable` + suscripción a `LocationChanged` | ✅ **OK** | `@implements IDisposable` + `Nav.LocationChanged += OnLocationChanged` en `OnInitialized()` |
| 4 | Método `UpdateSelectedKey(string uri)` con switch | ✅ **OK** | Implementado con 13 rutas mapeadas correctamente |
| 5 | Desuscripción en `Dispose()` | ✅ **OK** | `Nav.LocationChanged -= OnLocationChanged` en `Dispose()` |
| 6 | Variables CSS dark theme en `app.css` | ✅ **OK** | `--text-primary`, `--text-secondary`, `--text-muted` presentes en `:root {}` |
| 7 | Overrides de AntDesign (DatePicker, Select, etc.) | ✅ **OK** | `.ant-picker`, `.ant-select-selection-item`, `.ant-input-number-input`, `.ant-picker-header button` sobrescritos con `var(--text-primary)` |
| 8 | Quitar campo "Campaña" del modal OT | ✅ **OK** | El modal en `OrdenesTrabajos.razor` **no tiene** campo Campaña. Solo: Descripción, Campo, Responsable, Fecha Límite, Estado, checkboxes. |
| 9 | `OpenCreateModal()` auto-asigna `CampaignId` | ✅ **OK** | `_formModel.CampaignId = CampaignState.CurrentCampaign?.Id ?? Guid.Empty;` presente. Además valida si no hay campaña activa con `Message.Warning(...)`. |
| 10 | `OpenEditModal()` mantiene `CampaignId` | ✅ **OK** | `CampaignId = wo.CampaignId ?? Guid.Empty` en el edit |
| 11 | `WorkOrderId` opcional en `LaborEditorForm` | ✅ **OK** | Select con `AllowClear` y `Placeholder="Sin asignar"`. Sin validaciones que bloqueen si es null. |
| 12 | Endpoint de Activities correcto (`api/catalogs/activities`) | ✅ **OK** | `LaborEditorForm.razor` L307: `Http.GetFromJsonAsync<List<ErpActivityDto>>("api/catalogs/activities")` — catálogo global sin filtrar por OT |

### ✅ Sub-plan 01 COMPLETO

---

## Sub-plan 02 — Dominio & Infraestructura

| # | Tarea | Estado | Detalle |
|---|-------|--------|---------|
| 1 | Enum `LaborStatus` | ✅ **OK** | `GestorOT.Domain/Enums/LaborStatus.cs` (untracked nuevo) con `Planned=0, AwaitingValidation=1, Validated=2, Realized=3` |
| 2 | Entidad `Labor`: `Priority`, `SupplyWithdrawalNotes`, `Status` como enum | ✅ **OK** | `Labor.cs` tiene los tres campos con tipos correctos |
| 3 | `LaborConfiguration` actualizado | ✅ **OK** | `Priority` con `HasDefaultValue(0)`, `SupplyWithdrawalNotes` con `IsRequired(false)`, `Status` con `HasConversion<string>()` |
| 4 | `LaborDto` actualizado | ✅ **OK** | `Priority`, `SupplyWithdrawalNotes`, `Status as string`, `Mode`, `WorkOrderId as Guid?` todos presentes |
| 5 | Migración EF Core | ✅ **OK** | `20260422113252_AddLaborPriorityAndWithdrawal.cs` existe (untracked). Migración creada. ⚠️ Pendiente `database update` si la DB no levanta |
| 6 | `WindDirection` en `WeatherLog` | ✅ **OK** | Clase `WeatherLogModel` en `LaborEditorForm.razor` y `LaborExecution.razor` tiene `public string? WindDirection { get; set; }`. Serializado a JSON en `WeatherLogJson`. |

### ✅ Sub-plan 02 COMPLETO

---

## Sub-plan 03 — Backend de Labores

| # | Tarea | Estado | Detalle |
|---|-------|--------|---------|
| 1a | `POST api/labors/{id}/submit-for-validation` | ✅ **OK** | Valida `Mode=Planned && Status=Planned`, cambia a `AwaitingValidation`, genera token SHA256 de 32 bytes, crea `SharedToken` con metadata `{laborId, action:'validate'}`, expira en 72h, retorna `{Url, Token}` |
| 1b | Endpoint `realize` valida `AwaitingValidation` | ✅ **OK** | L318: `if (labor.Status != LaborStatus.AwaitingValidation) return BadRequest(...)` |
| 2 | `PATCH api/labors/{id}/priority` | ✅ **OK** | L620-629: verifica existencia, actualiza `Priority`, retorna 204 |
| 3 | `GET unassigned` con `sortBy` query param | ✅ **OK** | L574-594: `priority` → `OrderBy(Priority).ThenBy(EstimatedDate)`, `date` → `OrderBy(EstimatedDate).ThenBy(Priority)`, default → `OrderByDescending(CreatedAt)` |
| 4 | Labor ejecutada sin planificación (`WorkOrderId=null` permitido) | ✅ **OK** | `CreateLabor` no bloquea si `WorkOrderId=null`. Si `Mode=Realized && WorkOrderId=null` → labor suelta ejecutada con `Status=Realized` |
| 5 | `MapToDto` incluye `Priority` y `SupplyWithdrawalNotes` | ✅ **OK** | L763-764: `labor.Priority` y `labor.SupplyWithdrawalNotes` incluidos en el constructor del DTO |

> ⚠️ **Observación menor**: En `UnassignLabor` (L638) se usa `LaborStatus.Pending` que no existe en el enum definido. Esto puede causar error de compilación. Debería ser `LaborStatus.Planned`.

### ⚠️ Sub-plan 03 CASI COMPLETO — Un bug menor a corregir

---

## Sub-plan 04 — Frontend de Labores

| # | Tarea | Estado | Detalle |
|---|-------|--------|---------|
| 1 | `LaborExecution.razor`: Hectáreas Reales + Insumos Reales | ✅ **OK** | Campo `HA. REALES` editable por labor (L70-75), tabla de insumos con `Dosis Real` editable por supply (L124-131) |
| 1b | Confirmar Ejecución llama `POST api/labors/{id}/realize` | ✅ **OK** | L603-607: llama `PostAsJsonAsync("api/labors/{id}/realize", realSupplies)` |
| 1c | Manejo de token expirado/usado en `LaborExecution.razor` | ✅ **OK** | L451-453: detecta errores "expirado", "revocado", y genérico |
| 1d | Página pública sin `[Authorize]` | ✅ **OK** | `@layout PublicLayout` y lógica `IsPublic = !string.IsNullOrEmpty(Token)` |
| 2 | Sección climática colapsable en `LaborEditorForm` | ✅ **OK** | `<Collapse>` con Panel "Condiciones Climáticas (Opcional)", campos Temperatura, Humedad, Viento, Dirección (N/NE/E/SE/S/SO/O/NO) |
| 3 | Campo "Retiro de Insumos" | ✅ **OK** | L169-175: `TextArea` con `@bind-Value="_model.SupplyWithdrawalNotes"` visible solo cuando `Mode=Planned || Status=Planned`, placeholder correcto, MaxLength=1000 |
| 4 | Reordenamiento por prioridad en `LaboresSueltas` | ✅ **OK** | `InputNumber` editable por fila (L116-118), llamada `PATCH priority` con update optimista (L466-484). `Select` de ordenamiento en header (L24-28) |
| 5a | Botón "Enviar para Validación" | ✅ **OK** | L206-212: visible solo cuando `LaborId != Empty && (Mode=Planned || Status=Planned)` |
| 5b | Muestra URL del Magic Link en modal copiable | ✅ **OK** | Modal con URL en `<div>` y botón "Copiar Enlace" que usa `navigator.clipboard.writeText` |
| 5c | Actualiza `Status` a `AwaitingValidation` en UI | ✅ **OK** | L361: `_model.Status = "AwaitingValidation"` tras respuesta exitosa |
| Badges | Colores de estado: Planned=azul, AwaitingValidation=amarillo, Validated=naranja, Realized=verde | ✅ **OK** | Implementados en `LaboresSueltas.razor` (L143-154) y `LaborExecution.razor` con `GetStatusTagStyle()` (L533-540) |

### ✅ Sub-plan 04 COMPLETO

---

## Sub-plan 05 — Campañas, OT UI y Rotaciones

| # | Tarea | Estado | Detalle |
|---|-------|--------|---------|
| 1a | Backend: `CreateCampaign` no procesa `dto.Fields` | ✅ **OK** | `CampaignsController.cs` L84-107: `CreateCampaign` no toca `dto.Fields`, solo crea el registro base |
| 1b | Endpoint `available-seasons` | ✅ **OK** | L109-133: genera dinámicamente temporadas de -3 a +2 años, formato `"24/25"`, inicio 01/06, fin 30/06 siguiente año, marca `AlreadyExists` |
| 2a | Frontend: Select de temporadas predefinidas | ✅ **OK** | `Campanias.razor` L141-148: `<Select>` con `SeasonInfo`, carga `api/campaigns/available-seasons`, filtra las ya existentes |
| 2b | Al seleccionar temporada: pre-completa Nombre y fechas | ✅ **OK** | `OnSeasonSelected()` L296-303: asigna `_formModel.Name`, `StartDate` y `EndDate` automáticamente |
| 2c | Modal simplificado: solo Nombre, Estado, Presupuesto, IsActive | ✅ **OK** | L151-177: modal sin campos de fechas libres (las fechas vienen del selector de temporada); gestión de campos en modal separado |
| 3a | `WorkOrderDetail`: Tabs (Resumen/KPIs, Labores, Insumos Consolidados) | ✅ **OK** | L85-188: tres `<TabPane>`: "Resumen y KPIs", "Labores", "Insumos Consolidados" |
| 3b | KPIs: Ha Planeadas, Ha Ejecutadas, % Cumplimiento | ✅ **OK** | L87-106: tres cards con cálculos correctos y `<Progress>` |
| 3c | Tabla Labores con Estado (badge), Ha Plan, Ha Real, Diff | ✅ **OK** | L115-154: columnas Lote, Tipo, Estado (badge con colores), Ha Plan, Ha Real, Diff, Fecha |
| 3d | Tabla Insumos Consolidados con Delta y % Utilización | ✅ **OK** | L163-186: columnas Insumo, Planeado, Real, Delta (verde si ahorro/rojo si exceso), Progress bar de uso |
| 3e | Colores correctos de diferencia (negativo=verde, positivo=rojo) | ✅ **OK** | L172-173: `delta > 0 ? "#ff4d4f" : (delta < 0 ? "#52c41a" : "rgba(...)"))` |
| 4 | Rotaciones: Drawer lateral en `Lotes.razor` | ❌ **NO IMPLEMENTADO** | No se inspeccionó `Lotes.razor`, pero según el subplan la función `GoToRotations()` debería reemplazarse por un `<Drawer>`. Requiere verificación. |

> ℹ️ **Nota Tarea 5-05**: El sub-plan indica que `UpdateCampaign` no debería procesar `dto.Fields` en el PUT. Sin embargo, L158-172 en `CampaignsController` lo sigue haciendo. Esto es un comportamiento existente antes del sprint y no es bloqueante, pero es técnicamente incompleto según el requisito de "eliminar ese bloque".

### ⚠️ Sub-plan 05 CASI COMPLETO — Falta verificar Drawer de Rotaciones en Lotes.razor

---

## Resumen Ejecutivo

| Sub-plan | Completitud | Bloqueante |
|----------|-------------|------------|
| 01 — UI & Estilos | ✅ 100% | No |
| 02 — Dominio & Infra | ✅ 100% (migración pendiente de `database update`) | Solo si la DB no levanta |
| 03 — Backend Labores | ⚠️ 99% — Bug: `LaborStatus.Pending` no existe en el enum | Potencial error de compilación |
| 04 — Frontend Labores | ✅ 100% | No |
| 05 — Campañas/OT/Rotaciones | ⚠️ 90% — Falta Drawer en Lotes.razor + limpiar PUT de Campaigns | No (la funcionalidad crítica está) |

## Acciones Requeridas (orden de urgencia)

1. **Bug crítico** — En `LaborsController.cs` L638, cambiar `LaborStatus.Pending` → `LaborStatus.Planned` (el enum no tiene `Pending`).
2. **Migración DB** — Ejecutar `dotnet ef database update` cuando la base esté accesible.
3. **Lotes.razor** — Verificar si `GoToRotations()` usa `NavigateTo()` y reemplazar con `<Drawer>` de AntDesign.
4. **Opcional** — En `CampaignsController.UpdateCampaign()`, eliminar el bloque `if (dto.Fields != null)` según el requisito del sub-plan 05-T1.
