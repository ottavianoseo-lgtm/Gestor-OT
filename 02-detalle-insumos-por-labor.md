# Bug 2 — Detalle de insumos por Labor: reorganizar columnas, agregar Ha, renombrar Dosis→Coef

> Módulo: **Detalle de OT — `ExpandTemplate` del listado de labores**
> Imagen del PDF: **imagen3** (tabla "Desvío de Insumos (Detalle Técnico)" con "Ha" marcado en rojo y flecha indicando que falta entre UNIDAD y DOSIS PLAN.)
> Criticidad: **Alta** (la unidad no se trae, la Ha por insumo se pierde, las dosis están desagrupadas)
> Estimación: **2-3h**

---

## 1. Qué pide el bug

> "En el detalle de insumos por Labor dentro una OT, agrupemos primero toda la info Planeada, y luego toda la info Realizada. Falta la columna de Ha por insumo, tanto la planeada como la realizada (que pueden ser distintas a la de lote. Se configuran por cada insumo dentro de la Labor). Entonces las columnas quedarían:
> -Insumo
> -Unidad (que no las está trayendo, el dato está en el modal 'inventario' en cada insumo tiene el detalle de la unidad de medida)
> -Ha. Plan.
> -Dosis Plan. (Cambiemoslo por Coef. Plan. así es coincidente con cómo se llama en Gestor)
> -Total Plan.
> -Ha. Real.
> -Coef. Real.
> -Total Real"

Resumen de cambios:

| Cambio | Detalle |
|---|---|
| 1 | Las columnas se reagrupan: primero **bloque Planeado** (Ha Plan / Coef Plan / Total Plan), luego **bloque Realizado** (Ha Real / Coef Real / Total Real). |
| 2 | Agregar **Unidad** (lectura desde `Inventory.UnitA` vía `LaborSupplyDto.SupplyUnit`). |
| 3 | Agregar **Ha. Plan.** (de `LaborSupplyDto.PlannedHectares`). |
| 4 | Agregar **Ha. Real** (de `LaborSupplyDto.RealHectares`). |
| 5 | Renombrar **Dosis Plan.** → **Coef. Plan.** (mismo dato, otro label). |
| 6 | Renombrar **Dosis Real** → **Coef. Real.** (mismo dato, otro label). |

## 2. Estado actual del código

La tabla que el bug describe **es el `ExpandTemplate` dentro de `LaborList.razor`**, que aparece al expandir una fila de labor en la tab "Labores" del detalle de OT.

**Archivo:** `src/GestorOT.Client/Components/LaborList.razor` líneas 94-118.

Estado actual (líneas 103-108):

```razor
<Table TItem="LaborSupplyDto" DataSource="@laborRow.Data.Supplies" Size="TableSize.Small" HidePagination Style="background: transparent;">
    <PropertyColumn Property="s => s.SupplyName" Title="Insumo" />
    <PropertyColumn Property="s => s.PlannedTotal" Title="Cant. Plan." Format="N2" />
    <PropertyColumn Property="s => s.RealTotal" Title="Cant. Real" Format="N2" />
    <PropertyColumn Property="s => s.UnitOfMeasure" Title="Unidad" />
</Table>
```

Hoy solo 4 columnas, sin Ha por insumo, sin Coef.

**Buena noticia**: `LaborSupplyDto` (en `Shared/Dtos/LaborDto.cs` líneas 63-87) ya expone todo lo necesario:

- `SupplyName`, `SupplyUnit` (= `Inventory.UnitA`)
- `PlannedHectares`, `RealHectares`
- `PlannedDose`, `RealDose` ← estos son los "coeficientes"
- `PlannedTotal`, `RealTotal`
- `UnitOfMeasure` (la unidad del coeficiente, ej "L/Ha")

**Confirmación importante sobre la unidad**: el texto del bug dice "el dato está en el modal inventario". Eso corresponde a `Inventory.UnitA`, que el query service ya mapea a `LaborSupplyDto.SupplyUnit` (ver `WorkOrderQueryService.cs` línea 152: `s.Supply?.UnitA`). **Usar `SupplyUnit` y no `UnitOfMeasure`** — son cosas distintas:

