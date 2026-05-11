# Bug #5 — "Enviar para validar" genera un link que no funciona

> Módulo: **Validación Pública / Share / Labores**
> Criticidad: **Alta** (rompe el flujo de validación de labores con terceros — contratistas, operarios)
> Estimación: **5-8h** incluyendo tests, es el PR más invasivo
> PR: mediano-grande, **dejar para el final**

---

## 1. Causa raíz

Hay un desalineamiento entre lo que el endpoint `SubmitForValidation` **crea** (token para una labor) y lo que el endpoint `ValidateToken` **espera** (token asociado a una OT). Cuando el usuario presiona "enviar para validar" sobre una **labor suelta** (sin OT), el sistema genera un `SharedToken` con `WorkOrderId = null` y un link bien formado, pero al abrir el link el endpoint público no encuentra la OT (porque no hay) y devuelve 404, así que el front muestra "El enlace no es válido o ha expirado."

**Evidencia concreta:**

### 1.1. Generación del token (labor sin OT)

`src/GestorOT.Api/Controllers/LaborsController.cs` líneas 520-565, método `SubmitForValidation`:

- Línea 548: `WorkOrderId = (labor.WorkOrderId == null || labor.WorkOrderId == Guid.Empty) ? null : labor.WorkOrderId`
  → si la labor es suelta, el token se persiste con `WorkOrderId = null`.
- Línea 555: `Metadata = JsonSerializer.Serialize(new { laborId = labor.Id, action = "validate" })`
  → el `laborId` queda en metadata, no en una columna.
- Línea 562: `var publicUrl = $"{baseUrl}/public/labor-execution/{rawToken}";`
  → la URL apunta a `LaborExecution.razor` (ruta `/public/labor-execution/{Token}`), que llama internamente a `api/share/validate/{Token}` (ver `LaborExecution.razor` línea 406).

### 1.2. Validación del token (espera siempre una OT)

`src/GestorOT.Api/Controllers/ShareController.cs` método `ValidateToken` líneas 61-151:

- Línea 91: `.FirstOrDefaultAsync(w => w.Id == sharedToken.WorkOrderId);`
  → si `WorkOrderId` es `null`, esta query devuelve `null`.
- Línea 93-94: `if (wo == null) return NotFound("Orden de trabajo no encontrada.");`
  → 404 directo. **Acá muere el flujo de labor suelta.**

### 1.3. Lectura del frontend público

`src/GestorOT.Client/Pages/LaborExecution.razor`:

- Línea 1: `@page "/execution/{WorkOrderId:guid}"` (uso interno autenticado).
- Línea 2: `@page "/public/labor-execution/{Token}"` (uso público con token).
- Líneas 404-455: cuando es público, hace `GET api/share/validate/{Token}`, espera un `PublicWorkOrderDto`.
- Línea 451-453: si recibe error, mapea texto a tres mensajes posibles ("expirado", "revocado", "no válido").

### 1.4. Realización pública (otro punto donde falla)

`src/GestorOT.Api/Controllers/ShareController.cs` líneas 170-176, dentro de `RealizeLaborPublic`:

```csharp
var labor = await _context.Labors
    .IgnoreQueryFilters()
    .Include(l => l.Supplies)
    .FirstOrDefaultAsync(l => l.Id == laborId && l.WorkOrder!.Id == sharedToken.WorkOrderId);
```

→ exige que la labor pertenezca a una OT que coincida con el token. Para labores sueltas, falla.

## 2. Pre-lectura obligatoria con context7 (MCP)

