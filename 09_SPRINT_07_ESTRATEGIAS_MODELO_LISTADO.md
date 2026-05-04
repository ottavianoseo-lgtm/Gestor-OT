# Sprint 07 - Estrategias: modelo funcional, listado y nombres reales

## Objetivo

Corregir la base funcional de estrategias: una estrategia tiene una sola actividad ERP/cultivo y sus labores heredan esa actividad. El listado debe mostrar nombres reales de actividad, labor e insumos.

## Rama sugerida

`fix/s07-estrategias-modelo-listado`

## Bugs cubiertos

- Campo `Cultivo/Actividad` por labor de estrategia no tiene sentido.
- Estrategia muestra `Labor` genérico.
- Lista de estrategias no muestra actividad, labores e insumos correctamente.
- Superposición visual de botones de eliminar/quitar labor.

## Archivos principales

- `src/GestorOT.Client/Pages/Estrategias.razor`
- `src/GestorOT.Api/Controllers/StrategiesController.cs`
- `src/GestorOT.Shared/Dtos/StrategyDto.cs`
- `src/GestorOT.Domain/Entities/CropStrategy.cs`
- `src/GestorOT.Domain/Entities/StrategyItem.cs`
- Configuración EF de estrategias
- Inventario/insumos si se requiere resolver nombres
- Catálogo de tipos de labor
- Catálogo de actividades ERP

## Regla funcional

La estrategia representa un plan de labores para un cultivo/actividad. Por eso:

- `CropStrategy.ErpActivityId` define la actividad/cultivo.
- `StrategyItem` define tipo de labor, offset de días e insumos.
- `StrategyItem.ErpActivityId` no debe usarse como actividad funcional.
- Si queda en el modelo por compatibilidad, debe considerarse legacy.

## Tareas técnicas

### 1. UI - Modal de estrategia

En `Estrategias.razor`:

1. Mantener campo `Actividad ERP` solo a nivel estrategia.
2. Quitar campo `Cultivo/Actividad` por cada item/labor.
3. Cada item debe tener:
   - tipo de labor;
   - días de espera/offset;
   - insumos por defecto;
   - dosis;
   - unidad.
4. Mejorar layout para que no se superpongan botones.
5. Cada fila de labor debe tener una columna clara de acciones.

### 2. Backend - GetStrategies

En `StrategiesController.GetStrategies`:

1. Incluir actividad de estrategia.
2. Incluir tipo de labor de cada item.
3. Resolver nombres de insumos si se almacenan por JSON con `SupplyId`.
4. Poblar en DTO:
   - `ErpActivityName`
   - `LaborTypeName`
   - `SupplyName`

### 3. Backend - Create/Update

1. Guardar `ErpActivityId` en estrategia.
2. Al guardar items, no exigir ni usar actividad por item.
3. Si llega `StrategyItem.ErpActivityId`, ignorarlo o igualarlo a la actividad de estrategia, documentando decisión.
4. Validar:
   - nombre obligatorio;
   - actividad obligatoria;
   - al menos una labor;
   - cada labor con tipo de labor;
   - insumos con dosis válida si existen.

### 4. Datos existentes

Si hay estrategias viejas con `ErpActivityId` en items:

1. No romperlas.
2. Mostrar actividad de estrategia.
3. Si estrategia no tiene actividad, inferir de items solo como migración/compatibilidad y pedir corrección al usuario.

## No hacer en este sprint

- No corregir aún el wizard de creación masiva completo.
- No tocar adjuntos.
- No crear OTs desde estrategia.
- No modificar reglas de rotación más allá de preparar DTO.

## Pruebas manuales

### Caso 1 - Crear estrategia

1. Crear estrategia `Maíz temprano`.
2. Seleccionar actividad ERP `Maíz`.
3. Agregar dos labores.
4. Agregar insumos.

Resultado esperado:

- No aparece campo actividad por labor.
- Guarda.
- Lista muestra `Actividad: Maíz`.
- Lista muestra nombres reales de labores.
- Lista muestra insumos y dosis.

### Caso 2 - Editar estrategia

1. Editar estrategia.
2. Agregar/quitar labor.
3. Guardar.

Resultado esperado:

- No hay superposición visual.
- Persisten items e insumos.

### Caso 3 - Datos legacy

1. Abrir estrategia existente.

Resultado esperado:

- No rompe.
- Si falta actividad de estrategia, se informa o se usa fallback controlado.

## Criterios de aceptación

- Actividad única a nivel estrategia.
- No hay actividad por item en UI.
- Listado muestra nombres reales.
- Insumos visibles.
- Layout no superpone acciones.
- Código compila.

## Prompt corto para DeepSeek

Implementá solo Sprint 07. Corregí modelo/listado de estrategias: una sola actividad ERP por estrategia, quitar actividad por labor en UI, poblar nombres reales de actividad, tipo de labor e insumos desde backend y arreglar layout del modal. No implementes todavía el wizard de creación de labores.