- `SupplyUnit` = unidad del insumo en sí ("L", "kg") ← lo que pide el bug
- `UnitOfMeasure` = unidad del coeficiente ("L/Ha", "kg/Ha") ← lo que está hoy

## 3. Pre-lectura obligatoria con context7 (MCP)

1. `context7` — **AntDesign Blazor v1.6.1**: `<Column TData>` vs `<PropertyColumn>` con `Format`, agrupación visual con `<ColumnGroup>` si existe en esta versión (para los headers "Planeado"/"Realizado").
2. `context7` — **AntDesign Blazor** styles: cómo aplicar background diferenciado a un grupo de columnas (CSS selector por `data-key` o clase custom).

## 4. Plan de implementación

### 4.1 Reemplazar las columnas del ExpandTemplate

**Archivo:** `src/GestorOT.Client/Components/LaborList.razor` líneas 94-118.

Versión simple (sin agrupación visual de headers — solo orden):

```razor
<ExpandTemplate Context="laborRow">
    <div style="padding: 12px; background: rgba(255,255,255,0.02); border-radius: 8px; border: 1px solid rgba(255,255,255,0.05);">
        <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 12px; border-bottom: 1px solid rgba(255,255,255,0.05); padding-bottom: 8px;">
            <h4 style="margin: 0; color: #3498DB; font-size: 14px; display: flex; align-items: center; gap: 8px;">
                <Icon Type="info-circle" /> Detalle de Insumos
            </h4>
        </div>
        @if (laborRow.Data.Supplies != null && laborRow.Data.Supplies.Any())
        {
            <Table TItem="LaborSupplyDto" DataSource="@laborRow.Data.Supplies" Size="TableSize.Small" HidePagination Style="background: transparent;">

                <PropertyColumn Property="s => s.SupplyName" Title="Insumo" />

                <PropertyColumn Property="s => s.SupplyUnit" Title="Unidad" />

                <!-- Bloque Planeado -->
                <Column TData="decimal" Title="Ha. Plan.">
                    <span class="planned-cell">@context.PlannedHectares.ToString("N2")</span>
                </Column>
                <Column TData="decimal" Title="Coef. Plan.">
                    <span class="planned-cell">@context.PlannedDose.ToString("N2")</span>
                </Column>
                <Column TData="decimal" Title="Total Plan.">
                    <span class="planned-cell">@context.PlannedTotal.ToString("N2")</span>
                </Column>

                <!-- Bloque Realizado -->
                <Column TData="decimal?" Title="Ha. Real">
                    <span class="real-cell">@(context.RealHectares?.ToString("N2") ?? "—")</span>
                </Column>
                <Column TData="decimal?" Title="Coef. Real">
                    <span class="real-cell">@(context.RealDose?.ToString("N2") ?? "—")</span>
                </Column>
                <Column TData="decimal?" Title="Total Real">
                    <span class="real-cell">@(context.RealTotal?.ToString("N2") ?? "—")</span>
                </Column>
            </Table>
        }
        else
        {
            <div style="padding: 20px; text-align: center; color: rgba(255,255,255,0.3);">
                <Icon Type="inbox" Style="font-size: 24px; display: block; margin-bottom: 8px;" />
                No hay insumos registrados para esta labor
            </div>
        }
    </div>
</ExpandTemplate>
```

Agregar al bloque `<style>` del archivo:

```css
.planned-cell {
    color: rgba(52, 152, 219, 0.85);  /* azul tenue = planeado */
    font-weight: 500;
}
.real-cell {
    color: rgba(46, 204, 113, 0.85);  /* verde tenue = real */
    font-weight: 500;
}
```

Esto da un tinte visual a cada bloque sin necesidad de agrupación de headers (que en algunas versiones de AntDesign Blazor no está implementada limpiamente en `<Table>`).

### 4.2 Opcional — agrupación visual de headers con `<ColumnGroup>` (si la versión lo soporta)

Si `context7` confirma que AntDesign Blazor 1.6.1 soporta `<ColumnGroup>` en el componente `<Table>`, el agente puede agregar headers agrupados:

