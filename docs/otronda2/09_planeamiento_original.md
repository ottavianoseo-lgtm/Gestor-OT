# Sub-plan 09 — Módulo de Planeamiento Original

**Prioridad**: 🟢 BAJO  
**Área**: Nueva sección + cambios en dominio, API y frontend transversal

---

## PO-01: Concepto

El **Planeamiento Original** es el conjunto de labores que se crean al inicio de una campaña como línea de base. Son **inmutables una vez creadas**: no se pueden editar, eliminar, pasar a Realizadas ni cambiar ningún valor.

Son visibles en:
- La lista de Labores (con identificador visual distinto)
- El Work Planner
- Pueden sumarse a una OT (read-only dentro de ella)

Sirven de contraste para analizar desvíos al final de la campaña.

---

## PO-02: Cambios en el dominio

**`Labor.cs`** — agregar campo:
```csharp
public bool IsOriginalPlan { get; set; } = false;
```

**Migración EF**:
- `ALTER TABLE "Labors" ADD COLUMN "IsOriginalPlan" BOOLEAN NOT NULL DEFAULT FALSE;`

---

## PO-03: Backend — protección de labores de planeamiento original

**`LaborsController.cs`**:
1. En `PUT api/labors/{id}`:
```csharp
if (labor.IsOriginalPlan)
    return Conflict("Esta labor forma parte del Planeamiento Original y no puede modificarse.");
```
2. En `DELETE api/labors/{id}`: mismo chequeo.
3. En el endpoint de cambio de estado: mismo chequeo.

**Nuevo endpoint** para crear labores de planeamiento original:
```
POST api/labors/original-plan
```
Idéntico al de creación normal, pero setea `IsOriginalPlan = true`.

---

## PO-04: Frontend — nueva sección "Planeamiento Original"

**Nuevo archivo**: `src/GestorOT.Client/Pages/PlaneamientoOriginal.razor`

- URL: `/original-plan`
- Estructura idéntica a `LaboresSueltas.razor`.
- Solo muestra labores donde `IsOriginalPlan = true`.
- El botón "Nueva Labor" llama al endpoint `POST api/labors/original-plan`.
- **No hay botones de editar ni eliminar** en las filas.
- Banner informativo en el encabezado:
```razor
<Alert Type="@AlertType.Info"
       Message="Las labores del Planeamiento Original no pueden modificarse. Son la línea de base de la campaña."
       ShowIcon="true" Style="margin-bottom: 16px;" />
```

**Agregar al menú de navegación** (`MainLayout.razor` o `NavMenu`):
```razor
<MenuItem Key="original-plan" RouterLink="/original-plan">
    <Icon Type="book" /> Planeamiento Original
</MenuItem>
```

---

## PO-05: Identificador visual en lista general de Labores y Work Planner

**Archivos**: `LaboresSueltas.razor`, `WorkPlanner.razor`

**Qué hacer**:
1. En la columna "Labor" o en el card de cada labor, si `IsOriginalPlan = true`, agregar un ícono/badge:
```razor
@if (context.IsOriginalPlan)
{
    <Tag Color="gold" Style="font-size: 10px; margin-left: 6px;">BASE</Tag>
}
```
2. En el Work Planner, el bloque de la labor de planeamiento original puede tener un borde o fondo levemente distinto (ej: borde dorado/ámbar).

---

## PO-06: Permisos — rol con capacidad de reabrir planeamiento

> **Nota**: Este punto requiere que el sistema de roles esté implementado. Implementar solo si ya existe infraestructura de roles.

**Lógica**:
- Un rol especial (ej: `AdminCampaña`) puede llamar a `POST api/labors/{id}/unpin-original-plan` que setea `IsOriginalPlan = false`, dejando la labor editable nuevamente.
- Esta acción queda registrada en el `AuditLog`.
