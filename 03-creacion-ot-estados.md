# Bug #3 — Creación de OT falla con "no hay estados de OT creados"

> Módulo: **Órdenes de Trabajo**
> Criticidad: **Alta** (bloquea creación de cualquier OT) · **Riesgo de fix: bajísimo** (1 línea de cliente)
> Estimación: **30 min** + tests
> PR: el más chico de los cinco — recomendado **ejecutar primero** para desbloquear al usuario

---

## 1. Causa raíz

**Typo de URL en el cliente Blazor.** El endpoint del controller está en `/api/WorkOrderStatuses`, pero la página `OrdenesTrabajos.razor` lo llama como `/api/workorder-statuses` (con guion entre las palabras). En ASP.NET Core, las rutas son case-insensitive **pero los guiones no se ignoran** — `workorder-statuses` ≠ `WorkOrderStatuses`, lo que devuelve 404. El catch silencioso de la página convierte la excepción en una lista vacía y dispara el mensaje "No hay estados de OT configurados".

**Evidencia concreta:**

`src/GestorOT.Api/Controllers/WorkOrderStatusesController.cs` línea 10:
```csharp
[Route("api/[controller]")]   // → /api/WorkOrderStatuses
```

`src/GestorOT.Client/Pages/OrdenesTrabajos.razor` línea 265-267:
```csharp
_workOrderStatuses = await Http.GetFromJsonAsync<List<WorkOrderStatusDto>>(
    "api/workorder-statuses",                                  // ← typo: guion de más
    AppJsonSerializerContext.Default.ListWorkOrderStatusDto) ?? new();
```

`src/GestorOT.Client/Pages/OrdenesTrabajos.razor` línea 269-272 (catch silencioso que oculta el problema):
```csharp
catch
{
    _workOrderStatuses = new();
}
```

`src/GestorOT.Client/Pages/OrdenesTrabajos.razor` línea 160 (cartel resultante):
```razor
<Alert Type="@AlertType.Error" Message="No hay estados de OT configurados. Cree al menos un estado inicial en Administración > Estados de OT." ... />
```

**Prueba de consistencia interna**: la página de admin `src/GestorOT.Client/Pages/Admin/WorkOrderStatuses.razor` línea 108 llama correctamente a `"api/workorderstatuses"` (sin guion) y por eso **sí funciona** la pantalla donde el usuario ve sus estados creados.

## 2. Pre-lectura obligatoria con context7 (MCP)

1. `context7` — `Microsoft.AspNetCore.Mvc` v10: convención de routing con `[Route("api/[controller]")]`, comportamiento ante guiones, opciones globales (`AddRoutingConvention`, `LowercaseUrls`).
2. `context7` — Blazor WASM `HttpClient`: comportamiento de `GetFromJsonAsync` ante 404, diferencia con `GetAsync`.
3. `context7` — `System.Text.Json` source generator: chequear si `AppJsonSerializerContext` requiere algún ajuste cuando la ruta cambia (debería ser irrelevante, pero confirmar).

## 3. Plan de implementación

### 3.1. Fix mínimo: corregir la URL en el cliente

**Archivo:** `src/GestorOT.Client/Pages/OrdenesTrabajos.razor`, línea 266.

Cambiar:
```csharp
"api/workorder-statuses",
```
por:
```csharp
"api/workorderstatuses",
```

Esto alinea con `Admin/WorkOrderStatuses.razor` línea 108 y con la ruta efectiva del controller. Es **una línea**, y se confirma como suficiente para que `_workOrderStatuses` se pueble correctamente y el `else if (_workOrderStatuses.Count > 0)` de la línea 149 entre.

### 3.2. Mejora defensiva — eliminar el catch silencioso

El catch de la línea 269-272 oculta cualquier 404, 500, timeout o error de red. Si en el futuro hay otro typo o el backend baja, el usuario verá "No hay estados" en lugar del error real.

Cambiar:
```csharp
catch
{
    _workOrderStatuses = new();
}
```
por:
```csharp
catch (HttpRequestException ex)
{
    _workOrderStatuses = new();
    Message.Error($"No se pudieron cargar los estados de OT: {ex.Message}");
}
catch (Exception ex)
{
    _workOrderStatuses = new();
    Message.Error($"Error inesperado al cargar estados de OT: {ex.Message}");
}
```

Esto **no cambia** la UX cuando todo funciona; solo hace visible el problema cuando algo se rompe. Coherente con el patrón de `Home.razor` líneas 606-618 (que usa `Message.Error`).