```razor
<ColumnGroup Title="Planeado">
    <Column ...>Ha. Plan.</Column>
    <Column ...>Coef. Plan.</Column>
    <Column ...>Total Plan.</Column>
</ColumnGroup>
<ColumnGroup Title="Realizado">
    ...
</ColumnGroup>
```

**Si no lo soporta** (probable, AntDesign Blazor no tiene paridad total con React/Vue): mantener el approach del 4.1 con los colores por bloque y agregar una fila separadora visual con CSS-only.

### 4.3 Verificar que `WorkOrderQueryService` traiga los datos correctos

**Archivo:** `src/GestorOT.Infrastructure/Services/WorkOrderQueryService.cs` líneas 138-155.

Ya pasa los campos. Confirmar:

- Línea 142: `s.PlannedHectares` ✅
- Línea 143: `s.RealHectares` ✅
- Línea 152: `s.Supply?.UnitA` → mapea a `SupplyUnit` ✅

**Acción del agente**: correr el endpoint `GET /api/workorders/{id}` y verificar en el JSON de respuesta que cada `supply` tiene los campos esperados. Si falta alguno, el `Include(...).ThenInclude(...)` está incompleto. Verificar:

```csharp
.Include(w => w.Labors)
    .ThenInclude(l => l.Supplies)
        .ThenInclude(s => s.Supply)  // ← este ThenInclude es el que trae Inventory para UnitA
```

`WorkOrderQueryService.cs` líneas 111-113 ya lo hacen. ✅

### 4.4 Asegurar que `LaborEditorForm` permita editar `PlannedHectares` y `RealHectares` por insumo

**Archivo:** `src/GestorOT.Client/Components/LaborEditorForm.razor` líneas 218-222.

Ya está hecho (líneas 218-221):

```razor
<FormItem Label="@(_model.Status == "Realized" ? "Ha. Reales" : "Hectáreas")" Style="margin-bottom: 0;">
    <AntDesign.InputNumber TValue="decimal"
        Value="@(_model.Status == "Realized" ? (supply.RealHectares ?? supply.PlannedHectares) : supply.PlannedHectares)"
        OnChange="@(v => { if (_model.Status == "Realized") supply.RealHectares = v; else supply.PlannedHectares = v; })"
        Min="0m" Step="0.1m" Style="width: 100%; color: var(--text-primary);" />
</FormItem>
```

Pero ahí hay un detalle: cuando la labor está en estado "Realized", el editor solo permite cambiar `RealHectares`. Cuando está en "Planned", solo cambia `PlannedHectares`. Eso está bien.

**Nada que cambiar en este PR sobre el editor.**

### 4.5 Acción defensiva — si los Ha por insumo están NULL en BD para labores viejas

Las labores creadas antes de la introducción del campo `LaborSupply.PlannedHectares` pueden tener ese campo en NULL o 0. Para evitar mostrar "0.00" donde el usuario espera el Ha de la labor:

**Opción A (recomendada, mínimamente invasiva)**: si `PlannedHectares == 0`, mostrar el Ha de la labor (`laborRow.Data.Hectares`) como fallback:

```razor
<Column TData="decimal" Title="Ha. Plan.">
    <span class="planned-cell">
        @((context.PlannedHectares > 0 ? context.PlannedHectares : laborRow.Data.Hectares).ToString("N2"))
    </span>
</Column>
```

**Opción B**: una migración EF que poblara `LaborSupply.PlannedHectares = Labor.Hectares` para registros con `PlannedHectares == 0`. Más invasivo, decisión del usuario.

**Por defecto el plan elige A** — opción no destructiva. Si después se decide B, va en un PR de mantenimiento aparte.

## 5. Tests

**Archivo nuevo:** `src/GestorOT.Tests/Regression/LaborSupplyDtoColumnsTests.cs`

Casos:

