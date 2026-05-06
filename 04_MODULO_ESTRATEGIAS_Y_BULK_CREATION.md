# Módulo 04 — Estrategias y creación masiva de labores

Bugs incluidos: BUG-07, BUG-18, BUG-19, BUG-20 y BUG-21.

## Uso obligatorio de MCP Context7

Antes de editar código en este módulo, el agente debe consultar Context7 y registrar en su reporte final qué documentación usó.

Consultas mínimas obligatorias:

```text
/context7 .NET 10 ASP.NET Core Blazor
/context7 Entity Framework Core 10
/context7 AntDesign Blazor
```

Si el módulo toca GIS, agregar:

```text
/context7 Leaflet
/context7 Leaflet.draw
/context7 Blazor JS Interop
```

Si el agente no puede usar Context7, debe detener la implementación del módulo y reportar el bloqueo. No debe improvisar cambios de APIs, componentes ni patrones.


## Objetivo funcional

La estrategia debe funcionar como matriz confiable de labores para una actividad/cultivo. Debe respetar orden, días entre labores, insumos sugeridos, lote-campaña, superficie productiva y rotación.

## Archivos a revisar

- `src/GestorOT.Client/Components/StrategyLaborWizard.razor`
- Pantalla de creación/edición de Estrategias.
- `src/GestorOT.Api/Controllers/StrategiesController.cs`
- `src/GestorOT.Api/Controllers/LaborsController.cs`
- DTOs de estrategia y bulk creation.
- Entidades de Strategy / StrategyLabor / Labor.
- Validaciones agronómicas y rotaciones.

## Reglas funcionales

1. Una estrategia pertenece a una actividad/cultivo.
2. Las labores de estrategia heredan actividad de la estrategia.
3. No debe permitirse actividad distinta por labor dentro de la estrategia.
4. Los insumos y dosis son sugeridos.
5. Al aplicar estrategia, se crean labores sueltas salvo que la pantalla defina otro destino.
6. La fecha base es sugerida y editable en vista previa.
7. Mantener separación de fechas debe recalcular fechas respetando offsets.
8. Sin mantener separación, cada fecha queda independiente.
9. La validación de rotación aplica también a estrategias.

## BUG-07 — Separador de días

### Implementación requerida

1. Renderizar separador de días entre labores consecutivas.
2. No mostrar separador en la última labor.
3. Persistir separadores por relación entre items, no como campo confuso de la última labor.
4. Probar con 1, 2 y 3+ labores.

### Aceptación

- La última labor no muestra días de separación.
- Guardar y reabrir conserva separaciones.

## BUG-18 — Orden invertido

### Implementación requerida

1. Revisar orden en frontend antes de guardar.
2. Revisar orden en backend al persistir.
3. Agregar `SortOrder` o usar campo existente.
4. Al leer estrategia, ordenar por `SortOrder` ascendente.
5. Al crear labores desde estrategia, respetar ese orden.

### Aceptación

- Labor 1, 2, 3 se guardan y reabren en el mismo orden.
- El bulk creation respeta orden.

## BUG-19 — Filtro por campo al crear labores desde estrategia

### Implementación requerida

1. Agregar selector de Campo en el wizard.
2. Mostrar solo campos con lotes asignados a campaña activa.
3. Filtrar lotes por campo.
4. Si no hay campaña o no hay lotes, mostrar aviso claro.
5. No permitir seleccionar lotes de otra campaña.

### Aceptación

- Campo filtra lotes.
- No aparecen campos sin lotes de campaña activa.
- Las labores creadas usan `CampaignLotId`.

## BUG-20 — Eliminar labor individual

### Implementación requerida

1. Agregar botón eliminar por item de labor en estrategia.
2. Recalcular orden y separadores.
3. Confirmar eliminación si el item tiene insumos.
4. No permitir estrategia sin labores si la regla funcional exige al menos una.

### Aceptación

- Se elimina una labor individual.
- El orden se recalcula.
- Los separadores quedan consistentes.

## BUG-21 — Error al confirmar labores desde estrategia

### Implementación requerida

1. Revisar request del wizard hacia `api/labors/bulk-from-strategy`.
2. Validar que envía:
   - `StrategyId`
   - `CampaignLotIds`
   - Fechas por labor
   - `CampaignLotId` real, no solo `LotId`
   - `ErpActivityId`
   - `LaborTypeId`
   - Hectáreas
   - Insumos/dosis si corresponden
3. El backend debe validar campaña bloqueada.
4. El backend debe validar rotación: si actividad no coincide con rotación activa, bloquear; si no hay rotación, advertir.
5. En errores parciales, devolver detalle por lote/labor.
6. Usar transacción: si falla una labor crítica, no dejar creación parcial salvo que se diseñe explícitamente como parcial con reporte.

### Aceptación

- Se puede confirmar creación desde estrategia.
- Las labores creadas aparecen en Labores Sueltas.
- Respetan campaña activa.
- Tienen actividad de la estrategia.
- Tienen fechas correctas.
- Tienen insumos sugeridos.
- Si hay error, el usuario sabe qué lote/labor falló.

## Pruebas de regresión

1. Crear estrategia con 1 labor.
2. Crear estrategia con 3 labores y separadores.
3. Eliminar labor intermedia.
4. Guardar y reabrir.
5. Aplicar estrategia a lotes de un campo.
6. Aplicar estrategia a lotes de varios campos.
7. Probar campaña bloqueada.
8. Probar lote sin rotación.
9. Probar lote con rotación de otra actividad.
10. Verificar labores creadas en listado filtrado por campaña.
