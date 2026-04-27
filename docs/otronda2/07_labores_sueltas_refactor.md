# Sub-plan 07 — LaboresSueltas: Refactor a "Todas las Labores"

**Prioridad**: 🟡 MEDIO  
**Área**: `src/GestorOT.Client/Pages/LaboresSueltas.razor`, `src/GestorOT.Api/Controllers/LaborsController.cs`

---

## LS-01: Mostrar todas las Labores (no solo las sin OT) con filtros

**Contexto**: La sección debe mostrar todas las labores. "Asignadas a OT" y "Sin asignar" son filtros, no el criterio de carga.

**Backend** — `LaborsController.cs`:
1. El endpoint `GET api/labors` debe devolver todas las labores de la campaña activa.
2. Agregar parámetros opcionales de filtro:
   - `?assigned=true` → solo las que tienen `WorkOrderId != null`
   - `?assigned=false` → solo las que tienen `WorkOrderId == null`
   - `?status=Planned|Realized` → filtro por estado

**Frontend** — `LaboresSueltas.razor`**:
1. Renombrar la página: cambiar título de "Labores Pendientes de Asignar" a "Labores".
2. Cambiar `page-subtitle` a "Todas las labores de la campaña".
3. Agregar filtros en el toolbar:
```razor
<Select TItemValue="string?" TItem="string" @bind-Value="_filterAssigned"
        Placeholder="Asignación" AllowClear Style="width: 160px;">
    <SelectOption Value="@("assigned")" Label="Asignadas a OT" />
    <SelectOption Value="@("unassigned")" Label="Sin OT" />
</Select>
<Select TItemValue="string?" TItem="string" @bind-Value="_filterStatus"
        Placeholder="Estado" AllowClear Style="width: 140px;">
    <SelectOption Value="@("Planned")" Label="Planeadas" />
    <SelectOption Value="@("Realized")" Label="Realizadas" />
</Select>
```
4. Los KPIs del encabezado se actualizan para reflejar totales globales (no solo sin OT).

---

## LS-02: Tabla de Labores como tabla plana (no cards)

**Contexto**: La tabla con atributos en columna es más fácil de leer y ordenar.

**Qué hacer**:
1. Si actualmente `LaboresSueltas` usa cards, reemplazar con `<Table TItem="LaborDto">` de AntDesign.
2. Columnas requeridas en orden: **Labor** (tipo), **Fecha**, **Lote**, **Campo**, **Responsable**, **Ha**, **Estado**.
3. Todas las columnas con `Sortable`.
4. La columna Estado usa el mismo badge de color ya usado en OrdenesTrabajos.
