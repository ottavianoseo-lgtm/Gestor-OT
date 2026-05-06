# Módulo 02 — Campaña, lotes, superficies y GIS

Bugs incluidos: BUG-02, BUG-03, BUG-04, BUG-05, BUG-06, BUG-08 y BUG-09.

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

Todo debe operar dentro de Empresa + Campaña. Las campañas bloqueadas deben poder seleccionarse para consulta, pero no permitir altas, ediciones ni eliminaciones. Las campañas futuras pueden seleccionarse para planificar, pero no deben seleccionarse automáticamente.

## Archivos a revisar

- `src/GestorOT.Client/Layout/CampaignSelector.razor`
- `src/GestorOT.Client/Services/CampaignState.cs`
- `src/GestorOT.Client/Pages/Campaigns.razor`
- `src/GestorOT.Api/Controllers/CampaignsController.cs`
- `src/GestorOT.Api/Controllers/LotsController.cs`
- `src/GestorOT.Client/Pages/Lotes.razor`
- `src/GestorOT.Client/Pages/CampaignLotEditor.razor`
- `src/GestorOT.Client/Pages/Mapa.razor`
- `src/GestorOT.Client/Components/LaborEditorForm.razor`
- `src/GestorOT.Client/Pages/LaboresSueltas.razor`
- `mapInterop.js` o archivo equivalente de JS Interop.

## BUG-03 — Selector de campaña colgado

### Implementación requerida

1. Reemplazar handlers `async void` por patrón seguro: `Task` interno + captura de excepciones.
2. Garantizar que `_loading = false` siempre se ejecute en `finally`.
3. Evitar loops entre `NotifyCampaignsChanged`, `SetCampaign` y `OnChange`.
4. Al crear una campaña nueva, refrescar listado sin seleccionar automáticamente la nueva campaña.
5. Rehidratar selección desde `localStorage` solo si la campaña existe y sigue disponible.
6. Si hay campañas múltiples y no hay selección, abrir modal, pero no bloquear navegación.

### Aceptación

- Crear campaña no deja spinner infinito.
- La nueva campaña aparece sin Ctrl+F5.
- No se fuerza selección automática.
- No hay llamadas infinitas al backend.

## BUG-02 — Labores sueltas sin filtro por campaña

### Implementación requerida

1. En `LaboresSueltas.razor`, agregar `campaignId=CampaignState.CurrentCampaign.Id` al query `api/labors`.
2. Agregar opción visible “Ver todas” para omitir filtro de campaña.
3. Mantener filtros existentes: asignación, estado, tipo, responsable, orden.
4. Mostrar indicador de campaña activa.

### Aceptación

- Con campaña seleccionada, solo se ven labores de esa campaña.
- “Ver todas” muestra todas las campañas si el usuario lo decide.
- Campaña sin labores muestra estado vacío claro.

## BUG-09 — Campo y lote en crear labor

### Implementación requerida

1. En `LaborEditorForm`, derivar `_fields` desde `_lots.Select(l => l.FieldId/FieldName)` cuando hay campaña activa.
2. No mostrar campos que no tengan lotes asignados a la campaña.
3. Si `_lots.Count == 0`, mostrar alerta: “La campaña actual no tiene lotes asignados.”
4. Bloquear botón guardar hasta seleccionar lote válido.
5. Mantener búsqueda y filtro por campo.

### Aceptación

- Campaña sin lotes: campo y lote aparecen vacíos con aviso claro.
- No se muestran campos irrelevantes.
- Campaña con lotes: campo filtra lote correctamente.

## BUG-05 y BUG-08 — Superficies

### Implementación requerida

1. Validar `LotsController.UpdateLot`: si `dto.CadastralArea > 0`, persistir aunque no venga `WktGeometry`.
2. Revisar que `LotDto` y formulario envíen `CadastralArea`.
3. Mostrar todas las superficies con `N2`.
4. Aplicar formato a superficie productiva en campaña-lote, lote y GIS.
5. No pisar superficie manual cuando se edita geometría, salvo decisión explícita.

### Aceptación

- Editar hectáreas catastrales persiste.
- Reabrir lote muestra valor nuevo.
- Superficies visibles muestran 2 decimales.
- GIS no sobreescribe manualmente sin intención.

## BUG-06 — Campaña 27/28 no asignable

### Implementación requerida

1. Revisar endpoint de asignación campaña-lote.
2. Validación de duplicados debe contemplar tenant, lotId, campaignId y estado lógico si existe soft delete.
3. Si existe registro inactivo/eliminado, no debe bloquear o debe reactivarse según diseño.
4. Agregar respuesta de error con IDs y motivo funcional, no mensaje genérico.

### Aceptación

- Campaña 27/28 puede asignarse si no está asignada activamente.
- No se crean duplicados activos.
- Otras campañas siguen funcionando.

## BUG-04 — Edición GIS

### Implementación requerida

1. Localizar botón de edición en mapa.
2. Implementar modo edición real en JS Interop con Leaflet/Leaflet.draw o librería ya instalada.
3. Permitir mover vértices o reemplazar polígono.
4. Capturar geometría modificada y devolver WKT/GeoJSON al componente Blazor.
5. Guardar geometría vía API.
6. Mantener área manual salvo decisión explícita del usuario.

### Aceptación

- Botón activa modo edición.
- Usuario puede modificar vértices.
- Guardar persiste geometría.
- Reabrir lote muestra geometría actualizada.
- Lote sin geometría permite crear polígono.

## Pruebas de regresión

1. Crear campaña nueva.
2. Seleccionar campaña activa.
3. Seleccionar campaña bloqueada y verificar solo lectura.
4. Asignar campaña a lote.
5. Editar superficie manual.
6. Editar geometría.
7. Crear labor en campaña con lotes.
8. Crear labor en campaña sin lotes.
9. Ver labores sueltas filtradas por campaña.