1. **`LaborSupplyDto_HasSupplyUnitField`** — reflection: confirma que la propiedad existe (la usa la nueva columna "Unidad").
2. **`LaborSupplyDto_HasPlannedHectaresAndRealHectares`** — reflection: previene regresión si alguien las borra del DTO.
3. **`WorkOrderQueryService_GetById_PopulatesSupplyUnitFromInventoryUnitA`** — Inventory.UnitA = "kg" → DTO trae `SupplyUnit = "kg"`.
4. **`WorkOrderQueryService_GetById_PopulatesPlannedHectaresFromLaborSupply`** — `LaborSupply.PlannedHectares = 12.5` → DTO trae `PlannedHectares = 12.5`.
5. **`WorkOrderQueryService_GetById_NullRealHectares_DoesNotCrash`** — labor en estado "Planned", `RealHectares = null`, el query devuelve null sin explotar.

**Test visual (bUnit, opcional)**: si el repo introduce bUnit, agregar un test que renderice el ExpandTemplate con datos mockeados y haga assert sobre el orden de las columnas. Si no hay bUnit, omitir y compensar con smoke manual.

## 6. Smoke test manual

1. Crear una labor con 2 insumos. Insumo A: `PlannedHectares = 10`, `PlannedDose = 2`. Insumo B: `PlannedHectares = 5`, `PlannedDose = 1`.
2. Abrir OT que contiene esa labor → tab "Labores" → expandir la fila.
3. **Antes:** 4 columnas (Insumo, Cant. Plan., Cant. Real, Unidad).
4. **Después:** 8 columnas en este orden: Insumo, Unidad, Ha. Plan., Coef. Plan., Total Plan., Ha. Real, Coef. Real, Total Real.
5. Insumo A muestra: Ha. Plan. = 10.00, Coef. Plan. = 2.00, Total Plan. = 20.00.
6. Insumo B muestra: Ha. Plan. = 5.00, Coef. Plan. = 1.00, Total Plan. = 5.00.
7. Bloque Planeado en azul tenue, bloque Realizado en verde tenue (o, si se aplicó ColumnGroup, headers agrupados).
8. Para una labor en estado "Realized", se ven valores en las columnas Real. Para "Planned", las columnas Real muestran "—".
9. La columna **Unidad** muestra `Inventory.UnitA` (ej. "L", "kg"). Si está vacío en BD → "—".
10. **Regresión**: el editor de labor sigue funcionando — agregar insumo, modificar hectáreas, guardar, refrescar, ver los cambios en el expand.

## 7. Definition of Done específica

- [ ] Build limpio (paso 0 del README).
- [ ] ExpandTemplate de `LaborList.razor` con 8 columnas en el orden correcto.
- [ ] Bloque planeado y realizado visualmente diferenciados (color o ColumnGroup).
- [ ] Renombres aplicados: "Dosis Plan." → "Coef. Plan.", "Dosis Real" → "Coef. Real".
- [ ] Columna "Unidad" usa `SupplyUnit` (no `UnitOfMeasure`).
- [ ] Fallback de `PlannedHectares == 0` → `Labor.Hectares` aplicado.
- [ ] 5 tests nuevos en `LaborSupplyDtoColumnsTests.cs`, en verde.
- [ ] Smoke test de 10 pasos completado.
- [ ] PR description con consultas a `context7`.
- [ ] Screenshot del antes/después.

## 8. Lo que NO se cambia en este PR

- DTO `LaborSupplyDto` ni entidad `LaborSupply` (los campos ya existen).
- `WorkOrderQueryService` (el mapeo ya estaba bien).
- `LaborEditorForm` (la edición de Ha por insumo ya funciona).
- La tabla "Plan vs. Real" de la tab "Insumos Consolidados" (eso es Bug 1).
- La tabla de `Reportes.razor` (otra pantalla, otro bug si aplica).

---

## Notas para el agente

- Si encuentra que `LaborSupply.PlannedHectares` está siempre en 0 para labores nuevas, eso es un bug colateral en `LaborEditorForm` u otro punto de creación de labor. **No arreglar en este PR** — abrir issue separada.

- El usuario llama "Coef." a lo que el código llama "Dose". **No** renombrar la propiedad del DTO (`PlannedDose`/`RealDose`) — solo cambiar el `Title` de la columna en la UI. Cambiar la propiedad rompe `LaborEditorForm`, controllers, etc.