1. `context7` — `Microsoft.EntityFrameworkCore` v10: `IgnoreQueryFilters`, FK nullables, `OnDelete` behavior cuando una FK pasa a nullable, generación de migraciones (no se necesita migración nueva — la columna `SharedToken.WorkOrderId` ya es nullable, ver `SharedToken.cs`).
2. `context7` — `Microsoft.AspNetCore.Mvc`: `[IgnoreAntiforgeryToken]`, mejores prácticas para endpoints públicos con token corto (rate limiting básico, prevención de enumeración).
3. `context7` — Blazor `NavigationManager`: generación de URLs absolutas detrás de proxy reverso (Nginx/Cloudflare) — confirmar que `Request.Scheme` + `Request.Host` produce el host público y no el interno. Si el deploy tiene proxy, puede que se necesite `app.UseForwardedHeaders` en `Program.cs`.
4. `context7` — `System.Security.Cryptography`: confirmar que `Convert.ToHexStringLower` está disponible en .NET 10 (ya se usa en el código, OK) y comparar con la convención del repo.

## 3. Plan de implementación

El fix tiene dos partes que **deben ir juntas en el mismo PR** porque son simétricas (crear / consumir).

### 3.1. Verificar primero la URL generada — saneamiento del link "raro"

El usuario reporta que el link "es algo raro". Antes de cambiar lógica, verificar si hay un bug de URL building:

`LaborsController.SubmitForValidation` línea 561-562:
```csharp
var baseUrl = $"{Request.Scheme}://{Request.Host}";
var publicUrl = $"{baseUrl}/public/labor-execution/{rawToken}";
```

Si la API está expuesta detrás de un proxy reverso (Nginx, Cloudflare), `Request.Scheme` puede devolver `http` aunque el cliente vea `https`, y `Request.Host` puede devolver el host interno (`localhost:5000`, `api.internal`) en lugar del público. Esto puede ser el motivo de "link raro".

**Acción**:
1. Confirmar el deploy con el usuario: ¿hay proxy reverso?
2. Si sí: agregar `app.UseForwardedHeaders(new ForwardedHeadersOptions { ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost })` en `Program.cs` **antes** de `app.UseRouting()` (línea 55). Configurar `KnownNetworks` o `KnownProxies` para producción.
3. Si no hay proxy: la URL ya está bien construida, el problema es solo el lado lógico (sección 3.2 en adelante).

**Sugerencia adicional**: agregar configuración opcional `PublicBaseUrl` en `appsettings.json` para forzar el host público (más explícito que confiar en headers):

```json
"App": { "PublicBaseUrl": "https://gestorot.midominio.com" }
```

Y usarla en `SubmitForValidation` con fallback a `Request.Scheme + Host`. Esto es el plan recomendado: más simple que `UseForwardedHeaders` para un deploy on-prem como el descrito en la spec (sección 16.1: "Servidor Local").

### 3.2. Hacer que `ValidateToken` soporte tokens sin OT (labor suelta)

**Archivo:** `src/GestorOT.Api/Controllers/ShareController.cs`

Reemplazar las líneas 80-94 (`ValidateToken`) por:

```csharp
WorkOrder? wo = null;
List<Labor> labors;

// Parse metadata first to know if it's a single-labor token
Guid? singleLaborId = null;
HashSet<Guid> allowedIds = new();
if (!string.IsNullOrEmpty(sharedToken.Metadata))
{
    try
    {
        var meta = JsonSerializer.Deserialize<JsonElement>(sharedToken.Metadata);
        if (meta.TryGetProperty("laborIds", out var idsProp))
        {
            foreach (var id in idsProp.EnumerateArray()) allowedIds.Add(id.GetGuid());
        }
        else if (meta.TryGetProperty("laborId", out var idProp))
        {
            singleLaborId = idProp.GetGuid();
            allowedIds.Add(singleLaborId.Value);
        }
    }
    catch { /* malformed metadata: tratar como ausencia de allow-list */ }
}

if (sharedToken.WorkOrderId.HasValue && sharedToken.WorkOrderId != Guid.Empty)
{
    wo = await _context.WorkOrders
        .AsNoTracking()
        .IgnoreQueryFilters()
        .Include(w => w.Field)
        .Include(w => w.Labors).ThenInclude(l => l.Type)
        .Include(w => w.Labors).ThenInclude(l => l.Lot)
        .Include(w => w.Labors).ThenInclude(l => l.Supplies).ThenInclude(s => s.Supply)
        .FirstOrDefaultAsync(w => w.Id == sharedToken.WorkOrderId);

    if (wo == null) return NotFound("Orden de trabajo no encontrada.");

    labors = allowedIds.Any()
        ? wo.Labors.Where(l => allowedIds.Contains(l.Id)).ToList()
        : wo.Labors.ToList();
}
else if (singleLaborId.HasValue)
{
    // Standalone labor — sin OT
    var laborQuery = await _context.Labors
        .AsNoTracking()
        .IgnoreQueryFilters()
        .Include(l => l.Type)
        .Include(l => l.Lot).ThenInclude(lot => lot!.Field)
        .Include(l => l.Supplies).ThenInclude(s => s.Supply)
        .FirstOrDefaultAsync(l => l.Id == singleLaborId.Value);

    if (laborQuery == null) return NotFound("Labor no encontrada.");

    labors = new List<Labor> { laborQuery };
}
else
{
    return BadRequest("Token sin destino válido.");
}

// Construir DTO público
var publicLabors = labors.OrderBy(l => l.CreatedAt).Select(l => new PublicLaborDto(
    l.Id, l.Type?.Name ?? "Labor", l.Status.ToString(), l.Hectares,
    l.LotId, l.Lot?.Name,
    l.Supplies.Select(s => new PublicLaborSupplyDto(
        s.Id, s.SupplyId, s.Supply?.ItemName ?? "Insumo",
        s.PlannedDose, s.RealDose, s.PlannedTotal, s.RealTotal,
        s.UnitOfMeasure, s.Supply?.UnitB
    )).ToList()
)).ToList();

return new PublicWorkOrderDto(
    wo?.Id ?? labors.First().Id, // Si no hay OT, usamos el Labor.Id como "container id" del DTO
    wo?.Description ?? labors.First().Type?.Name ?? "Labor",
    wo?.Status ?? labors.First().Status.ToString(),
    wo?.AssignedTo,
    wo?.DueDate ?? labors.First().EstimatedDate ?? DateTime.UtcNow,
    wo?.Field?.Name ?? labors.First().Lot?.Field?.Name,
    publicLabors
);
```

**Borrar** el bloque actual de líneas 96-120 que reasigna `filteredLabors` (queda absorbido arriba).

### 3.3. Hacer que `RealizeLaborPublic` soporte tokens sin OT

`ShareController.RealizeLaborPublic` líneas 170-176, reemplazar el filtro:

```csharp
var labor = await _context.Labors
    .IgnoreQueryFilters()
    .Include(l => l.Supplies)
    .FirstOrDefaultAsync(l =>
        l.Id == laborId &&
        (
            (sharedToken.WorkOrderId.HasValue && l.WorkOrderId == sharedToken.WorkOrderId)
            ||
            (!sharedToken.WorkOrderId.HasValue && SharedTokenAllowsLabor(sharedToken, l.Id))
        )
    );
```

Donde `SharedTokenAllowsLabor` es un helper privado del controller que parsea `Metadata.laborId`/`laborIds` y verifica que `l.Id` está adentro. **Mejor**: extraer el parseo de metadata a un helper estático reutilizable (ya hay duplicación entre `ValidateToken` y este método — extraerlo y limpiar el TODO).

### 3.4. Política de "una sola realización" (alineada con la spec)

La spec sección 5.3 ("estados: planeada o realizada") y el comportamiento ya implementado en `RealizeFromHtml` línea 267 (`sharedToken.IsUsed = true;`) indican que **un token debería poder usarse solo una vez**. Hoy `RealizeLaborPublic` **no** setea `IsUsed`. Decisión:

- Para mantener consistencia, **agregar** `sharedToken.IsUsed = true;` al final de `RealizeLaborPublic` (después de `await _context.SaveChangesAsync();`).
- Validar al inicio: `if (sharedToken.IsUsed) return BadRequest("Este enlace ya fue utilizado.");`

