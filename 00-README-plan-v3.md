# Plan de Desarrollo v3 — Gestor OT

> Bugs PRIORITARIOS reportados sobre el detalle de OT (Insumos Consolidados, Detalle por Labor, Total Real editable, Export PDF).
> Rama: `fix-bugs-revisión-gestor-ot` (continúa sobre la v2 ya aplicada)
> Stack: .NET 10 · Blazor WASM · EF Core 10 · PostgreSQL/PostGIS · AntDesign Blazor 1.6.1 · xUnit + Moq

---

## 0. Prerrequisito BLOQUEANTE — el build está roto

El archivo `build_output.txt` del repo (timestamp 2026-05-15 14:47) reporta **22 errores de compilación** en `src/GestorOT.Client/Pages/WorkOrderDetailFinal.razor`:

```
WorkOrderDetailFinal.razor(31,15): error RZ9980: Unclosed tag 'div' with no matching end tag.
WorkOrderDetailFinal.razor(42,14): error RZ1034: Found a malformed 'Row' tag helper.
WorkOrderDetailFinal.razor(43,18): error RZ1042: Component or tag helper 'Col' must be self-closing...
... (22 errores)
```

Al revisar el archivo a mano se ve sintácticamente correcto, lo que sugiere una de tres cosas:

1. **El `build_output.txt` está obsoleto** y el código ya compila.
2. **Falta algún `@using`** que provoque que el parser Razor no reconozca `Row`/`Col` como componentes y los trate como void HTML.
3. **El archivo `OTDetalleFinalBase.cs`** (la code-behind) tiene un símbolo desincronizado.

**Paso 0 obligatorio antes de tocar bugs**: el agente que ejecute este plan debe correr `dotnet build` y confirmar que compila. Si no compila:

- Revisar que `@using AntDesign` esté en el archivo y/o en `_Imports.razor`.
- Verificar que en el `.csproj` esté `<PackageReference Include="AntDesign" Version="1.6.1" />` (ya confirmado en `src/GestorOT.Api/GestorOT.Api.csproj`, falta confirmar en `src/GestorOT.Client/GestorOT.Client.csproj`).
- Si persiste, abrir `WorkOrderDetailFinal.razor` y reemplazar `<AntDesign.Row>` y `<AntDesign.Col>` por `<Row>` y `<Col>` (o viceversa) según cómo esté declarado en `_Imports.razor`.

**No avanzar con ningún bug hasta que el build esté en verde.** Sin compilación no hay forma de validar cambios sin romper más cosas.

---

## 1. Reglas de oro (idénticas a las del plan v2 — siguen vigentes)

Recordatorio condensado. Para detalle ver `00-README-plan-v2.md` del paquete anterior.

### 1.1 context7 (MCP) obligatorio antes de tocar código

El agente debe consultar `context7` para cada librería involucrada. Lista por bug en cada `.md`. No se puede operar desde memoria entrenada — APIs cambian entre versiones menores. Registrar las consultas en la descripción del PR.

### 1.2 Garantía de no-regresión

1. `dotnet test` antes y después de cada cambio. **El conteo de tests pasando no puede disminuir.**
2. Cada `.md` incluye su propio test de causa raíz que debe pasar de rojo a verde.
3. PRs chicos por bug. No mezclar.
4. Smoke test manual obligatorio antes de mergear.

### 1.3 Invariantes funcionales (spec sección 0)

- Tenant aislamiento.
- Campaña como segundo tenant.
- Campañas bloqueadas son solo lectura.
- Labores no cambian de campaña una vez creadas.
- Planeamiento Original es inmutable salvo admin.

---

## 2. Mapa de bugs (con imágenes del PDF)

| Bug | Imagen PDF | Módulo | Archivo `.md` |
|---|---|---|---|
| 1 | imagen1 (tab consolidados con 3 huecos) + imagen2 (campo Retiro de Insumos a sacar de Labor) | Detalle OT — tab Insumos Consolidados + modal Labor | `01-insumos-consolidados-3-columnas.md` |
| 2 | imagen3 (Desvío de Insumos Detalle Técnico, falta Ha y reorganización) | Detalle OT — detalle por labor | `02-detalle-insumos-por-labor.md` |
| 3 | imagen4 (tabla Planeado: Ha/Coef/Cantidad/% del Total) | Detalle OT — Insumos Consolidados (interacción) | `03-total-real-editable-regla-de-tres.md` |
| 4 | imagen5 (PDF "Orden de Trabajo #12" formato deseado) | Detalle OT — exportación | `04-exportacion-ot-pdf.md` |

Los 4 bugs viven en la misma pantalla (`/workorders/{id}` → `WorkOrderDetailFinal.razor`) y comparten DTOs/servicios.

---

## 3. Orden de ejecución recomendado

De menor riesgo a mayor riesgo. Cada uno debe estar mergeado y verde antes de empezar el siguiente.

```
[0]  Arreglar build roto (prerrequisito)
 │
[1]  Bug 1 — Insumos Consolidados: 3 columnas nuevas + mover campo desde Labor
 │   (cambio mayormente de UI + 1 campo extra en DTO. Sin migraciones de BD.)
 │
[2]  Bug 2 — Detalle por Labor: agregar Ha, renombrar Dosis→Coef, reagrupar plan/real
 │   (cambio solo de UI en ExpandTemplate de LaborList.razor. Sin backend.)
 │
[3]  Bug 3 — Total Real editable con regla de tres bidireccional
 │   (cambio de UI + lógica de recálculo en cliente + endpoint nuevo opcional.
 │    El más delicado de los 4 — toca la fórmula que ya existe en backend.)
 │
[4]  Bug 4 — Export PDF imprimible
     (cambio nuevo: librería QuestPDF + endpoint + botón. Aislado.)
```

