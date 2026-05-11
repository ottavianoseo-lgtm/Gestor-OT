# Plan de Debug v2 — Gestor OT

> Documento maestro. **Leer antes de tocar cualquier archivo.**
> Rama de trabajo: `fix-bugs-revisión-gestor-ot`
> Stack: .NET 10 · Blazor WASM · EF Core · PostgreSQL/PostGIS · AntDesign Blazor · xUnit + Moq

---

## 0. Reglas no-negociables para el agente que ejecute este plan

Estas reglas aplican a **cada uno** de los `.md` por módulo. Si una regla no se puede cumplir, el agente **detiene la tarea y reporta**, no improvisa.

### 0.1. Uso obligatorio de context7 (MCP)

Antes de escribir o modificar código, el agente **debe** consultar `context7` para obtener documentación actualizada de cada librería involucrada. No se permite operar desde memoria entrenada — las APIs de .NET 10 / EF Core 10 / AntDesign Blazor cambian entre versiones menores y el costo de equivocarse es romper otra parte del sistema.

Llamadas `context7` mínimas exigidas por bug:

| Bug | Librería | Tópicos a resolver con context7 |
|---|---|---|
| 1 | `Microsoft.EntityFrameworkCore` 10.x | `HasQueryFilter`, `IgnoreQueryFilters`, scoped DbContext lifetime, header-based filters |
| 1 | `Microsoft.AspNetCore.Http` | `IHttpContextAccessor` en servicios scoped, headers personalizados |
| 2 | `Microsoft.EntityFrameworkCore` 10.x | `Contains` traducción a SQL con listas de Guid, `AsNoTracking`, `Include` |
| 3 | `Microsoft.AspNetCore.Mvc` 10.x | `[Route]` con tokens, kebab-case en routing, `AddRoutingConvention` |
| 3 | Blazor WASM | `HttpClient.GetFromJsonAsync` y manejo de 404 silencioso |
| 4 | `AntDesign.Blazor` versión declarada en csproj | `Table`, `Selection`, `SelectedRowsChanged`, comportamiento de re-render |
| 4 | CSS Flexbox | `flex-wrap`, `min-width: 0` en hijos flex |
| 5 | `Microsoft.EntityFrameworkCore` 10.x | Nullable FK + `IgnoreQueryFilters` |
| 5 | Blazor `NavigationManager` | Generación de URLs absolutas, `Request.Scheme`/`Request.Host` detrás de proxy |

El agente **debe** dejar registro en cada PR de las consultas a `context7` realizadas (commit message o descripción del PR).

### 0.2. Garantía de no-regresión

El usuario reporta cansancio por el patrón **"arreglo X y rompo Y"**. Para evitarlo:

1. **Antes de cambiar código de producción**, el agente identifica todos los puntos del sistema que **consumen** la pieza a modificar (controladores, servicios, páginas Blazor, tests). Usa `grep` recursivo en el repo, no asume.
2. **Cada bug se entrega con sus tests**:
   - Un test que **falla** con la versión actual y **pasa** con el fix (test de la causa raíz).
   - Tests de regresión sobre los caminos vecinos que la spec marca como críticos (multi-tenant, multi-campaña, separación lectura/escritura).
3. **Suite de regresión existente**: `src/GestorOT.Tests/Regression/`. El agente corre `dotnet test` antes de empezar y al terminar. **Si el delta de tests pasando es negativo, revierte.**
4. **PRs chicos por módulo** (preferencia del usuario, sección 15.3 de la spec). **Un PR por `.md`**, no un PR gigante. Orden sugerido: 03 → 01 → 04 → 02 → 05 (más simple primero, más invasivo último).
5. **Manual smoke test** después de cada PR, antes del siguiente: ver checklist al final de cada `.md`.

### 0.3. Invariantes funcionales del sistema (de la spec)

Toda modificación debe respetar:

- **Tenant aislamiento** (sección 0): TODO ocurre dentro del contexto de una Empresa (tenant).
- **Campaña como segundo tenant** (sección 0): TODO ocurre dentro de una Campaña activa para esa Empresa.
- **Campañas bloqueadas son solo lectura** (sección 1.2): ninguna mutación puede ocurrir sobre `Campaign.Status == "Locked"`.
- **Una labor no puede cambiar de campaña** (sección 5.2).
- **Planeamiento Original es inmutable** salvo admin (sección 7.1).

Estos invariantes ya están parcialmente implementados (query filters en `ApplicationDbContext`, validación de campaña bloqueada en `WorkOrdersController` y `LaborsController`). **Ningún fix puede romperlos.**

### 0.4. Convenciones de código del repo (observadas, respetar)

