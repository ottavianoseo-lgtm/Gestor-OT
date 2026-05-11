# Bug #4 — UX del panel de labores se rompe al seleccionar una o más labores

> Módulo: **UI / Labores Sueltas**
> Criticidad: **Media** (no bloquea funcionalidad, deteriora experiencia) · **Riesgo de fix: muy bajo** (CSS puro)
> Estimación: **1-2h** incluyendo verificación responsive
> PR: chico, sin tocar lógica

---

## 1. Causa raíz

El header de la página de labores está armado con un único `<div>` flex sin `flex-wrap`. Cuando el usuario selecciona una o más labores, aparecen **dos botones adicionales** dinámicamente ("Asignar X a OT" y "Crear OT con X seleccionadas") que **no caben en una sola línea** junto al resto de los controles existentes (5 selects de filtro + tag de campaña + botón refresh + botón "Labor Rápida"). El contenido desborda, se solapa o empuja a su contenedor según el viewport.

**Evidencia concreta:**

`src/GestorOT.Client/Pages/LaboresSueltas.razor` línea 25:
```html
<div style="display: flex; gap: 8px; align-items: center;">
```
Sin `flex-wrap: wrap`. Todo lo siguiente queda en una sola fila.

Líneas 26-49: cinco `<Select>` + `<Button Icon="sync">` + tag de campaña (líneas 51-60) + botón "Labor Rápida" (líneas 61-64).

Líneas 65-75: bloque condicional que **se agrega** cuando `_selectedIds.Count > 0`:
```razor
@if (_selectedIds.Count > 0)
{
    <Button ...>Asignar @_selectedIds.Count a OT</Button>
    <Button ...>Crear OT con @_selectedIds.Count seleccionadas</Button>
}
```

Cada uno de los dos botones nuevos tiene icono + texto + número embebido — son anchos significativos. Sumados al header preexistente no caben en ninguna pantalla de menos de ~1900px sin desbordar.

## 2. Pre-lectura obligatoria con context7 (MCP)

1. `context7` — CSS Flexbox: `flex-wrap`, `min-width: 0` en hijos, comportamiento con `gap` cuando hay wrap.
2. `context7` — AntDesign Blazor (versión del repo): `Button` size variants, `Tag`, `Select` ancho default, contenedor recomendado para barras de acción dinámicas (`Space` vs `<div>` plano).
3. `context7` — Blazor: re-render diff cuando se monta/desmonta un bloque `@if` dentro de un flex container — confirmar que no hace falta `key` (no debería para nodos simples).

## 3. Plan de implementación

Tres opciones, ordenadas de menos a más invasiva. El plan recomienda la **A**, deja la B como respaldo si A no luce bien con ≥10 labores seleccionadas, y la C como camino largo si el equipo decide refactorizar el header.

### 3.1. Opción A (recomendada) — `flex-wrap` + agrupar acciones de selección

**Archivo:** `src/GestorOT.Client/Pages/LaboresSueltas.razor`

**Cambio 1** (línea 25): agregar `flex-wrap: wrap; row-gap: 8px;`:
```html
<div style="display: flex; gap: 8px; align-items: center; flex-wrap: wrap; row-gap: 8px;">
```

**Cambio 2** (líneas 65-75): mover los dos botones de selección **fuera** del header, a una **barra contextual** propia que aparezca debajo del título cuando hay selección. Esto separa lo permanente (filtros) de lo contextual (acciones sobre selección), y elimina la presión sobre el header.

Insertar **después** de la línea 77 (`</div>` que cierra el `page-header`) y **antes** del Alert de error (línea 79):

```razor
@if (_selectedIds.Count > 0)
{
    <div class="selection-action-bar">
        <span class="selection-count">
            <Icon Type="check-circle" Theme="IconThemeType.Fill" Style="color: #2ECC71;" />
            @_selectedIds.Count labor@(_selectedIds.Count == 1 ? "" : "es") seleccionada@(_selectedIds.Count == 1 ? "" : "s")
        </span>
        <div class="selection-actions">
            <Button Size="@ButtonSize.Small" OnClick="ClearSelection">Limpiar</Button>
            <Button Type="@ButtonType.Primary" OnClick="OpenAssignModal"
                    Style="background: #3498DB; border-color: #3498DB;">
                <Icon Type="link" Theme="IconThemeType.Outline" /> Asignar a OT
            </Button>
            <Button Type="@ButtonType.Primary" OnClick="CreateOTFromSelected"
                    Style="background: #E74C3C; border-color: #E74C3C;">
                <Icon Type="file-add" Theme="IconThemeType.Outline" /> Crear OT con la selección
            </Button>
        </div>
    </div>
}
```

Y **borrar** el bloque `@if (_selectedIds.Count > 0) { ... }` original (líneas 65-75).

**Cambio 3** (estilos): agregar al bloque `<style>` (alrededor de la línea 174 en adelante):

