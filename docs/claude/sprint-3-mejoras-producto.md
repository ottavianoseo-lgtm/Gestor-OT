# Sprint 3 — Mejoras de Producto
**GestorMax · Gestor OT** | Semanas 5–6 | Prioridad: MEDIA

---

## Objetivo

Incorporar mejoras funcionales que reducen la fricción operativa para el usuario final en el día a día: filtro de lotes por campo y asignación masiva de campaña desde la sección Campos.

> **Prerequisito:** Sprints 1 y 2 completados.

---

## Issues a resolver

### #14a — Filtro por Campo en selector de Lotes

**Severidad:** MEJORA  
**Archivos clave:** `CampaignLotEditor.razor`, `LotDto.cs`  
**Esfuerzo estimado:** 1 día

**Descripción:**  
El dropdown de selección de lotes no tiene ningún filtro previo. En instalaciones con muchos lotes (decenas o cientos), encontrar un lote específico requiere scrollear toda la lista, lo que degrada considerablemente la UX.

**Comportamiento esperado:**  
Aparece un selector opcional "Filtrar por Campo" encima del dropdown de lotes. Al seleccionar un campo, la lista de lotes se reduce a los lotes pertenecientes a ese campo. Si no se selecciona campo, se muestran todos los lotes (comportamiento actual).

**Tareas concretas:**
1. Agregar un `<select>` o componente equivalente de "Filtrar por Campo" encima del dropdown de lotes en `CampaignLotEditor.razor`.
2. Cargar la lista de campos disponibles en `OnInitializedAsync` (ya debería estar disponible en el contexto del componente).
3. Implementar el filtrado en memoria: cuando `selectedFieldId != null`, filtrar `_lots` por `lot.FieldId == selectedFieldId`.
4. Cuando el filtro de campo está vacío/ninguno, mostrar todos los lotes (sin filtro).
5. Al cambiar el campo seleccionado, resetear el lote seleccionado si ya no aparece en la lista filtrada.
6. El select de campo debe tener una opción inicial "— Todos los campos —" o similar.

**Consideraciones técnicas:**
- El filtrado es en memoria (no requiere nueva llamada al API) para minimizar latencia.
- Si en el futuro la cantidad de lotes crece significativamente, evaluar virtualización o paginación.

---

### #14b — Asignación masiva de Campaña desde sección Campo

**Severidad:** MEJORA  
**Archivos clave:** `Campanias.razor`, `CampaignsController.cs`, `CampaignManagerService.cs`  
**Esfuerzo estimado:** 2 días

**Descripción:**  
Actualmente, asignar una campaña a los lotes de un campo requiere hacerlo lote por lote. Con campos que tienen muchos lotes, esto es una tarea repetitiva y propensa a omisiones.

**Comportamiento esperado:**  
En el modal "Campos de Campaña", existe un botón "Asignar Lotes del Campo" que asigna automáticamente la campaña activa a **todos** los lotes del campo seleccionado en una sola operación.

**Tareas concretas:**

**Backend:**
1. Crear o verificar la existencia del endpoint: `POST /api/campaigns/{id}/lots/batch`  
   - Body: `{ fieldId: string }` (o lista de `lotIds`)
   - Comportamiento: asigna la campaña `id` a todos los lotes del campo `fieldId` que pertenezcan al tenant activo.
   - Debe ser idempotente: si un lote ya estaba asignado, no falla ni duplica.
2. Implementar la lógica en `CampaignManagerService.cs`:
   - Obtener todos los lotes del campo `fieldId` para el tenant activo.
   - Asignar la campaña a cada uno (bulk insert/update).
   - Retornar un resumen: `{ assigned: N, skipped: M }`.
3. Asegurar que el endpoint respete la autorización de tenant (no asignar lotes de otro tenant).

**Frontend:**
4. En el modal "Campos de Campaña" de `Campanias.razor`, agregar un botón "Asignar Lotes del Campo" visible cuando hay un campo seleccionado.
5. Al hacer clic, mostrar un diálogo de confirmación: *"¿Asignar la campaña [nombre] a todos los lotes del campo [nombre]? Esta acción afectará N lotes."*
6. Si el usuario confirma, llamar al endpoint batch y mostrar un mensaje de resultado: *"Se asignaron N lotes correctamente."*
7. Manejar el estado de carga durante la operación (deshabilitar el botón, mostrar spinner).
8. Recargar la lista de lotes del campo para reflejar el nuevo estado.

---

## Criterios de aceptación del Sprint 3

- [ ] En `CampaignLotEditor`, existe un selector "Filtrar por Campo" que reduce la lista de lotes al campo elegido.
- [ ] Cuando no se selecciona ningún campo, se muestran todos los lotes.
- [ ] Al cambiar el campo, el lote previamente seleccionado se resetea si ya no aparece en la lista filtrada.
- [ ] En el modal "Campos de Campaña", el botón "Asignar Lotes del Campo" está presente y funcional.
- [ ] Al ejecutar la asignación masiva, se muestra confirmación antes de proceder.
- [ ] Tras la asignación, la lista de lotes del campo refleja el nuevo estado.
- [ ] El endpoint de asignación batch es idempotente y respeta el tenant activo.

---

## Notas para el agente

- Para #14b, acordar con el equipo de negocio si la asignación masiva debe **desasignar** lotes del campo de campañas anteriores, o simplemente agregar la nueva campaña sin tocar otras asignaciones. El comportamiento más seguro es **solo agregar**, sin remover.
- El botón de asignación masiva debe estar deshabilitado si no hay un campo seleccionado.
- Considerar agregar un log de auditoría de la operación masiva (quién la ejecutó, cuándo, cuántos lotes afectados).