---

## 4. Cambios transversales que necesitan los 4 bugs

Para evitar duplicación, estos cambios deben hacerse **una sola vez** y los `.md` los referencian:

### 4.1 Agregar `SupplyUnit` a `WorkOrderSupplyApprovalDto`

**Archivo:** `src/GestorOT.Shared/Dtos/LaborDto.cs` líneas 111-127.

Hoy el DTO no expone la unidad del insumo. Es necesario para Bug 1 (mostrar Unidad en la tabla de consolidados) y Bug 4 (mostrar Unidad en el PDF).

Cambio:

```csharp
public class WorkOrderSupplyApprovalDto
{
    public Guid Id { get; set; }
    public Guid WorkOrderId { get; set; }
    public Guid SupplyId { get; set; }
    public string? SupplyName { get; set; }
    public string? SupplyUnit { get; set; }  // ← AGREGAR (Inventory.UnitA)
    public decimal TotalCalculated { get; set; }
    public decimal ApprovedWithdrawal { get; set; }
    public string? WithdrawalCenter { get; set; }
    public decimal? RealTotalUsed { get; set; }

    public WorkOrderSupplyApprovalDto() { }
    public WorkOrderSupplyApprovalDto(Guid id, Guid workOrderId, Guid supplyId, string? supplyName, string? supplyUnit, decimal totalCalculated, decimal approvedWithdrawal, string? withdrawalCenter, decimal? realTotalUsed)
    {
        Id = id; WorkOrderId = workOrderId; SupplyId = supplyId; SupplyName = supplyName; SupplyUnit = supplyUnit; TotalCalculated = totalCalculated; ApprovedWithdrawal = approvedWithdrawal; WithdrawalCenter = withdrawalCenter; RealTotalUsed = realTotalUsed;
    }
}
```

Y en `src/GestorOT.Infrastructure/Services/WorkOrderQueryService.cs` líneas 205-216, agregar `a.Supply?.UnitA` al constructor:

```csharp
var supplyApprovalsDto = workOrder.SupplyApprovals
    .OrderBy(a => a.Supply?.ItemName)
    .Select(a => new WorkOrderSupplyApprovalDto(
        a.Id,
        a.WorkOrderId,
        a.SupplyId,
        a.Supply?.ItemName,
        a.Supply?.UnitA,        // ← AGREGAR
        a.TotalCalculated,
        a.ApprovedWithdrawal,
        a.WithdrawalCenter,
        a.RealTotalUsed
    )).ToList();
```

Y verificar que el `Include` ya trae `Supply` (debería). Si no, agregar `.Include(w => w.SupplyApprovals).ThenInclude(a => a.Supply)`.

Este cambio NO rompe nada existente porque solo agrega un campo opcional al DTO. Hacer este cambio en el PR del Bug 1.

### 4.2 Confirmar que `Inventory.UnitA` esté poblado en datos reales

La spec sección 16.1 dice que el sistema usa "Datos reales - consultas API a clientes de GestorMax". La sincronización de inventario debe traer `UnitA`. Si la columna llega vacía desde el ERP, el bug 1 se va a ver como "Unidad: (vacío)" aunque el código sea correcto.

**Acción del agente**: antes de empezar el bug 1, ejecutar contra la base de pruebas:

```sql
SELECT COUNT(*) FILTER (WHERE "UnitA" IS NULL OR "UnitA" = '') AS sin_unidad,
       COUNT(*) AS total
FROM public."Inventories";
```

Si `sin_unidad / total > 0` → reportar al usuario antes de empezar el fix de UI. Puede ser un problema de sync, no del código de la app.

---

## 5. Definition of Done global

Idéntica a la v2. Cada PR requiere:

- [ ] Build limpio (paso 0 cumplido).
- [ ] `context7` consultado para cada librería involucrada — registro en descripción del PR.
- [ ] Test de causa raíz nuevo, en verde.
- [ ] `dotnet test` total sin regresiones.
- [ ] Smoke test manual ejecutado.
- [ ] Sin `TODO`/`HACK` introducidos.
- [ ] Si hay migraciones EF — `Up` y `Down` probados en base limpia.

---

## 6. Preguntas pendientes que deja este plan abierto

Si alguna respuesta cambia al ejecutar, **detener el bug correspondiente** y reportar antes de seguir:

1. **Bug 3 — escritura bidireccional**: el plan asume que al editar `Total Real` en consolidados se persiste **solo** `WorkOrderSupplyApproval.RealTotalUsed`, y los valores por labor se proyectan al vuelo vía `CalculatedDose`/`CalculatedTotal` (que ya existe en `WorkOrderQueryService` línea 172-203). Si el usuario quiere que también se escriban en cada `LaborSupply.RealTotal`/`RealDose`, hay que ampliar el plan — ver advertencia en `03-total-real-editable-regla-de-tres.md` sección 5.

2. **Bug 4 — librería de PDF**: el plan elige **QuestPDF** (gratis comercial hasta 1M USD, sin Chromium, PDF nativo). Si el equipo prefiere `Puppeteer-Sharp` o `iTextSharp`, se cambia solo la implementación del servicio; el endpoint y DTOs no cambian.

3. **Bug 4 — unidad en PDF imagen5 muestra "Hectárea (HA)" para Glifosato**: esto puede ser dato mal cargado (Glifosato debería ser "L" o "kg"). El plan **respeta** lo que diga `Inventory.UnitA` — si está mal cargada, el PDF lo muestra mal. Es responsabilidad de la sincronización ERP, no del export.