Esto evita reenvíos accidentales del formulario público que dupliquen el "realizado".

### 3.5. Mensaje de error específico cuando es 404 por causa correcta

`LaborExecution.razor` líneas 451-453 hoy tiene 3 mensajes ("expirado", "revocado", "no válido"). Agregar un cuarto: cuando el server devuelva específicamente "Token sin destino válido." o "Labor no encontrada.", mostrarlo tal cual al usuario (puede que la labor haya sido borrada después de generar el link).

### 3.6. Frontend público — soportar labor sin OT visual

`LaborExecution.razor` líneas 30-52 (cabecera "Ejecución de Labores") muestra `_order.OTNumber`, `_order.Description`, `_order.FieldName`. Cuando la OT no existe, el mapping en 3.2 pone valores derivados de la labor. **Confirmar visualmente** que la cabecera tiene sentido para un caso de una sola labor sin OT: si "@_order.OTNumber" muestra "S/N" y "@_order.Description" muestra "Pulverización" (el tipo de labor), está OK.

Si el usuario prefiere un layout claramente distinto para "validación de labor individual" vs "validación de OT", abrir issue para refinar la UI en un PR posterior — **no expandir el scope acá**.

## 4. Tests

**Archivo nuevo:** `src/GestorOT.Tests/Regression/PublicValidationTokenTests.cs`

Casos:

1. **`SubmitForValidation_StandaloneLabor_CreatesTokenWithNullWorkOrderId`** — labor sin OT → token persiste con `WorkOrderId = null` y `Metadata.laborId` presente.
2. **`SubmitForValidation_LaborWithOT_CreatesTokenWithOTId`** — labor en OT → token persiste con `WorkOrderId` correcto.
3. **`ValidateToken_StandaloneLaborToken_Returns200WithLaborData`** — token de labor suelta → 200 + DTO con la labor.
4. **`ValidateToken_OTToken_Returns200WithAllLabors`** — token de OT → 200 + DTO con todas las labores de la OT (regresión).
5. **`ValidateToken_OTTokenWithLaborIdsMetadata_FiltersLabors`** — token con `metadata.laborIds = [a, b]` → solo devuelve esas labores (regresión).
6. **`ValidateToken_InvalidToken_Returns404`** — hash no existe → 404.
7. **`ValidateToken_ExpiredToken_ReturnsBadRequest`** — token vencido → 400 con "expirado" en el body.
8. **`ValidateToken_RevokedToken_ReturnsBadRequest`** — token revocado → 400 con "revocado".
9. **`RealizeLaborPublic_StandaloneLaborToken_MarksAsRealized`** — POST con datos reales → labor pasa a `Realized`, supplies actualizadas, `IsUsed = true`.
10. **`RealizeLaborPublic_AlreadyUsedToken_Returns400`** — segundo POST con el mismo token → 400 "ya fue utilizado".
11. **`RealizeLaborPublic_OTToken_StillWorks`** — flujo con OT sigue funcionando (regresión).
12. **`SubmitForValidation_UsesConfiguredPublicBaseUrl_WhenSet`** — con `appsettings.App.PublicBaseUrl = "https://prod.example"`, la URL devuelta usa ese host (validar el cambio de 3.1).
13. **`SubmitForValidation_FallsBackToRequestHost_WhenPublicBaseUrlNotSet`** — sin config, usa `Request.Scheme + Host` (regresión).

Verificar que los tests existentes en `Tests/Regression/` siguen verdes:
- `FileAssetTests.cs`, `FileAssetSecurityTests.cs` (no relacionados directamente, pero el `_context.SaveChangesAsync` se invoca en flujos similares).
- `WorkOrderStatusTests.cs`, `PlaneamientoOriginalRegressionTests.cs`.

## 5. Smoke test manual

### Caso A: labor en OT

1. Crear OT con 3 labores. En una de ellas, click "Enviar para validar".
2. Copiar el link. Pegarlo en una pestaña incógnito (sin sesión).
3. La página `/public/labor-execution/{token}` debe cargar mostrando las 3 labores de la OT (comportamiento previo).
4. Completar hectáreas/insumos reales en una labor, "Realizar".
5. Volver al sistema autenticado: la labor figura como `Realized`.

