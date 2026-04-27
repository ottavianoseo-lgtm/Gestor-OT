# Sub-plan 05 — Work Planner: Filtros y Consistencia

**Prioridad**: 🟡 MEDIO  
**Área**: `src/GestorOT.Client/Pages/WorkPlanner.razor`

---

## WP-01: Eliminar botones circulares de colores de tipo Labor (no funcionales)

**Síntoma**: Existen "botones" con círculos de colores para tipos de labor que no cumplen ninguna función.

**Archivos**: `WorkPlanner.razor`

**Qué hacer**:
1. Localizar el bloque que renderiza esos indicadores (buscar clases como `labor-type-dot`, `color-badge` o similar).
2. Eliminarlo del markup.
3. Verificar que no haya lógica en `@code` que dependa de esos elementos (si la hay, también eliminarla).

---

## WP-02: Agregar filtro por "Actividad" al lado del filtro de tipo de Labor

**Contexto**: Ya existe un filtro desplegable por tipo de Labor. Agregar uno por Actividad (propiedad `ErpActivityId` en `Labor`).

**Archivos**: `WorkPlanner.razor`

**Qué hacer**:
1. Cargar actividades al inicializar:
```csharp
_activities = await Http.GetFromJsonAsync<List<ErpActivityDto>>("api/catalogs/activities", ...) ?? new();
```
2. Agregar selector al toolbar del Work Planner:
```razor
<Select TItem="ErpActivityDto" TItemValue="Guid?"
        DataSource="_activities"
        @bind-Value="_filterActivityId"
        LabelName="@nameof(ErpActivityDto.Name)"
        ValueName="@nameof(ErpActivityDto.Id)"
        Placeholder="Filtrar por Actividad"
        AllowClear
        Style="width: 180px;" />
```
3. El filtro de Tipo de Labor y el de Actividad son combinables (AND lógico):
```csharp
private IEnumerable<LaborDto> FilteredLabors => _labors
    .Where(l => _filterLaborTypeId == null || l.LaborTypeId == _filterLaborTypeId)
    .Where(l => _filterActivityId == null || l.ErpActivityId == _filterActivityId);
```

---

## WP-03: Modal de Labor consistente con el resto de la app

**Síntoma**: El modal de Labor que se abre desde WorkPlanner es distinto al de Labores o al de OT.

**Qué hacer**:
- Ver `LAB-01` en `03_labores_mejoras.md`.
- Reemplazar cualquier modal inline en `WorkPlanner.razor` con el componente `<LaborEditorForm>`.
