# Sprint 2 — UX, Consistencia y Estados
**GestorMax · Gestor OT** | Semanas 3–4 | Prioridad: ALTA

---

## Objetivo

Corregir los issues de UX que afectan la fluidez de trabajo diario del usuario: comportamiento del mapa, sincronización de estados de OT, nomenclatura y jerarquía visual de la interfaz.

> **Prerequisito:** Sprint 1 completado y mergeado.

---

## Issues a resolver

### #7 — Mapa hace zoom-out al asignar lote a geometría

**Severidad:** ALTO  
**Archivos clave:** `Mapa.razor → SaveDrawnGeometry()`, `map.js`  
**Esfuerzo estimado:** 1 día

**Causa raíz:**  
Tras el éxito del `PUT` en `SaveDrawnGeometry()`, se llama a `ReloadMap()` que reinicializa el mapa con `fitBounds()` globales sobre todos los lotes, perdiendo el viewport actual del usuario.

**Tareas concretas:**
1. Antes de llamar a `ReloadMap()`, capturar el viewport actual: `map.getBounds()` desde JS.
2. Pasar los bounds capturados a `ReloadMap()` como parámetro opcional.
3. Después del reload, restaurar el viewport con `map.fitBounds(savedBounds)` en lugar del `fitBounds` automático.
4. Si no hay bounds previos (primer carga), mantener el comportamiento actual de `fitBounds` global.

---

### #8 — Sin acceso al modal de Lote haciendo clic en el mapa

**Severidad:** ALTO  
**Archivos clave:** `Mapa.razor`, `map.js`, `Lotes.razor`  
**Esfuerzo estimado:** 1 día

**Descripción:**  
Al hacer clic en un polígono de lote en el mapa Leaflet, no hay ninguna acción. El usuario no puede acceder al detalle o edición del lote desde el mapa.

**Tareas concretas:**
1. Al registrar los polígonos en `map.js`, agregar un `popup` con HTML que incluya un botón o enlace "Ver / Editar Lote".
2. Exponer una función JS-invokable desde Blazor (`[JSInvokable]`) que reciba el `lotId` y abra el modal de edición del lote correspondiente en `Lotes.razor`.
3. El popup debe mostrar al menos: nombre del lote, campo, superficie.
4. Verificar que el popup funcione correctamente tanto en lotes con geometría dibujada como en lotes importados.

---

### #10 — Estados de OT hardcodeados, no sincronizados con admin

**Severidad:** ALTO  
**Archivos clave:** `OrdenesTrabajos.razor`, `WorkOrderStatusesController.cs`  
**Esfuerzo estimado:** 1 día

**Causa raíz:**  
`OrdenesTrabajos.razor` tiene hardcodeados los `SelectOption` de Estado (`'Draft'`, `'Pending'`, `'InProgress'`). El sistema ya cuenta con un endpoint `GET /api/workorder-statuses` y una pantalla de administración para configurarlos dinámicamente, pero no están conectados.

**Tareas concretas:**
1. En `OnInitializedAsync` de `OrdenesTrabajos.razor`, llamar a `GET /api/workorder-statuses` y cargar la lista en una colección local.
2. Reemplazar los `SelectOption` hardcodeados por un loop sobre esa colección, usando el campo `Name` del `WorkOrderStatusDto` como etiqueta y `Id` como valor.
3. Manejar el estado de carga (spinner) mientras se obtienen los estados.
4. Manejar el caso de error (lista vacía o endpoint no disponible) con un fallback visual.

---

### #11 — Orden visual Empresa/Campaña invertido

**Severidad:** MEJORA  
**Archivos clave:** `MainLayout.razor`, Layout CSS  
**Esfuerzo estimado:** 0.5 días

**Descripción:**  
En la navegación lateral, el `CampaignSelector` aparece antes del `TenantSelector`. La jerarquía lógica del sistema es Empresa → Campaña → Lote, y la UI debe reflejarla.

**Tareas concretas:**
1. En `MainLayout.razor` (o el componente de navegación lateral correspondiente), mover el `TenantSelector` por encima del `CampaignSelector`.
2. Verificar que el orden de inicialización no genere problemas: el `TenantSelector` debe estar resuelto antes de que `CampaignSelector` cargue las campañas del tenant activo.
3. Ajustar CSS si es necesario para mantener la coherencia visual.

---

### #12 — Nomenclatura incorrecta en UI

**Severidad:** MEJORA  
**Archivos clave:** `Lotes.razor`, `LaborCatalog.razor`, `LaborDto.cs`  
**Esfuerzo estimado:** 0.5 días

**Cambios requeridos:**

| Término actual | Término correcto | Motivo |
|---|---|---|
| Catálogo de Labores | Tipo de Labores | Evita confusión con el listado de labores ejecutadas |
| Superficie Catastral | Hectáreas | Más claro para el usuario final agrícola |

**Tareas concretas:**
1. Buscar y reemplazar todas las ocurrencias de "Catálogo de Labores" → "Tipo de Labores" en archivos `.razor` y `.cs` de la capa de presentación.
2. Buscar y reemplazar "Superficie Catastral" → "Hectáreas" en todos los labels, placeholders y tooltips visibles al usuario.
3. **No renombrar** columnas de base de datos ni campos de DTOs en esta instancia (solo labels de UI) para minimizar el riesgo de regresión.
4. Si `LaborDto.cs` tiene propiedades expuestas en la UI, evaluar caso a caso.

---

### #13 — Tooltip de actividad bloqueada poco claro y selector sin feedback visual

**Severidad:** MEJORA  
**Archivos clave:** `CampaignLotEditor.razor`, CSS global  
**Esfuerzo estimado:** 0.5 días

**Descripción:**  
Cuando una actividad está bloqueada por el plan de Rotaciones, el selector no muestra ningún indicador visual de que está deshabilitado, y el tooltip no explica cómo desbloquearlo.

**Tareas concretas:**
1. Aplicar `opacity: 0.5` al elemento `<select>` cuando esté deshabilitado por rotación.
2. Ocultar el chevron (flecha) del selector en estado deshabilitado para reforzar visualmente que no es interactivo.
3. Actualizar el texto del tooltip a: *"Actividad definida en el plan de Rotaciones. Para modificarla, dirigirse a la sección Rotaciones."*
4. Cambiar la pastilla de estado de `'Sugerida'` → `'Planeada'` para reflejar con más precisión que fue definida en el plan.

---

## Criterios de aceptación del Sprint 2

- [ ] Al asignar un lote a una geometría, el mapa mantiene el zoom y posición actuales.
- [ ] Al hacer clic en un polígono del mapa, se muestra un popup con datos del lote y opción de editar.
- [ ] Los estados del dropdown de OT se cargan desde el endpoint de administración, no están hardcodeados.
- [ ] En la barra lateral, `TenantSelector` aparece sobre `CampaignSelector`.
- [ ] Las etiquetas "Catálogo de Labores" y "Superficie Catastral" fueron reemplazadas en toda la UI.
- [ ] El selector bloqueado por rotación muestra opacity reducida, sin chevron, y tooltip explicativo correcto.
- [ ] La pastilla "Sugerida" fue reemplazada por "Planeada".

---

## Notas para el agente

- Para #10, verificar que el endpoint `GET /api/workorder-statuses` esté protegido con la política de autorización correcta y retorne datos del tenant activo.
- Para #11, testear el flujo completo: login → selección de empresa → selección de campaña para asegurar que el orden de inicialización es correcto.
- Los cambios de nomenclatura (#12) deben incluir un pass final de búsqueda global en el repo para asegurar que no quedaron ocurrencias del término antiguo.
