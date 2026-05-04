# Sprint 08 - Estrategias: wizard y creación masiva de labores

## Objetivo

Corregir el flujo de aplicar estrategias para que cree labores correctamente, con preview claro, fechas editables, validación de rotaciones y sin crear OTs automáticamente.

## Rama sugerida

`fix/s08-estrategias-wizard`

## Bugs cubiertos

- Debe haber un solo modal/flujo para crear labores desde estrategia.
- Botón `Crear Labores` no funciona.
- Preview no muestra nombre real de labor.
- Header no muestra estrategia.
- Fecha base precargada confunde.
- Hectáreas deben mostrarse con dos decimales.
- Propio/Contratista debe ser más claro.
- Falta validación actividad estrategia vs rotaciones.
- Estrategia no debe crear OTs automáticamente salvo flujo explícito.

## Archivos principales

- `src/GestorOT.Client/Pages/Estrategias.razor`
- `src/GestorOT.Client/Components/StrategyLaborWizard.razor`
- `src/GestorOT.Api/Controllers/LaborsController.cs`
- `src/GestorOT.Api/Controllers/StrategiesController.cs`
- `src/GestorOT.Shared/Dtos/StrategyDto.cs`
- `src/GestorOT.Shared/AppJsonSerializerContext.cs`
- DTOs de `BulkFromStrategyRequest`
- Servicios de validación agronómica

## Regla funcional

Aplicar una estrategia debe generar labores. La OT es un agrupador posterior u opcional.

## Diseño esperado

### Desde Estrategias

- Botón `Aplicar a Lotes`.
- Abre `StrategyLaborWizard`.
- Crea labores sueltas asociadas a campaña/lote.

### Desde OT

- Puede existir botón `Crear labores desde estrategia`.
- Usa el mismo wizard con `WorkOrderId` opcional.
- Las labores creadas quedan asociadas a la OT.

## Tareas técnicas

### 1. Unificar flujo

1. En `Estrategias.razor`, reemplazar flujo viejo de aplicar estrategia que crea OTs.
2. Usar `StrategyLaborWizard` como único flujo principal.
3. Deprecar visualmente o eliminar uso de `api/strategies/apply`.
4. Si se mantiene endpoint legacy, que no sea invocado por UI principal.

### 2. Parámetros del wizard

`StrategyLaborWizard` debe aceptar:

- estrategia inicial opcional;
- `WorkOrderId` opcional;
- `ForceOriginalPlan` opcional;
- campaña actual desde `CampaignState`.

### 3. Header de preview

Mostrar:

- Nombre de estrategia.
- Actividad ERP.
- Campaña seleccionada.
- Cantidad de lotes.
- Cantidad total de labores.

### 4. Selección de lotes

1. Listar lotes de la campaña seleccionada.
2. Usar `CampaignLotId`.
3. Mostrar:
   - lote;
   - campo;
   - superficie productiva con dos decimales.

### 5. Fechas

1. Quitar fecha base precargada como campo obligatorio visible.
2. El usuario trabaja en preview con fechas por labor.
3. Si `Mantener separación de fechas` está activo:
   - cambiar primera fecha recalcula las siguientes por offsets;
   - conservar diferencias de la estrategia.
4. Si está inactivo:
   - cada fecha se edita independientemente.
5. Al activar separación luego de edición manual, pedir confirmación.

### 6. Preview por fila

Cada fila debe mostrar:

- Lote.
- Campo.
- Labor real.
- Actividad heredada de estrategia.
- Fecha.
- Hectáreas `N2`.
- Responsable.
- Propio/Contratista como botones segmentados claros.
- Insumos.
- Dosis.
- Warning/error de rotación.

### 7. Validación de rotación

1. Validar contra `strategy.ErpActivityId`.
2. Si no hay rotación:
   - warning.
3. Si hay rotación y coincide:
   - ok.
4. Si hay rotación y no coincide:
   - error bloqueante.
5. El botón `Crear Labores` queda deshabilitado si hay errores bloqueantes.
6. El backend repite la validación.

### 8. Backend CreateBulkFromStrategy

1. Prevalidar todo antes de insertar.
2. Usar transacción.
3. No crear registros parciales si hay error bloqueante.
4. Crear labores con:
   - `CampaignLotId`;
   - `LotId` derivado;
   - `LaborTypeId`;
   - `ErpActivityId = strategy.ErpActivityId`;
   - `Hectares`;
   - `EstimatedDate`;
   - `ContactId`;
   - `IsExternalBilling`;
   - `IsOriginalPlan` si corresponde;
   - `WorkOrderId` si corresponde.
5. Crear insumos con dosis y totales.
6. Devolver:
   - cantidad creada;
   - warnings;
   - ids creados.

## No hacer en este sprint

- No rediseñar listado de estrategias si ya fue corregido en Sprint 07.
- No implementar adjuntos.
- No tocar campañas/lotes salvo compatibilidad.
- No crear nuevas entidades innecesarias.

## Pruebas manuales

### Caso 1 - Aplicar estrategia a lotes

1. Seleccionar campaña activa.
2. Crear estrategia Maíz con dos labores.
3. Aplicar a dos lotes.

Resultado esperado:

- Preview muestra 4 labores.
- Se ven nombres reales.
- `Crear Labores` crea 4 labores.

### Caso 2 - Fechas con separación

1. Estrategia con offsets 0, 5, 10.
2. Cambiar primera fecha a 15/05.

Resultado esperado:

- Fechas quedan 15/05, 20/05, 25/05.

### Caso 3 - Fechas independientes

1. Desactivar separación.
2. Cambiar segunda fecha.

Resultado esperado:

- Solo cambia esa fila.

### Caso 4 - Rotación conflictiva

1. Estrategia Soja.
2. Lote con rotación Maíz.

Resultado esperado:

- Preview marca error.
- No permite crear.
- Backend rechaza si se fuerza.

### Caso 5 - Propio/Contratista

1. En preview elegir Contratista para una labor y Propio para otra.

Resultado esperado:

- La elección queda clara.
- El payload refleja esa elección.

## Criterios de aceptación

- Un solo flujo principal para aplicar estrategia.
- Crea labores, no OTs automáticas.
- Preview claro.
- Fechas editables y separación funcional.
- Validación de rotación correcta.
- Botón Crear Labores funciona.
- Código compila.

## Prompt corto para DeepSeek

Implementá solo Sprint 08. Corregí StrategyLaborWizard y flujo Aplicar a Lotes para crear labores desde estrategia. Unificá el flujo, quitá creación automática de OTs, agregá preview completo, fechas editables, hectáreas N2, Propio/Contratista claro y validación de rotaciones contra actividad de estrategia. Usá transacción y no crees parciales si hay errores.
