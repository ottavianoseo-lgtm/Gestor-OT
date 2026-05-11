# Bug #1 — Dashboard principal no filtra por campaña

> Módulo: **Dashboard**
> Criticidad: **Alta** (rompe el invariante de tenant secundario "Campaña" de la spec, sección 0)
> Estimación: **2-3h** incluyendo tests
> PR: chico, aislado a 3 archivos

---

## 1. Causa raíz

El dashboard del Home muestra estadísticas y órdenes recientes **sin filtrar por la campaña actual**. El frontend está bien preparado (se suscribe a `CampaignState.OnChange` y vuelve a pedir los datos cuando cambia la campaña, ver `Home.razor` líneas 555 y 665-676), pero el backend ignora el header `X-Campaign-ID`.

**Evidencia concreta:**

`src/GestorOT.Infrastructure/Services/DashboardQueryService.cs`:

- `GetStatsAsync` (líneas 17-53): consulta `_context.Fields`, `_context.CampaignLots`, `_context.Lots`, `_context.WorkOrders` **sin ningún `.Where(... CampaignId == ...)`**.
- `GetRecentOrdersAsync` (líneas 55-71): consulta `_context.WorkOrders` **sin filtro por campaña**.
- El servicio **ni siquiera inyecta** `ICampaignContextService`.

Patrón correcto ya implementado en el mismo repo: `src/GestorOT.Infrastructure/Services/WorkOrderQueryService.cs` líneas 11-32.

## 2. Pre-lectura obligatoria con context7 (MCP)

Antes de tocar código, el agente debe consultar:

1. `context7` — `Microsoft.EntityFrameworkCore` v10: traducción de `Where` condicional sobre nullable Guid, costo de `GroupBy` en consultas filtradas, comportamiento de `SumAsync` cuando el filtro deja 0 filas.
2. `context7` — `Microsoft.AspNetCore.Http`: lifetime de `IHttpContextAccessor` cuando se inyecta en un servicio scoped que se invoca desde un controller.
3. `context7` — Blazor WASM: `EventCallback` / eventos custom (`CampaignState.OnChange`) — confirmar que el patrón actual de `Home.razor` no necesita ajuste.

Dejar registro de los temas consultados en la descripción del PR.

## 3. Plan de implementación

### 3.1. `DashboardQueryService` — inyectar contexto y filtrar

**Archivo:** `src/GestorOT.Infrastructure/Services/DashboardQueryService.cs`

Cambios:

- Inyectar `ICampaignContextService` (ya está registrado en DI vía `ServiceExtensions.cs`).
- En cada método, leer `_campaignContext.CurrentCampaignId` y aplicar `.Where(... CampaignId == campaignId)` **solo si** `campaignId.HasValue && campaignId != Guid.Empty`. Mismo patrón que `WorkOrderQueryService` líneas 21 y 29-32.

Detalles por contador:

| Métrica | Cómo filtrar |
|---|---|
| `fieldsCount` | Vía `CampaignFields`: `_context.CampaignFields.Where(cf => cf.CampaignId == campaignId).Select(cf => cf.FieldId).Distinct().CountAsync()`. Si no hay campaña seleccionada, mantener `_context.Fields.CountAsync()`. |
| `totalProductiveArea` | `_context.CampaignLots.Where(cl => cl.CampaignId == campaignId).SumAsync(cl => cl.ProductiveArea)`. |
| `lotStats` (total + active) | Vía `CampaignLots`: contar `Lot`s referenciados desde `CampaignLot` de esa campaña. Active = `Lot.Status == "Active"`. Usar `.Select(cl => cl.Lot).Distinct()` o subquery. |
| `workOrderCounts` | `_context.WorkOrders.Where(w => w.CampaignId == campaignId).GroupBy(w => w.Status)`. |
| `GetRecentOrdersAsync` | `_context.WorkOrders.Where(w => w.CampaignId == campaignId)...`. |

**No** modificar el contrato `IDashboardQueryService` ni `DashboardStatsDto`. **No** agregar parámetros al controller (el header `X-Campaign-ID` se resuelve vía DI dentro del servicio — coherente con cómo lo hace `WorkOrderQueryService`).

### 3.2. `DashboardController` — sin cambios

`src/GestorOT.Api/Controllers/DashboardController.cs` no se toca. El controller seguirá llamando `_queryService.GetStatsAsync(ct)` y `_queryService.GetRecentOrdersAsync(10, ct)` — la inyección de `ICampaignContextService` ocurre dentro del servicio.

### 3.3. `Home.razor` — verificar (no modificar salvo bug colateral)

