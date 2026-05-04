# Sprint 06 - Planeamiento Original

## Objetivo

Corregir el módulo de Planeamiento Original para que funcione como línea base inmutable de campaña y no permita crear labores realizadas.

## Rama sugerida

`fix/s06-planeamiento-original`

## Bugs cubiertos

- Planeamiento Original permite crear labor `Realizada`.
- Guardar labor base no crea o no aparece.
- El listado no necesariamente filtra por campaña seleccionada.
- Debe ser lectura cuando la campaña está bloqueada.

## Archivos principales

- `src/GestorOT.Client/Pages/PlaneamientoOriginal.razor`
- `src/GestorOT.Client/Components/LaborEditorForm.razor`
- `src/GestorOT.Client/Components/StrategyLaborWizard.razor`
- `src/GestorOT.Api/Controllers/LaborsController.cs`
- `src/GestorOT.Shared/Dtos/LaborDto.cs`
- Servicios de validación de labor

## Regla funcional

Planeamiento Original representa el plan inicial/base de la campaña. No es una carga de ejecución.

Toda labor creada desde este módulo debe quedar:

- `IsOriginalPlan = true`
- `Status = Planned`
- `Mode = Planned`
- Asociada a campaña/lote mediante `CampaignLotId`

## Tareas técnicas

### 1. Pantalla PlaneamientoOriginal

1. Exigir campaña seleccionada.
2. Si no hay campaña, mostrar alerta y deshabilitar botones.
3. Si campaña bloqueada, mostrar tabla en solo lectura.
4. Al cargar datos, filtrar por:
   - `isOriginalPlan = true`
   - `campaignId = campaña seleccionada`
5. Al guardar una labor base, cerrar modal y recargar tabla.

### 2. LaborEditorForm con ForceOriginalPlan

Cuando `ForceOriginalPlan = true`:

1. Ocultar selector de estado o dejarlo fijo en `Planeada`.
2. No mostrar opción `Realizada`.
3. Setear:
   - `IsOriginalPlan = true`
   - `Status = Planned`
   - `Mode = Planned`
4. Si el usuario cambia de alguna forma el estado, restaurar `Planned`.
5. No permitir ejecución directa desde este modal.

### 3. Backend

En `CreateLabor` y `UpdateLabor`:

1. Si `IsOriginalPlan == true`:
   - rechazar `Status != Planned`;
   - rechazar `Mode != Planned`;
   - rechazar `ExecutionDate` como fecha de ejecución real si se diferencia de fecha estimada.
2. Guardar `IsOriginalPlan`.
3. Asegurar que la consulta de labores permite filtrar por `campaignId`.

### 4. Wizard de estrategia en Planeamiento Original

Si el wizard se usa desde Planeamiento Original:

1. Forzar `IsOriginalPlan = true`.
2. Forzar estado `Planned`.
3. Ocultar opción `Realized`.
4. Crear labores base.
5. No crear OTs automáticamente.

## No hacer en este sprint

- No rehacer todo el wizard de estrategia salvo parámetros necesarios.
- No implementar adjuntos.
- No tocar estados de OT.

## Pruebas manuales

### Caso 1 - Nueva Labor Base

1. Seleccionar campaña activa.
2. Ir a Planeamiento Original.
3. Click `Nueva Labor Base`.
4. Completar datos.
5. Guardar.

Resultado esperado:

- No existe opción `Realizada`.
- Guarda.
- Aparece en tabla.
- Tiene `IsOriginalPlan = true`.
- Tiene estado `Planned`.

### Caso 2 - API protegida

1. Enviar request `IsOriginalPlan=true` y `Status=Realized`.

Resultado esperado:

- Backend rechaza.

### Caso 3 - Campaña bloqueada

1. Seleccionar campaña bloqueada.
2. Ir a Planeamiento Original.

Resultado esperado:

- Se puede consultar.
- No se puede crear ni desanclar salvo rol/regla explícita.

### Caso 4 - Filtro por campaña

1. Crear labor base en campaña A.
2. Cambiar a campaña B.

Resultado esperado:

- No aparece la labor de campaña A.

## Criterios de aceptación

- Planeamiento Original nunca crea realizadas.
- Guardar labor base funciona.
- La tabla filtra por campaña.
- Backend protegido.
- Campaña bloqueada es solo lectura.

## Prompt corto para DeepSeek

Implementá solo Sprint 06. Corregí Planeamiento Original para que requiera campaña, filtre por campaña, cree únicamente labores planificadas con IsOriginalPlan=true y bloquee cualquier intento de crear Realized desde UI o API. No implementes adjuntos ni rediseñes estrategias salvo compatibilidad del wizard.
