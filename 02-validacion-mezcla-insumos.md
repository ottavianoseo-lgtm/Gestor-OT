# Bug #2 — Prohibiciones de mezcla de insumos no se aplican al guardar labor

> Módulo: **Validación Agronómica / Labores**
> Criticidad: **Alta** (riesgo agronómico real: el sistema deja guardar combinaciones que el usuario marcó como prohibidas)
> Estimación: **3-5h** incluyendo tests
> PR: mediano

---

## 1. Causa raíz

Existe el motor de validación, existe la UI para administrar reglas, existe el endpoint público que valida una mezcla aislada — **pero el pipeline de guardado de labor no llama al validador de mezcla.**

**Evidencia concreta:**

`src/GestorOT.Api/Controllers/LaborsController.cs`, método privado `ValidateLaborAsync` (líneas 1008-1099). Hoy ejecuta tres validaciones:

- Línea 1062: `ValidateLaborSurfaceAsync` ✅
- Línea 1072: `ValidateLaborDatesInRotationAsync` ✅
- Línea 1083: `ValidateLaborActivityAsync` ✅
- **Falta**: `ValidateMixAsync(supplyIds)` ❌

`AgronomicValidationService.ValidateMixAsync` está implementado correctamente (`src/GestorOT.Infrastructure/Services/AgronomicValidationService.cs` líneas 17-38) y es llamado desde `TankMixRulesController.ValidateMix` (línea 84) cuando el usuario hace la validación manual. **Nadie lo llama desde el flujo POST/PUT de labor.**

Consecuencia: cuando el usuario configura una regla "Producto X + Producto Y = prohibido" desde `Admin/TankMix.razor` y luego carga una labor con ambos insumos, **el sistema guarda sin protestar.**

## 2. Pre-lectura obligatoria con context7 (MCP)

1. `context7` — `Microsoft.EntityFrameworkCore` v10: traducción de `Contains` sobre `List<Guid>` a `WHERE ... IN (...)` en PostgreSQL, performance con listas largas.
2. `context7` — `Microsoft.AspNetCore.Mvc`: convención para devolver advertencias (no errores) en una respuesta exitosa. El repo ya tiene patrón propio (`(string? Error, List<string> Warnings)`) — respetarlo, no inventar `Result<T>` ni `ProblemDetails`.
3. `context7` — Blazor AntDesign `Modal`: cómo mostrar una lista de warnings antes de un `OnOk` y permitir cancelar.

Dejar registro en el PR.

## 3. Plan de implementación

### 3.1. Definir la política de bloqueo vs advertencia

La spec sección 11.1 marca preferencia por **"advertencias antes que bloqueos"**, pero la entidad `TankMixRule` tiene un campo `Severity` (string). Decisión consensuada con la spec:

| `Severity` de la regla | Acción al guardar labor |
|---|---|
| `"Error"` o `"Block"` (prohibición dura) | **Bloquear**: devolver como error, no guardar |
| `"Warning"` (advertir) | Permitir, agregar a `warnings` |
| cualquier otro / vacío | Tratar como `"Warning"` para no romper datos existentes |

El admin `Admin/TankMix.razor` ya permite setear severidad por regla; **no** se modifica la UI de admin en este PR.

### 3.2. Modificar `ValidateLaborAsync` en `LaborsController`

**Archivo:** `src/GestorOT.Api/Controllers/LaborsController.cs`

Después del bloque de validación de actividad (línea 1096, antes del `return (null, warnings)` de línea 1098), agregar:

```csharp
// 3. Tank-Mix Rule Validation
if (dto.Supplies != null && dto.Supplies.Count >= 2)
{
    var supplyIds = dto.Supplies
        .Select(s => s.SupplyId)
        .Where(id => id != Guid.Empty)
        .Distinct()
        .ToList();

    if (supplyIds.Count >= 2)
    {
        var mixAlerts = await _validationService.ValidateMixAsync(supplyIds);

        var blocking = mixAlerts
            .Where(a => string.Equals(a.Severity, "Error", StringComparison.OrdinalIgnoreCase)
                     || string.Equals(a.Severity, "Block", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (blocking.Any())
        {
            var msg = "Mezcla prohibida: " + string.Join(" · ",
                blocking.Select(b => $"{b.ProductAName} + {b.ProductBName} ({b.WarningMessage})"));
            return (msg, warnings);
        }

        foreach (var alert in mixAlerts)
        {
            warnings.Add($"Advertencia de mezcla: {alert.ProductAName} + {alert.ProductBName} — {alert.WarningMessage}");
        }
    }
}
```

**Por qué acá**: este método ya se invoca desde:
- `CreateLabor` (POST, línea 99 — `var validation = await ValidateLaborAsync(...)`)
- `UpdateLabor` (PUT, línea 231 — `var validation = await ValidateLaborAsync(dto)`)

Insertar la validación de mezcla en este único punto cubre **ambos** flujos sin duplicar código.

### 3.3. Verificar que `dto.Supplies` viaje en POST/PUT

`src/GestorOT.Shared/Dtos/LaborDto.cs` — confirmar que `LaborDto.Supplies` es `List<LaborSupplyDto>` y se serializa. Si el cliente no manda los supplies en el body del POST/PUT (porque los guarda en un endpoint aparte), la validación no se dispararía. Confirmar leyendo `LaborEditorForm.razor` cómo arma el payload.

> **Si y solo si** los supplies se guardan en un endpoint separado (ej. `POST /api/labors/{id}/supplies`), agregar la misma validación allí. Documentar el hallazgo y no expandir el scope sin avisar.

### 3.4. Otros caminos de creación de labor que también deben validar

Caminos identificados que crean labores con supplies y **podrían saltarse** la validación si no llaman `ValidateLaborAsync`:

