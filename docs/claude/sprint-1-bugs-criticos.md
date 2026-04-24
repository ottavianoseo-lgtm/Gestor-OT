# Sprint 1 — Bugs Críticos de Datos y Validaciones
**GestorMax · Gestor OT** | Semanas 1–2 | Prioridad: MÁXIMA

---

## Objetivo

Eliminar todos los bugs que causan pérdida o corrupción de datos antes de cualquier release a producción. Este sprint es bloqueante: ningún otro sprint debe llegar a producción si estos issues están abiertos.

---

## Issues a resolver

### #1 · #2 · #3 — Campaña desaparece / Nombre de Lote sobreescrito / Superficie sobreescrita por GIS

**Severidad:** CRÍTICO  
**Archivos clave:** `CampaignProvider.razor`, `Lotes.razor`, `CampaignState.cs`  
**Esfuerzo estimado:** 3 días

**Causa raíz:**  
`CampaignProvider.razor` usa `[PersistentState]` para serializar el estado de la campaña activa. Si `CampaignState.OnChange` dispara una re-inicialización del componente `Lotes` durante una operación de guardado, el `_formModel` en vuelo captura valores del estado persistido (nombre de campaña, Id de campaña) en lugar de los valores ingresados por el usuario.

Adicionalmente, el evento `OnChange` está suscripto pero el componente **no está protegido** con `StateHasChanged` en el hilo correcto en operaciones asíncronas. Esto puede hacer que la UI rebind con un modelo incorrecto.

En `SaveLot()` (Lotes.razor ~línea 720), el `LotDto` se construye **antes** de que la respuesta HTTP retorne. Si durante el `await Http.PostAsJsonAsync` el `CampaignState` cambia (refresh de token, navegación del usuario), el estado puede ser stale al procesar la respuesta, y `LoadData()` puede reasignar la campaña activa incorrecta.

**Tareas concretas:**
1. Auditar el ciclo de vida de `CampaignProvider` durante operaciones asíncronas.
2. Identificar todos los puntos donde `_formModel` puede ser sobreescrito por datos de `CampaignState`.
3. Agregar guards que capturen el estado del formulario en variables locales **antes** del `await` y usen esas variables locales para construir el DTO (no el estado del componente, que puede haber mutado).
4. Evaluar si `CampaignState.OnChange` debe desuscribirse durante una operación de guardado en vuelo y re-suscribirse al finalizar.
5. Asegurar que todas las llamadas a `StateHasChanged` post-await se hagan en el contexto correcto (`InvokeAsync`).

---

### #4 — Área Catastral no persiste al crear un Lote (queda en 0)

**Severidad:** CRÍTICO  
**Archivos clave:** `Lotes.razor → SaveLot()`, `LotDto.cs`, `LotsController.cs`  
**Esfuerzo estimado:** 1 día

**Causa raíz confirmada:**  
El constructor de `LotDto` recibe parámetros posicionales. En `SaveLot()` (~línea 718–722) solo se pasan 5 argumentos. `CadastralArea` es el 8° argumento del record y queda en su valor default `0`. El valor que el usuario ingresó en `_formModel.CadastralArea` **nunca se incluye** en el DTO enviado al API.

**Tareas concretas:**
1. Agregar `_formModel.CadastralArea` como argumento al construir el `LotDto` en `SaveLot()`.
2. Verificar que el endpoint `PUT /api/lots/{id}` en `LotsController.cs` mapee correctamente `CadastralArea` a la entidad de dominio.
3. Revisar si existe el mismo problema en el flujo de actualización (`EditLot()`).
4. **Tarea extra preventiva:** auditar todos los constructores de DTOs usados en operaciones POST/PUT para detectar campos silenciosamente omitidos (mismo patrón).

---

### #5 — Botones GIS se superponen al panel 'Polígono Dibujado'

**Severidad:** ALTO  
**Archivos clave:** `Mapa.razor` (estilos `.gis-toolbar`, `.orphan-panel`)  
**Esfuerzo estimado:** 0.5 días

**Causa raíz:**  
El `.gis-toolbar` tiene `z-index: 999` y el panel de polígono dibujado tiene `z-index: 1000`. El toolbar se renderiza como posición absoluta a la derecha del mapa, quedando físicamente sobre el panel de confirmación.

**Tareas concretas:**
1. Bindear dinámicamente el `z-index` del toolbar: cuando `_drawnWkt != null` (hay polígono activo), bajar el toolbar a `z-index: 990`.
2. Cuando `_drawnWkt == null`, restaurar el toolbar a `z-index: 1010` (o el valor original).
3. Validar el comportamiento en mobile / viewport reducido.

---

### #6 — Lote Huérfano no se precarga al navegar desde lote sin geometría

**Severidad:** ALTO  
**Archivos clave:** `Lotes.razor`, `Mapa.razor → OnPolygonDrawn()`  
**Esfuerzo estimado:** 1 día

**Causa raíz:**  
El botón 'Sin geometría (Configurar)' navega a `/mapa` sin parámetros. `Mapa.razor` no recibe contexto del lote de origen y no puede preseleccionarlo.

**Tareas concretas:**
1. Cambiar la navegación a `/mapa?lotId={id}` pasando el Id del lote de origen.
2. En `OnInitializedAsync` de `Mapa.razor`, leer el query param `lotId`.
3. Si `lotId` está presente, asignar su valor a `_linkingLotId` para que el campo 'Seleccionar lote huérfano' quede preseleccionado automáticamente.
4. Verificar que el flujo de guardado del polígono funcione correctamente con el preseleccionado.

---

### #9 — Rotación con fechas invertidas (Desde > Hasta)

**Severidad:** ALTO  
**Archivos clave:** `RotationService.cs`, `CampaignLotEditor.razor`  
**Esfuerzo estimado:** 0.5 días

**Causa raíz:**  
`RotationService.CreateRotationAsync` y `UpdateRotationAsync` no validan que `dto.StartDate < dto.EndDate` antes de persistir. Es posible guardar rotaciones inválidas.

**Tareas concretas:**
1. Agregar validación `StartDate < EndDate` en `CreateRotationAsync` y `UpdateRotationAsync`.
2. Retornar un error estructurado con mensaje descriptivo si la validación falla.
3. En `CampaignLotEditor.razor`, agregar un `Alert` visible con el texto: *"La fecha de inicio debe ser anterior a la fecha de fin"* al detectar la inversión antes de llamar al API.
4. Bloquear el botón de guardar mientras las fechas estén invertidas.

---

## Criterios de aceptación del Sprint 1

- [ ] Crear un lote con nombre y área catastral → ambos valores persisten correctamente.
- [ ] La campaña activa no cambia luego de guardar un lote.
- [ ] El nombre del lote no es sobreescrito por el nombre de la campaña.
- [ ] La superficie catastral no es sobreescrita por el área GIS calculada.
- [ ] Los botones GIS no se superponen al panel 'Polígono Dibujado'.
- [ ] Al navegar a /mapa desde un lote sin geometría, ese lote queda preseleccionado.
- [ ] No es posible guardar una rotación con `StartDate >= EndDate`.
- [ ] El área catastral ingresada en el formulario se almacena correctamente en la base de datos.

---

## Notas para el agente

- **No hacer releases parciales** de este sprint. Los 4 bugs críticos (#1-4) deben estar resueltos y testeados juntos antes de mergear a producción.
- Al corregir #1-3, tener especial cuidado de no romper el flujo de persistencia de `[PersistentState]` para casos legítimos (recarga de página).
- Registrar en el PR los pasos exactos para reproducir cada bug antes del fix.
