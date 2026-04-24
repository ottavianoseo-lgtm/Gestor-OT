# Sprint 5 — Detalles de UX/UI y Consistencia del Dashboard
**GestorMax · Gestor OT** | Abril 2026 | Prioridad: ALTA

---

## Objetivo

Refinar la experiencia de usuario (UX) y la interfaz visual (UI) para asegurar una consistencia profesional en toda la plataforma, eliminando elementos residuales y corrigiendo comportamientos de filtrado en el dashboard.

---

## Issues a resolver

### #15 — "X" residual en Tabs de Detalle de OT
**Severidad:** BAJO (Visual)  
**Archivo clave:** `WorkOrderDetail.razor`  
**Causa raíz:**  
El componente `Tabs` está configurado como `Type="@TabType.EditableCard"`, lo que por defecto habilita botones de cierre ("x") en cada tab. Dado que estos tabs son estáticos (KPIs, Labores, Insumos), no deben ser editables.

**Tareas concretas:**
1. Cambiar el `Type` del componente `Tabs` de `TabType.EditableCard` a `TabType.Card` o `TabType.Line` para eliminar los botones de cierre.
2. Validar que el estilo visual se mantenga coherente con el diseño "glassmorphism" del resto de la página.

---

### #16 — Contraste de estados de OT
**Severidad:** MEDIO (Accesibilidad)  
**Archivos clave:** `WorkOrderDetail.razor`, `WorkOrders.razor`, `WorkOrderStatuses.razor`  
**Causa raíz:**  
Los colores de los estados de la Orden de Trabajo (Badge/Tag) no están validados contra el fondo. Hay textos negros sobre fondos oscuros o colores muy claros sobre fondos claros, lo que dificulta la lectura.

**Tareas concretas:**
1. Implementar una función de utilidad (o ajustar los estilos inline) para asegurar que el texto de los estados siempre tenga un contraste adecuado (WCAG).
2. En `WorkOrderDetail.razor` (~línea 162), ajustar el `Tag` para que use colores legibles sobre el fondo oscuro del dashboard.
3. Asegurar que en el selector de estados (~línea 59) el `Badge` sea claramente visible.

---

### #17 — Refresco de Dashboard al cambiar de Campaña
**Severidad:** ALTO (Funcional)  
**Archivos clave:** `Home.razor`, `DashboardState.cs`  
**Causa raíz:**  
El Dashboard utiliza un servicio de estado (`DashboardState`) para cachear los datos. Al cambiar la campaña desde el selector global, aunque se dispara el evento `OnCampaignChanged`, la lógica de carga en `Home.razor` a veces confía en los datos cacheados en lugar de forzar una nueva petición al API con el filtro de la nueva campaña.

**Tareas concretas:**
1. En `Home.razor`, asegurar que `OnCampaignChanged` llame a `DashboardState.Clear()` antes de ejecutar `LoadData()`.
2. Verificar que los endpoints `api/dashboard/stats` y `api/dashboard/recent-orders` estén recibiendo y respetando el `CampaignId` actual del `CampaignState`.
3. Eliminar cualquier cortocircuito que use datos cacheados si se detecta que la campaña actual no coincide con la de los datos guardados.

---

### #18 — Eliminación de referencias a Supabase
**Severidad:** BAJO (Consistencia)  
**Archivo clave:** `Home.razor`  
**Causa raíz:**  
El sistema ya no utiliza Supabase como infraestructura principal, pero aún persisten etiquetas visuales en el dashboard que lo mencionan.

**Tareas concretas:**
1. Localizar la sección "Estado del Sistema" en `Home.razor` (~línea 73).
2. Reemplazar la etiqueta "Supabase" por "Servidor API" o "Conexión de Datos".
3. Validar que el indicador de estado (`_isConnected`) refleje correctamente la conexión con nuestra API/Postgres local.

---

### #19 — Eliminación de Errores Silenciosos
**Severidad:** ALTO (UX)  
**Archivos clave:** Todos los componentes `.razor`, `HttpClient` interceptors.  
**Causa raíz:**  
Varios bloques `try-catch` en los componentes capturan excepciones pero solo las imprimen en consola o muestran mensajes genéricos que no informan adecuadamente al usuario si la operación falló en el servidor. El usuario debe saber siempre si un POST, PUT, GET o DELETE no se completó.

**Tareas concretas:**
1. Auditar todos los componentes `.razor` (especialmente `WorkOrderDetail`, `Home`, `Lotes`) en busca de bloques `catch` vacíos o con logging insuficiente.
2. Implementar notificaciones visuales (usando `IMessageService` de Ant Design) en cada falla de API que no esté ya cubierta.
3. Asegurar que los errores 4xx y 5xx del servidor muestren un mensaje descriptivo (ej: "Error al guardar cambios", "No se pudo conectar con el servidor").
4. **Opcional pero recomendado:** Evaluar la implementación de un Interceptor global de HttpClient que dispare un `Message.Error` automáticamente ante cualquier status code != 2xx.

---

## Criterios de aceptación del Sprint 5

- [ ] Los tabs de "Resumen", "Labores" e "Insumos" en el detalle de OT ya no muestran una "x" de cierre.
- [ ] Todos los tags de estado de OT son legibles (contraste adecuado) tanto en listas como en detalles.
- [ ] Al cambiar de campaña en el selector lateral, los números y el mapa del Dashboard se actualizan instantáneamente con los datos de la nueva campaña.
- [ ] No existe ninguna mención a "Supabase" en la interfaz del Dashboard.
- [ ] El mapa del dashboard se limpia y recarga con los lotes correspondientes a la campaña seleccionada.
- [ ] **Ninguna operación de API falla silenciosamente**: Ante cualquier error de red o de servidor, el usuario recibe una notificación visual (Toast/Message) descriptiva.

---

## Notas para el agente

- El diseño debe seguir manteniendo la estética **premium/dark** con efectos de desenfoque (glassmorphism).
- Al corregir el refresco del dashboard, ten cuidado con las condiciones de carrera (race conditions) si el usuario cambia de campaña muy rápido. Usa `CancellationToken` si es necesario (ya implementado parcialmente en `LoadData`).
- Para el problema de contraste, prioriza usar variables CSS del tema si están disponibles o colores con alta luminancia para temas oscuros.