### 3.3. (Opcional, **fuera de este PR**) — auditar URLs hardcodeadas

Auditoría sugerida para issue futura: `grep -rn "api/" src/GestorOT.Client/` y validar que cada URL del cliente apunte a un controller existente. Hoy hay ~50 llamadas hardcodeadas y este typo seguramente no es el único. **No incluir en este PR**: scope creep.

### 3.4. ¿Hay que cambiar el controller o agregar alias?

**No.** Tentación a evitar: agregar `[Route("api/workorder-statuses")]` como segunda ruta al controller para que ambas URLs funcionen. Esto fragmenta el contrato (¿cuál es la URL "oficial"?) y duplica superficie. Mejor: una URL canónica, consumidores alineados.

## 4. Tests

**Archivo nuevo:** `src/GestorOT.Tests/Regression/WorkOrderStatusesEndpointTests.cs`

Casos:

1. **`WorkOrderStatusesController_Route_IsWorkOrderStatuses`** — test de smoke usando `WebApplicationFactory` (o validando la metadata del controller) que verifique que la ruta efectiva sea `/api/WorkOrderStatuses` y no `/api/workorder-statuses`. Esto previene que alguien cambie el controller en el futuro y rompa de nuevo este cliente.
2. **Test de integración del cliente**: si hay un patrón de tests para Blazor en el repo (no lo hay actualmente — confirmar), agregar uno que asegure que `OrdenesTrabajos.razor` llama a la URL correcta. **Si no hay infraestructura de tests Blazor, omitir y compensar con el test del paso 1 + smoke manual.** No introducir bUnit en este PR para mantenerlo chico.
3. **Test existente `WorkOrderStatusTests.cs`** debe seguir verde.

Verificación adicional sin tests automatizados:
```bash
grep -rn '"api/' src/GestorOT.Client/ | grep -i "workorder.status\|work.order.status"
```
Confirmar que **solo** aparece `api/workorderstatuses` y no `api/workorder-statuses` ni `api/work-order-statuses`.

## 5. Smoke test manual

1. Ir a `Admin > Estados de OT`. Verificar que aparecen los estados ya creados (debería funcionar pre-fix).
2. Sin crear estados nuevos, ir a `Órdenes de Trabajo > Nueva Orden`.
3. En el modal, el campo "Estado" **debe** mostrar el select de estados (no el Alert rojo).
4. Crear la OT con el estado por defecto → se crea sin errores.
5. **Caso destructivo**: borrar todos los estados desde admin (si el backend lo permite). Volver a "Nueva Orden". El Alert rojo **sí** debe aparecer ahora — confirma que el mensaje es válido para su caso real (no hay estados creados).
6. Recrear al menos un estado por defecto. Nueva Orden vuelve a funcionar.
7. **Regresión Admin**: `Admin > Estados de OT` sigue funcionando idéntico.

## 6. Definition of Done específica

- [ ] Línea 266 de `OrdenesTrabajos.razor` corregida.
- [ ] Catch silencioso reemplazado por catch con mensaje visible al usuario.
- [ ] Test de ruta en `WorkOrderStatusesEndpointTests.cs` agregado y verde.
- [ ] Smoke test manual de los 7 pasos completado.
- [ ] Grep del repo confirma que no quedan otras llamadas a `api/workorder-statuses` con guion.
- [ ] `dotnet test` total sin regresiones.
- [ ] PR description lista las consultas a `context7`.

## 7. Lo que NO se cambia en este PR

- El `WorkOrderStatusesController` (la ruta sigue siendo `[Route("api/[controller]")]`).
- La entidad `WorkOrderStatus` ni su configuración EF.
- La página `Admin/WorkOrderStatuses.razor` (ya funcionaba).
- El resto de las URLs hardcodeadas del cliente (auditoría queda para issue separada).
- Cualquier cambio en el flujo de creación de OT más allá de poder seleccionar el estado.

---

## Nota lateral relevante

La hipótesis inicial al ver el bug fue que el filtro multi-tenant ocultaba estados con `TenantId` diferente. Esa hipótesis se descartó al ver que la página de Admin (que usa el mismo HttpClient con el mismo `TenantHttpHandler`) sí ve los estados — confirmando que el header `X-Tenant-ID` viaja y el query filter funciona. **El bug es 100% un typo de URL en el cliente.** Si el agente que ejecuta el plan llegara a observar comportamiento distinto (ej. con el typo corregido aún no aparecen estados), revisar entonces el `TenantId` de los registros en BD — pero **solo en ese caso**, no preventivamente.
