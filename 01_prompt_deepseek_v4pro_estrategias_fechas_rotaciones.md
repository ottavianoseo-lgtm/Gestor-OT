# Prompt para DeepSeek V4Pro — Corrección de Estrategias, Fechas y Rotaciones

## Rol

Eres **DeepSeek V4Pro**, especialista senior en **C# .NET 10, ASP.NET Core, EF Core, Blazor WebAssembly/Hosted y AntDesign Blazor**. Debes corregir bugs de producción en Gestor OT con criterio de dominio agropecuario.

## Requisito obligatorio de Context7 MCP

Antes de modificar código, debes usar obligatoriamente el **MCP de Context7** para consultar documentación actualizada de:

- .NET 10 / ASP.NET Core 10
- Blazor components, forms, binding y EventCallback
- EF Core 10 y patrones de validación/transactions
- AntDesign Blazor: `DatePicker`, `InputNumber`, `RadioGroup`, `Select`, `Form`

No avances sin dejar en tu respuesta técnica una sección llamada **“Documentación consultada vía Context7”** indicando qué librerías/componentes consultaste y qué decisión tomaste a partir de esa documentación.

## Rama y objetivo

Repositorio: `ottavianoseo-lgtm/Gestor-OT`  
Rama de trabajo: `fix/bugs-revision-gestor-ot`

Objetivo: corregir completamente el flujo de **Crear Labores desde Estrategia**, validando que cumpla la spec funcional y que no permita crear labores con conflictos bloqueantes de rotación.

## Archivos a revisar obligatoriamente

- `src/GestorOT.Client/Components/StrategyLaborWizard.razor`
- `src/GestorOT.Client/Pages/Estrategias.razor`
- `src/GestorOT.Client/Pages/PlaneamientoOriginal.razor`
- `src/GestorOT.Api/Controllers/LaborsController.cs`
- `src/GestorOT.Api/Controllers/RotationsController.cs`
- `src/GestorOT.Infrastructure/Services/AgronomicValidationService.cs`
- `src/GestorOT.Application/Services/IAgronomicValidationService.cs`
- DTOs relacionados en `src/GestorOT.Shared/Dtos/*Strategy*`, `*Labor*`, `*Rotation*`
- Tests existentes en `src/GestorOT.Tests`

## Bugs a corregir

### Bug 1 — “Mantener separación de fechas” no funciona

Situación actual esperada del producto:

- No debe existir una “Fecha Base” obligatoria en la pantalla inicial.
- En la vista previa, el usuario puede editar las fechas directamente.
- Si `Mantener separación de fechas` está activo y cambia una fecha, las demás fechas relacionadas deben recalcularse respetando los `DayOffset` de la estrategia.
- Si el checkbox está desactivado, cada fecha queda independiente.

Implementación requerida:

1. En `StrategyLaborWizard.razor`, agregar un handler explícito para cambio de fecha, por ejemplo:
   - `OnPreviewDateChanged(LaborPreview changedPreview, DateTime? newDate)`
2. Agrupar las labores por lote y estrategia aplicada.
3. Usar los `DayOffset` reales de cada `StrategyItem`.
4. Cuando se modifica una fecha y `ForceDateSeparation == true`:
   - Calcular la nueva fecha base implícita como `newDate - changedItem.DayOffset`.
   - Recalcular cada preview del mismo lote como `baseDate + item.DayOffset`.
5. Cuando `ForceDateSeparation == false`:
   - Solo modificar la fecha de la preview editada.
6. No usar `DateTime.Today` como una “fecha base” oculta que condicione el flujo sin explicar. Puede usarse como sugerencia inicial, pero debe quedar claro en código y tests que el usuario puede modificarlo y que no es un campo obligatorio.

### Bug 2 — Validación de rotación parseada incorrectamente

Situación actual:

- El frontend trata la respuesta de `api/labors/validate-rotation-activity` como string.
- El backend devuelve un objeto `LaborActivityValidationResult`.
- Se llama dos veces al endpoint para la misma preview.

Implementación requerida:

1. Crear/usar un DTO compartido para la respuesta de validación si todavía no existe.
2. Deserializar fuertemente la respuesta en `StrategyLaborWizard.razor`.
3. No mostrar JSON crudo en UI.
4. Si `Severity == "Error"`, marcar `HasError = true` y bloquear el botón `Crear Labores`.
5. Si `Severity == "Warning"`, mostrar advertencia y permitir continuar.
6. Si no hay rotación, permitir con advertencia según spec.
7. Si existe rotación activa y la actividad de la estrategia no coincide, bloquear.
8. Evitar llamadas duplicadas por cada labor preview.

### Bug 3 — Hectáreas deben mostrarse con dos decimales

Implementación requerida:

- En la preview de estrategia, mostrar hectáreas con 2 decimales.
- Mantener precisión interna `decimal`, pero la UI debe formatear `N2`.
- Validar que no se rompa la edición numérica.

### Bug 4 — Crear Labores desde Estrategia debe crear efectivamente todas las labores válidas

Revisar `CreateBulkFromStrategy` en backend.

Validar:

- Usa los overrides enviados desde la vista previa.
- Respeta `CampaignLotId`, no solo `LotId`.
- Usa la actividad de la estrategia.
- Copia insumos y dosis sugeridas de la estrategia.
- Si `ForceOriginalPlan == true`, fuerza `Status = Planned`, `Mode = Planned`, `IsOriginalPlan = true`.
- No crea ninguna labor si hay errores bloqueantes de rotación en el lote/fecha.
- Debe devolver errores y warnings estructurados.

## Criterios de aceptación funcional

- Al aplicar estrategia con labores Día 0, Día +5 y Día +10:
  - Si cambio la segunda labor del 20/05 al 25/05 y el checkbox está activo, las fechas deben quedar 20/05, 25/05 y 30/05.
  - Si el checkbox está inactivo, solo cambia esa labor.
- Si un lote tiene rotación activa de Soja y la estrategia es Maíz, el botón Crear Labores queda bloqueado para ese lote/fecha con mensaje claro.
- Si no existe rotación activa, se muestra advertencia pero se puede continuar.
- La preview muestra nombre de estrategia, actividad, lote, nombre real de cada labor, insumos, dosis y hectáreas `N2`.
- El botón `Crear Labores` crea la cantidad correcta de labores y refresca la pantalla llamadora.

## Tests obligatorios

Agregar tests, preferentemente en `src/GestorOT.Tests`, cubriendo:

1. `CreateBulkFromStrategy` crea N labores con fechas override.
2. `CreateBulkFromStrategy` bloquea conflicto de actividad vs rotación.
3. `CreateBulkFromStrategy` permite sin rotación con warning.
4. `ForceOriginalPlan` siempre crea `Planned/Planned`.
5. Lógica pura de cálculo de fechas: mantener separación ON/OFF.

Si la lógica de fecha hoy está en `.razor`, extraerla a una clase testeable, por ejemplo:

`StrategyDatePreviewService` o método estático en servicio de aplicación del cliente.

## Restricciones técnicas

- No introducir código deprecado.
- No mezclar responsabilidades de UI con reglas de negocio si puede extraerse a servicio testeable.
- No usar strings mágicos sin constantes/enum cuando ya existan DTOs/enums.
- Mantener compatibilidad con Blazor y serialización source-generated si el proyecto la usa.
- No romper `PlaneamientoOriginal.razor` ni `Estrategias.razor`.

## Entregable esperado

- Código corregido.
- Tests nuevos o actualizados.
- Breve informe con:
  - Archivos modificados.
  - Documentación consultada vía Context7.
  - Escenarios probados manualmente.
  - Comando exacto ejecutado: `dotnet build` y `dotnet test`.