`src/GestorOT.Client/Pages/Home.razor`:
- Línea 555: `CampaignState.OnChange += OnCampaignChanged` ✅
- Líneas 665-676: `OnCampaignChanged` limpia cache y vuelve a llamar `LoadData()` ✅
- Líneas 588-595: las 3 llamadas (`stats`, `recent-orders`, `lots/geojson`) usan el mismo `HttpClient` que pasa por `CampaignHttpHandler`. ✅

**No tocar el frontend** salvo que aparezca un caso donde el `Clear()` del `DashboardState` deje un estado intermedio visible al usuario. Si pasa, agregar un `_isFetching = true` antes de `_stats = new()` para que se vea el spinner.

### 3.4. Riesgo colateral: `api/lots/geojson`

`Home.razor` línea 592 llama a `api/lots/geojson` para pintar el mapa del fondo. **Ese endpoint también debería estar filtrado por campaña**, pero es **fuera del scope de este bug** (el reporte del usuario habla de "datos del dashboard", no del mapa). Anotar en la descripción del PR como issue futura: *"Verificar que `LotsController.GetGeoJson` filtre por campaña vía `CampaignLot` para coherencia visual"*. **No incluir en este PR** para mantenerlo chico.

## 4. Tests

**Archivo nuevo:** `src/GestorOT.Tests/Regression/DashboardCampaignFilterTests.cs`

Casos mínimos a cubrir:

1. **`GetStatsAsync_NoCampaignHeader_ReturnsAllData`** — sin `X-Campaign-ID`, el servicio devuelve totales globales del tenant (comportamiento legacy preservado, para que el dashboard no se quede vacío si por alguna razón el header no viajó).
2. **`GetStatsAsync_WithCampaignHeader_FiltersWorkOrders`** — con campañas A y B en el mismo tenant, con OTs de cada una, al pasar `CampaignId = A` solo se cuentan las OTs de A.
3. **`GetStatsAsync_WithCampaignHeader_FiltersProductiveArea`** — `CampaignLots` de campaña A con `ProductiveArea = 100`, B con 200 → con `CampaignId = A` retorna 100.
4. **`GetRecentOrdersAsync_WithCampaignHeader_OnlyReturnsCampaignOrders`** — con 3 OTs en A y 3 en B, pasar `CampaignId = A` devuelve solo las 3 de A.
5. **`GetStatsAsync_EmptyCampaignGuid_TreatedAsNoFilter`** — pasar `Guid.Empty` se comporta igual que no pasar nada (no devolver 0 silencioso).
6. **Regresión tenant — `GetStatsAsync_DoesNotLeakAcrossTenants`** — dos tenants con datos, el filtro de campaña no rompe el filtro de tenant (combinarlo con `CurrentTenantId` del query filter ya existente).

Patrón de mock: mirar `src/GestorOT.Tests/Regression/RotationServiceTests.cs` y `WorkOrderStatusTests.cs` para el setup de `IApplicationDbContext` con InMemory provider o mocks. Reutilizar `AsyncQueryHelpers.cs` que ya está en `Tests/Helpers/`.

## 5. Smoke test manual

Antes de mergear, ejecutar **en este orden** sobre una base limpia con dos campañas (A y B) con datos distintos:

1. Login → seleccionar Tenant T1.
2. Seleccionar Campaña A → ir a Home. Anotar: lotes, hectáreas, OTs abiertas.
3. Seleccionar Campaña B → ir a Home. Los valores **deben cambiar**.
4. Volver a Campaña A → los valores originales reaparecen.
5. Crear una OT en Campaña B → Home en B muestra +1 OT pendiente; Home en A **sin cambios**.
6. Cambiar a Tenant T2 → los valores deben ser de T2, no de T1 (verifica que el filtro de campaña no rompió el de tenant).

## 6. Definition of Done específica

- [ ] `DashboardQueryService` inyecta `ICampaignContextService` y filtra las 5 métricas + `GetRecentOrdersAsync`.
- [ ] 6 tests nuevos en `DashboardCampaignFilterTests.cs`, todos en verde.
- [ ] `dotnet test` total **no disminuye** vs main.
- [ ] Smoke test manual de los 6 pasos completado.
- [ ] Issue futura abierta para `api/lots/geojson` (no incluir el fix en este PR).
- [ ] `Home.razor` **no modificado** (o si se modificó, justificado en la descripción del PR).
- [ ] PR description lista las consultas a `context7` realizadas.

## 7. Lo que NO se cambia en este PR

- El frontend (`Home.razor`, `DashboardState.cs`).
- El contrato público `IDashboardQueryService`.
- Los DTOs `DashboardStatsDto` y `RecentWorkOrderDto`.
- El endpoint del controller.
- El filtro de `api/lots/geojson` (issue separada).
- Cualquier otro `*QueryService` del repo (aunque tengan el mismo bug latente, no entran acá).
