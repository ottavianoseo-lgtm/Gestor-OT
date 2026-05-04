# Sprint 10 - Solo lectura, estados configurables y QA

## Objetivo

Cerrar inconsistencias de permisos, campañas bloqueadas, estados de OT y pruebas de regresión.

## Rama sugerida

`fix/s10-solo-lectura-estados-qa`

## Bugs cubiertos

- Campaña bloqueada debe ser consultable pero no editable.
- Estados de OT deben controlar edición.
- Validaciones backend deben impedir mutaciones aunque UI falle.
- Falta regresión completa del flujo.

## Archivos principales

- `CampaignState.cs`
- `CampaignSelector.razor`
- `Campanias.razor`
- `Lotes.razor`
- `OrdenesTrabajos.razor`
- `LaborEditorForm.razor`
- `PlaneamientoOriginal.razor`
- `Estrategias.razor`
- `StrategyLaborWizard.razor`
- Controladores API de:
  - campañas;
  - lotes;
  - labores;
  - estrategias;
  - OTs;
  - rotaciones;
  - adjuntos.
- `WorkOrderStatusesController`
- Tests existentes

## Regla funcional

Campaña bloqueada:

- Permite ver.
- Permite descargar.
- Permite consultar.
- No permite crear, editar, eliminar, asignar, desasignar, modificar superficies, crear rotaciones, crear labores, crear OTs ni adjuntar.

Estado de OT no editable:

- Permite ver.
- No permite editar OT.
- No permite agregar/quitar/editar labores asociadas.
- No permite modificar aprobaciones si corresponde.

## Tareas técnicas

### 1. Campaña bloqueada en frontend

Crear o usar helper común:

- `CampaignState.IsReadOnly`
- `CampaignState.IsLocked`

Aplicarlo en:

- Campañas.
- Lotes.
- Rotaciones.
- Labores.
- OTs.
- Estrategias.
- Planeamiento Original.
- Adjuntos.

### 2. Campaña bloqueada en backend

En endpoints de mutación validar:

- campaña existe;
- campaña no está bloqueada;
- si el recurso pertenece a una campaña bloqueada, rechazar.

Endpoints mínimos:

- Crear/editar/eliminar campaña-lote.
- Crear/editar/eliminar labor.
- Crear/editar/eliminar OT.
- Crear labores desde estrategia.
- Crear/editar rotaciones.
- Adjuntar archivos a labor de campaña bloqueada.
- Modificar planeamiento original.

### 3. Estados de OT

1. Definir estado inicial configurable.
2. Si no existe estado inicial, fallback `Draft`.
3. Respetar `IsEditable`.
4. Si `IsEditable == false`:
   - backend rechaza edición;
   - UI deshabilita acciones.
5. Al cambiar estado, sincronizar `WorkOrderStatusId` y `Status`.

### 4. Tests automatizados

Agregar tests según capacidad del proyecto.

#### Backend

- `CreateWorkOrder_WithoutCampaign_ReturnsBadRequest`
- `CreateLabor_WithoutCampaignLot_ReturnsBadRequest`
- `CreateLabor_OriginalPlanRealized_ReturnsBadRequest`
- `CreateLabor_ActivityConflict_ReturnsBadRequest`
- `CreateLabor_NoRotation_ReturnsWarningAndCreates`
- `MutateLockedCampaign_ReturnsConflict`
- `GetStrategies_ReturnsNames`
- `BulkFromStrategy_CreatesExpectedLabors`

#### Frontend

Si hay bUnit o Playwright:

- Crear campaña y verla en selector.
- Guardar labor incompleta y ver errores.
- Aplicar estrategia y crear labores.
- Adjuntar antes de guardar.

### 5. QA manual

Ejecutar `13_QA_REGRESION_COMPLETA.md`.

## No hacer en este sprint

- No agregar features nuevas.
- No rediseñar UI.
- No cambiar reglas funcionales ya implementadas.
- No mezclar con optimizaciones de performance salvo bug evidente.

## Criterios de aceptación

- Campaña bloqueada es realmente solo lectura.
- Estado de OT no editable bloquea modificaciones.
- Tests críticos pasan.
- QA manual completo documentado.
- No hay regresión de navegación.

## Prompt corto para DeepSeek

Implementá solo Sprint 10. Consolidá solo lectura para campañas bloqueadas en frontend y backend, respetá estados de OT no editables y agregá pruebas/regresión. No agregues features nuevas. Entregá evidencia de build, tests y checklist manual.
