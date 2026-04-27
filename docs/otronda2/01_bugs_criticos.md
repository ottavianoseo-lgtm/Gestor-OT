# Sub-plan 01 — Bugs Críticos

**Prioridad**: 🔴 CRÍTICO — resolver antes que cualquier otra tarea  
**Área**: Transversal (Labores, Lotes, Mapa, Tabla OTs)

---

## BUG-01: Labor no se guarda al hacer clic en "Guardar" desde el modal

**Síntoma**: Se completa toda la info de una Labor, se presiona Guardar, el modal se cierra pero la labor no aparece en la lista.

**Archivos a revisar**:
- `src/GestorOT.Client/Components/LaborEditorForm.razor` — lógica del `OnValidSubmit` / botón Guardar
- `src/GestorOT.Api/Controllers/LaborsController.cs` — endpoint POST/PUT
- `src/GestorOT.Shared/Dtos/LaborDto.cs` — verificar que todos los campos requeridos estén presentes al serializar

**Qué hacer**:
1. Agregar `console.log` / breakpoint en el submit del form para verificar si el payload llega al server.
2. Revisar si hay validación silenciosa que falla y cierra el modal sin mostrar error.
3. Asegurar que el endpoint devuelva 200/201 y que el frontend lo maneje correctamente y recargue la lista.

---

## BUG-02: Campaña "28/29" aparece en selector de Labor pero no en la sección de Campañas

**Síntoma**: Al seleccionar campaña en el contexto de una Labor, aparece una campaña que no está listada en `Campanias.razor`.

**Archivos a revisar**:
- `src/GestorOT.Api/Controllers/CampaignsController.cs` — verificar si el endpoint de listado tiene filtro de estado activo que excluye campañas cerradas
- `src/GestorOT.Client/Layout/CampaignSelector.razor` — filtros aplicados al cargar campañas

**Qué hacer**:
1. Asegurar que el query de campañas para el selector de Labores y el de la página de Campañas usen el mismo endpoint o el mismo filtro.
2. Si la campaña está en estado borrador/cerrado, decidir si debe o no aparecer en el selector.

---

## BUG-03: Lote con Geometría aparece como huérfano al asignar polígono

**Síntoma**: Al intentar asignar un polígono a un Lote, el sistema ofrece como candidato un Lote que ya tiene Geometría asignada.

**Archivos a revisar**:
- `src/GestorOT.Client/Pages/Mapa.razor` — lógica de carga de lotes sin geometría
- `src/GestorOT.Api/Controllers/LotsController.cs` — endpoint que retorna lotes "huérfanos" o sin polígono

**Qué hacer**:
1. El endpoint o query que lista lotes disponibles para asignar geometría debe filtrar `WHERE Geometry IS NULL`.
2. Verificar que tras guardar una geometría, el lote desaparezca de esa lista (invalidar caché / recargar).

---

## BUG-04: Filtro de columna rompe visual de la tabla

**Síntoma**: Al hacer clic en un filtro A>Z o Z>A en cualquier tabla, la visual se rompe.

**Archivos a revisar**:
- Cualquier página que use `<Table>` de AntDesign Blazor con `Sortable` en columnas (principalmente `Lotes.razor`, `LaboresSueltas.razor`, `OrdenesTrabajos.razor`)

**Qué hacer**:
1. Revisar si el problema es CSS (z-index, overflow) o de estado (la lista se reordena pero `StateHasChanged` no se dispara).
2. Probar reemplazando `Sortable` nativo de la tabla por ordenamiento manual en `@code` con `IComparer`.
3. Si es CSS, agregar regla que resuelva el conflicto de layout al activar sort.

---

## BUG-05: Labor abierta desde OT trae Lote vacío en el modal

**Síntoma**: Al abrir una Labor que tiene un Lote asignado, el campo "Lote" aparece vacío dentro del modal.

**Archivos a revisar**:
- `src/GestorOT.Client/Components/LaborEditorForm.razor` — carga inicial del form cuando se edita
- `src/GestorOT.Api/Controllers/LaborsController.cs` — verificar que el GET de labor incluya `LotId` en el DTO
- `src/GestorOT.Shared/Dtos/LaborDto.cs`

**Qué hacer**:
1. Confirmar que `LaborDto` expone `LotId` (y `LotName` para display).
2. En el form de edición, asegurarse de que el `Select` de Lote inicialice con `@bind-Value="_formModel.LotId"` y que ese valor sea seteado al cargar.

---

## BUG-06: Botones GIS se superponen al menú "Polígono Dibujado" en el Mapa

**Síntoma**: Al dibujar un polígono, los botones de GIS se superponen visualmente al menú contextual de Polígono Dibujado.

**Archivos a revisar**:
- `src/GestorOT.Client/Pages/Mapa.razor` — estilos y lógica de visibilidad de botones GIS

**Qué hacer**:
1. Ocultar (o reducir `z-index` de) los botones GIS mientras el menú de Polígono Dibujado esté visible.
2. Usar una variable de estado `_polygonMenuVisible` que controle la visibilidad de los botones GIS:
   ```csharp
   // Cuando aparece el menú de polígono dibujado:
   _polygonMenuVisible = true;  // oculta botones GIS
   // Cuando se descarta el menú:
   _polygonMenuVisible = false; // vuelven a aparecer
   ```
3. En el markup: `@if (!_polygonMenuVisible) { /* botones GIS */ }`