```css
.selection-action-bar {
    display: flex;
    justify-content: space-between;
    align-items: center;
    gap: 16px;
    flex-wrap: wrap;
    padding: 10px 16px;
    margin-bottom: 16px;
    background: rgba(52, 152, 219, 0.08);
    border: 1px solid rgba(52, 152, 219, 0.25);
    border-radius: 10px;
}

.selection-action-bar .selection-count {
    color: #fff;
    font-weight: 600;
    display: flex;
    align-items: center;
    gap: 8px;
}

.selection-action-bar .selection-actions {
    display: flex;
    gap: 8px;
    flex-wrap: wrap;
}
```

**Cambio 4** (código C#): agregar al bloque `@code` un método `ClearSelection`:

```csharp
private void ClearSelection()
{
    _selectedIds.Clear();
    StateHasChanged();
}
```

### 3.2. Opción B (alternativa si A se ve sobrecargada) — Dropdown de acciones

Reemplazar los dos botones de la barra contextual por un único `<Dropdown>` "Acciones (X)" con dos opciones internas. Menos espacio, una sola fila incluso con 50 labores seleccionadas. Costo: un click más por acción.

### 3.3. Opción C (no recomendada ahora) — refactorizar el header

Reescribir `page-header` como un grid de dos filas: fila 1 título + selector de campaña + búsqueda; fila 2 filtros + ordenamiento. Pero implica cambios en `app.css` global y otras páginas que comparten la clase `page-header` (verificar con grep). **Fuera de scope de este bug.**

### 3.4. Confirmar que no afecta otras páginas

`grep -rn "page-header" src/GestorOT.Client/` mostrará otras páginas que usan la misma clase. **No se modifica `page-header` global**, solo el `<div>` interno con estilos inline. Por lo tanto otras páginas no se ven afectadas.

## 4. Tests

CSS es difícil de testear sin Playwright/bUnit. Plan:

1. **No** introducir framework de tests visuales en este PR.
2. **Sí** verificar visualmente en tres viewports: 1366×768 (laptop chica común), 1920×1080 (desktop), 1024×768 (tablet horizontal).
3. **Sí** mantener los tests existentes verdes (`dotnet test` sin regresiones — la lógica C# casi no se toca).
4. Si en el futuro se introduce bUnit, agregar un test que renderice `LaboresSueltas.razor` con `_selectedIds.Count = 3` y assert que la barra de selección está presente como elemento distinto del header. Anotar en backlog.

## 5. Smoke test manual

En cada uno de los tres viewports indicados:

1. Ir a `Labores Sueltas`. Header completo visible, una sola fila (o con wrap suave si el viewport es muy estrecho).
2. **Sin selección**: no debe aparecer la barra azul de selección.
3. Seleccionar 1 labor → aparece barra azul con texto "1 labor seleccionada" + botones Asignar / Crear OT / Limpiar. Header **no cambia**.
4. Seleccionar 5 labores → texto "5 labores seleccionadas". Header igual.
5. Seleccionar 50 labores → la barra sigue rendereando bien (con wrap si es necesario).
6. Click en "Limpiar" → barra desaparece, header vuelve a estado original.
7. Click en "Asignar a OT" → modal abre como antes (funcionalidad intacta).
8. Click en "Crear OT con la selección" → flujo funciona como antes.
9. **Regresión navegación**: ir a `OT > Detalle > volver a Labores Sueltas` con `?targetOT=...` (la página soporta este flujo en líneas 84-97). Verificar que el banner de "Modo Asignación" sigue mostrándose correctamente — **no debe colisionar** con la nueva barra de selección.
10. **Regresión campaña**: cambiar de campaña → la página recarga datos, no quedan selecciones residuales.

## 6. Definition of Done específica

- [ ] `flex-wrap: wrap` agregado al div header de `LaboresSueltas.razor`.
- [ ] Barra contextual de selección extraída del header (opción A).
- [ ] Estilos `.selection-action-bar` agregados.
- [ ] Método `ClearSelection()` agregado y wired al botón "Limpiar".
- [ ] Bloque condicional original (líneas 65-75) eliminado.
- [ ] Smoke test manual en 3 viewports completado.
- [ ] `dotnet test` sin regresiones.
- [ ] PR description lista las consultas a `context7`.
- [ ] Screenshot del antes/después en la descripción del PR (no obligatorio para CI pero exigido para review).

## 7. Lo que NO se cambia en este PR

- La lógica de selección (`OnSelectedRowsChanged`, `_selectedIds`, `ToggleSelect`).
- El componente `<Table>` de AntDesign ni sus props.
- El modal "Asignar a OT" ni el flujo de creación de OT desde labores.
- La clase global `page-header` ni el CSS de otras páginas.
- El banner de "Modo Asignación" (líneas 84-97 actuales).
- Otros bugs de UX del repo (decimales, botones superpuestos en otras pantallas, etc.) — issues separadas.

---

## Nota sobre `WorkPlanner.razor`

Si bien el usuario reporta el bug describiéndolo como "panel de labores" y existe también `WorkPlanner.razor` con un header similar (línea 17-22), **el reporte original adjunta capturas del listado de labores sueltas**. Tras inspeccionar `WorkPlanner`, su header **no** agrega botones dinámicos al seleccionar, así que no padece el mismo problema.

Si durante el smoke test se descubre que `WorkPlanner` también tiene un issue de wrap, **abrir un issue separado**, no incluir el fix acá.