| Endpoint | Línea | Acción |
|---|---|---|
| `POST /api/labors/bulk-from-strategy` (línea 795) | Sí crea labores con supplies derivados de la estrategia | **Aplicar la misma validación** dentro del bucle de creación. Si hay bloqueo en una labor, devolver el detalle de cuál(es) falló. |
| `POST /api/labors/{id}/replicate` (línea 377) | Replica una labor — los supplies se copian de la fuente. Si la fuente ya pasó validación, en teoría la copia también, **pero las reglas pueden haber cambiado entre creación y réplica** | Validar al replicar. |
| `POST /api/labors/{id}/execute-standalone` (línea 435) | Marca como realizada — no cambia supplies, no requiere revalidar mezcla |

Para `bulk-from-strategy` y `replicate`, **extraer la lógica de validación de mezcla a un método privado** `ValidateSupplyMixAsync(List<Guid> supplyIds)` reutilizable, para no duplicar el bloque.

### 3.5. Mostrar el bloqueo en el frontend

`src/GestorOT.Client/Components/LaborEditorForm.razor` — al recibir un 400 con el mensaje "Mezcla prohibida: ...", debe mostrarse en un `Alert` rojo dentro del modal, no en un toast efímero. Si ya hay un manejador genérico de errores (`ErrorHandlingHttpHandler.cs`), confirmar que **no oculta** el body del 400.

Las advertencias (severidad Warning) ya tienen camino: el repo retorna `warnings` en el response del POST/PUT. Confirmar que el componente las muestra (si no, agregar un `Alert` amarillo con la lista).

## 4. Tests

**Archivo nuevo:** `src/GestorOT.Tests/Regression/TankMixValidationOnSaveTests.cs`

Casos mínimos:

1. **`CreateLabor_WithBlockedMixRule_ReturnsBadRequest`** — regla Error entre A y B, POST labor con A+B → 400 + mensaje "Mezcla prohibida".
2. **`CreateLabor_WithBlockedMixRule_DoesNotPersistLabor`** — mismo escenario, verificar que el `Labors.Count` no incrementó.
3. **`CreateLabor_WithWarningMixRule_PersistsAndReturnsWarning`** — regla Warning entre A y B → 200/201 + warning en la respuesta + labor guardada.
4. **`CreateLabor_WithSingleSupply_SkipsValidation`** — un solo insumo, no se consulta `ValidateMixAsync`.
5. **`CreateLabor_WithUnrelatedSupplies_NoWarnings`** — A y B sin regla configurada → sin warnings.
6. **`UpdateLabor_AddingBlockedSecondSupply_ReturnsBadRequest`** — labor existente con A, PUT agregando B (prohibido) → 400.
7. **`UpdateLabor_RemovingSecondSupply_AllowsThrough`** — labor con A+B (bloqueada en teoría, pero supongamos creada antes de la regla), PUT removiendo B → 200.
8. **`BulkFromStrategy_WithBlockedMix_RejectsAffectedLabors`** — estrategia con un item que combina A+B prohibidos → devuelve detalle por labor afectada (no rompe el bulk completo si hay otras válidas — decidir comportamiento: ¿abortar todo o crear las válidas y reportar las falladas? Recomendado: **abortar todo** para coherencia, dejar al usuario corregir y reintentar).
9. **`ValidateMixAsync_RuleSeverityCaseInsensitive`** — `"error"`, `"ERROR"`, `"Error"` → todos bloquean.
10. **Regresión existente — `WorkOrderStatusTests`, `RotationServiceTests`, `PlaneamientoOriginalRegressionTests`**: **deben seguir verdes**.

## 5. Smoke test manual

1. Como admin, `Admin > TankMix` → crear regla: Glifosato + 2,4-D, severidad **Error**, mensaje "Antagonismo confirmado".
2. Crear segunda regla: Glifosato + Aceite agrícola, severidad **Warning**, mensaje "Verificar dosis".
3. Crear labor en cualquier lote, agregar **Glifosato + 2,4-D** → al guardar, **error rojo visible**, labor **no se guarda**, modal sigue abierto.
4. Cambiar 2,4-D por Aceite agrícola → al guardar, **advertencia amarilla**, labor **se guarda**.
5. Editar la labor guardada, agregar 2,4-D → al guardar, **bloqueo**.
6. Bulk desde estrategia: crear estrategia con item que use Glifosato + 2,4-D, aplicar a un lote → la operación falla con detalle de qué labor tiene el conflicto, **ninguna labor de la estrategia se persiste**.
7. **Regresión**: crear una labor normal sin insumos prohibidos → sigue funcionando como siempre.

## 6. Definition of Done específica

- [ ] `ValidateLaborAsync` invoca `ValidateMixAsync` con la lista de supplyIds.
- [ ] Severidad "Error"/"Block" → 400; "Warning" → 200 + warning.
- [ ] Lógica extraída a método privado y reutilizada en `BulkFromStrategy` y `ReplicateLabor`.
- [ ] 10 tests nuevos, todos verdes; suite preexistente sin regresiones.
- [ ] Smoke test manual de los 7 pasos completado.
- [ ] Frontend muestra el bloqueo (Alert rojo) y las advertencias (Alert amarillo).
- [ ] PR description lista las consultas a `context7`.

## 7. Lo que NO se cambia en este PR

- La UI de administración de reglas (`Admin/TankMix.razor`).
- El endpoint público `POST /api/tankmixrules/validate` (sigue siendo útil para validación "preview" antes de guardar).
- La firma de `IAgronomicValidationService.ValidateMixAsync`.
- Las reglas existentes en la base de datos.
- La validación de mezcla en el flujo público `ShareController/RealizeLaborPublic` (eso queda para issue separada vinculada al Bug #5).