### Caso B: labor suelta (el caso que rompía)

1. Crear una labor suelta (sin asignar a ninguna OT).
2. Desde el detalle de la labor o desde el panel de labores, click "Enviar para validar".
3. Copiar el link. Pegarlo en una pestaña incógnito.
4. La página debe cargar mostrando **la labor individual** (no error de "OT no encontrada").
5. Completar hectáreas reales + insumos reales, "Realizar".
6. Volver al sistema: la labor figura como `Realized`.

### Caso C: reuso del link

1. Repetir el flujo del Caso A o B y completar la realización.
2. Volver a abrir el mismo link → mensaje "Este enlace ya fue utilizado." o equivalente.

### Caso D: link expirado

1. Generar link, esperar más de 72h (o simular acortando `ExpiresAt` en BD).
2. Abrir → mensaje "Este enlace ha expirado."

### Caso E: link revocado

1. Generar link de OT, ir a "Revocar links" en la OT.
2. Abrir el link → "Este enlace ha sido revocado."

### Caso F: host de URL

Si el deploy tiene proxy reverso:
1. Generar link en producción.
2. Confirmar que la URL devuelta usa el host público (no `localhost`, no IP interna).

## 6. Definition of Done específica

- [ ] `ValidateToken` soporta `WorkOrderId = null` para labor suelta.
- [ ] `RealizeLaborPublic` soporta `WorkOrderId = null` para labor suelta.
- [ ] `RealizeLaborPublic` marca `IsUsed = true` al completar y rechaza tokens ya usados.
- [ ] Parseo de `Metadata` extraído a helper estático compartido (sin duplicar entre métodos).
- [ ] `appsettings.App.PublicBaseUrl` (opcional) implementado y respetado en `SubmitForValidation`.
- [ ] 13 tests nuevos en `PublicValidationTokenTests.cs`, todos verdes.
- [ ] Suite preexistente de regresión: verde.
- [ ] Smoke test manual de los casos A, B, C completado obligatoriamente. D, E, F si el entorno lo permite.
- [ ] PR description documenta:
  - Consultas a `context7` realizadas.
  - Si se configuró o no `UseForwardedHeaders` (depende del entorno del usuario).
  - Política definida de "un solo uso" para `RealizeLaborPublic`.

## 7. Lo que NO se cambia en este PR

- El formato del token ni el algoritmo de hash (`SHA256` + base64 URL-safe — sigue igual).
- La duración por defecto (72h para labor, 7 días para OT — coherente con código actual).
- El endpoint `/api/share/generate/{workOrderId}` (sigue siendo solo para OTs).
- La UI autenticada de `LaborExecution.razor` (ruta `/execution/{WorkOrderId:guid}` para usuarios logueados).
- El endpoint `RealizeFromHtml` y su flujo de HTML imprimible (no afectado por este bug).
- Adición de rate limiting o captcha en endpoints públicos — anotado para issue futura de hardening de seguridad.

---

## Notas para el agente

Este es el PR más invasivo del lote porque:
- Toca **dos** controllers que se hablan vía un contrato implícito (`SharedToken.WorkOrderId` puede ser null o no).
- Toca el flujo público (sin auth) — los errores se sienten más porque el usuario externo no tiene contexto.
- Cambia política de uso de token (de "infinitos usos" a "un solo uso" para realización de labor).

Por eso va **último** en el orden de PRs (ver `00-README-plan-v2.md` sección 2): si algo se rompe, los otros 4 PRs ya están mergeados y aliviaron el dolor inmediato.

Si durante la implementación aparecen señales de un problema más profundo en el modelo de `SharedToken` (ej. necesidad de tipos de token distintos `LaborToken` vs `WorkOrderToken`), **detener** y proponer un rediseño en un documento aparte antes de seguir tocando.
