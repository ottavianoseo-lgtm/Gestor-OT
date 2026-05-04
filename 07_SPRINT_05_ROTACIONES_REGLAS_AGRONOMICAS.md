# Sprint 05 - Rotaciones y reglas agronómicas

## Objetivo

Unificar la regla funcional de rotaciones para labores manuales, labores desde estrategia, OT y Planeamiento Original.

## Rama sugerida

`fix/s05-rotaciones-reglas`

## Bugs cubiertos

- Falta chequeo actividad de estrategia contra rotaciones.
- Actividad puede cargarse aunque contradiga rotación.
- Sin rotación debe ser warning, no error.
- La ruta o contrato usa confusamente `lotId` / `campaignLotId`.

## Archivos principales

- `src/GestorOT.Client/Components/LaborEditorForm.razor`
- `src/GestorOT.Client/Components/StrategyLaborWizard.razor`
- `src/GestorOT.Api/Controllers/LaborsController.cs`
- Controladores de rotaciones/campañas
- `src/GestorOT.Infrastructure/Services/RotationService.cs`
- `src/GestorOT.Application/Services/AgronomicValidationService.cs`
- DTOs de rotación

## Regla funcional

Para cada labor:

1. Buscar rotación activa para `CampaignLotId + Fecha`.
2. Si no existe:
   - warning;
   - permitir guardar.
3. Si existe:
   - actividad de labor debe coincidir con actividad de rotación;
   - si coincide, permitir;
   - si no coincide, bloquear.
4. Si existe rotación, la UI debe bloquear el selector de actividad.

## Tareas técnicas

### 1. Contrato claro

Definir y documentar un método único:

`ValidateLaborActivity(campaignLotId, date, activityId)`

Debe devolver:

- `IsValid`
- `Severity`: `None`, `Warning`, `Error`
- `Message`
- `ExpectedActivityId`
- `ExpectedActivityName`
- `ReceivedActivityId`
- `ReceivedActivityName`

### 2. Backend

1. Usar la validación en `CreateLabor`.
2. Usar la validación en `UpdateLabor`.
3. Usar la validación en `CreateBulkFromStrategy`.
4. No confiar en advertencias del frontend.
5. Si error, no guardar.
6. Si warning, guardar y devolver warning.

### 3. Frontend labor manual

1. Al seleccionar lote o cambiar fecha:
   - consultar rotación activa.
2. Si hay rotación:
   - setear actividad;
   - bloquear selector;
   - mostrar tag `Planeada por rotación`.
3. Si no hay rotación:
   - permitir seleccionar actividad;
   - mostrar tag `Sin rotación`.
4. Si backend devuelve warning al guardar, mostrarlo.

### 4. Frontend estrategia

1. En preview, validar cada fila.
2. Si sin rotación:
   - fila con warning.
3. Si rotación conflictiva:
   - fila con error bloqueante.
4. Botón `Crear Labores` deshabilitado si hay errores bloqueantes.
5. La validación debe usar actividad de estrategia, no actividad del item.

### 5. Nombres de rutas

Si hoy hay una ruta con forma:

`api/campaigns/{campaignId}/lots/{campaignLotId}/rotations/active`

Documentar o renombrar internamente el parámetro para que no haya confusión. Mantener compatibilidad si ya está usada.

## No hacer en este sprint

- No rediseñar toda la pantalla de rotaciones.
- No modificar estrategias salvo validación contra actividad.
- No tocar adjuntos.

## Pruebas manuales

### Caso 1 - Sin rotación

1. Crear labor en lote/campaña/fecha sin rotación.

Resultado esperado:

- Muestra warning.
- Permite guardar.

### Caso 2 - Con rotación coincidente

1. Lote con rotación Maíz.
2. Crear labor en esa fecha.

Resultado esperado:

- Actividad Maíz se carga y bloquea.
- Guarda.

### Caso 3 - Con rotación conflictiva

1. Lote con rotación Maíz.
2. Forzar labor Soja vía API.

Resultado esperado:

- Backend rechaza.

### Caso 4 - Estrategia

1. Estrategia Maíz.
2. Lote con rotación Maíz.
3. Crear desde estrategia.

Resultado esperado:

- Permite.

4. Estrategia Soja.
5. Lote con rotación Maíz.

Resultado esperado:

- Preview marca error.
- Backend bloquea.

## Criterios de aceptación

- Regla implementada una sola vez a nivel servicio.
- UI y backend coherentes.
- Warning sin rotación no bloquea.
- Conflicto de actividad bloquea.
- Estrategias validan actividad de estrategia.

## Prompt corto para DeepSeek

Implementá solo Sprint 05. Centralizá la validación de rotación usando CampaignLotId + fecha + actividad. Si no hay rotación, warning y permite. Si hay rotación con actividad distinta, bloquear. Aplicalo a labor manual, update labor y creación masiva desde estrategia. No hagas refactor visual amplio.