- Arquitectura Clean: `Domain ← Application ← Infrastructure / Api ← Client / Shared`.
- DbContext expone `IApplicationDbContext` (interface) en `Application/Interfaces/`. **No** referenciar `ApplicationDbContext` concreto desde controllers/servicios de aplicación.
- Filtro de tenant: `HasQueryFilter(e => CurrentTenantId == Guid.Empty || e.TenantId == CurrentTenantId)`. El `Guid.Empty` es escape hatch para seeds/migraciones — no inventar otros.
- Filtro de campaña: **no** está en query filter. Se aplica explícitamente en services/controllers vía `ICampaignContextService.CurrentCampaignId`. Patrón canónico en `WorkOrderQueryService` (líneas 19-32).
- Header `X-Tenant-ID` lo agrega `TenantHttpHandler`; `X-Campaign-ID`, `CampaignHttpHandler`. Ambos inyectados en `Program.cs` del cliente como pipeline encadenado.
- Migraciones EF en `src/GestorOT.Infrastructure/Data/Migrations/`. Nuevas migraciones se permiten (spec 15.1) pero **idempotentes** y con `Up`/`Down` simétricos.

---

## 1. Mapa de bugs → módulos → archivos

| # | Bug reportado | Módulo | `.md` con el plan | Archivos afectados |
|---|---|---|---|---|
| 1 | Dashboard no filtra por campaña | Dashboard | `01-dashboard-filtro-campania.md` | `DashboardController.cs`, `DashboardQueryService.cs`, `Home.razor` |
| 2 | Prohibiciones de mezcla no se aplican al guardar labor | Validación Agronómica / Labores | `02-validacion-mezcla-insumos.md` | `LaborsController.cs`, `AgronomicValidationService.cs`, `LaborEditorForm.razor` |
| 3 | Crear OT falla con "no hay estados de OT creados" | Órdenes de Trabajo | `03-creacion-ot-estados.md` | `OrdenesTrabajos.razor` (typo de URL), opcionalmente `WorkOrderStatusesController.cs` |
| 4 | UX panel de labores se rompe al seleccionar | UI / Labores | `04-ux-panel-labores.md` | `LaboresSueltas.razor` (CSS del header) |
| 5 | "Enviar para validar" genera link que no funciona | Validación Pública / Share | `05-enviar-para-validar-link.md` | `LaborsController.cs` (`SubmitForValidation`), `ShareController.cs` (`ValidateToken`), `LaborExecution.razor` |

---

## 2. Orden de ejecución recomendado

Lo más barato y de mayor impacto primero. Cada PR debe estar verde en CI antes de empezar el siguiente.

1. **PR #1 — Bug #3** (`03-creacion-ot-estados.md`): 1 línea de código en cliente. Desbloquea el flujo de creación de OT inmediatamente. Riesgo mínimo.
2. **PR #2 — Bug #4** (`04-ux-panel-labores.md`): CSS puro, sin lógica. Riesgo mínimo.
3. **PR #3 — Bug #1** (`01-dashboard-filtro-campania.md`): patrón ya probado en `WorkOrderQueryService`. Riesgo bajo.
4. **PR #4 — Bug #2** (`02-validacion-mezcla-insumos.md`): toca el pipeline de validación de labor, alto tráfico. Riesgo medio. Por eso va con tests reforzados.
5. **PR #5 — Bug #5** (`05-enviar-para-validar-link.md`): cambio de modelo (token sin OT), público sin auth. Riesgo más alto. Va último, con todos los anteriores verificados.

---

## 3. Definition of Done global (común a los 5 PRs)

Un PR no se mergea si no cumple **todos** estos puntos:

- [ ] `context7` consultado para cada librería listada en sección 0.1 del bug correspondiente; log de consultas en la descripción del PR.
- [ ] El test de causa raíz (incluido en cada `.md`) **fallaba** en `main` y **pasa** en la rama.
- [ ] `dotnet test` corre completo y **el conteo de tests pasando no disminuye**.
- [ ] Smoke test manual ejecutado según el checklist del `.md`.
- [ ] Invariantes de la sección 0.3 verificados manualmente: cambiar de tenant/campaña no muestra datos cruzados.
- [ ] Ningún `TODO`/`HACK`/`XXX` introducido sin issue asociada.
- [ ] Migraciones EF (si las hay) tienen `Up` y `Down` y fueron probadas en una base limpia.

---

## 4. Qué hacer si algo se rompe

- Si un test pasa en local pero falla en CI → no mergear, abrir issue.
- Si un fix de un bug rompe el smoke test de otro → **no** ajustar el smoke test, **revertir** y revisar el plan.
- Si aparece un sexto bug en el camino → **no** meterlo en el mismo PR. Abrir nuevo `.md` siguiendo este formato.
