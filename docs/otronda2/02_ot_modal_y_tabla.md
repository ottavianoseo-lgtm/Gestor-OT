# Sub-plan 02 — OT: Modal de Creación y Tabla

**Prioridad**: 🔴 ALTO  
**Área**: `src/GestorOT.Client/Pages/OrdenesTrabajos.razor`

---

## OT-01: Descripción no obligatoria y eliminar selector de Campo

**Contexto**: La info del campo/lote pertenece a cada Labor individual. Una OT puede agrupar N labores en distintos campos.

**Cambios en `OrdenesTrabajos.razor`**:
1. En `WoFormModel`, quitar el `[Required]` de `Description`.
2. Quitar la validación manual `if (string.IsNullOrWhiteSpace(_formModel.Description))` en `SaveWorkOrder()`.
3. Eliminar el `<FormItem Label="Campo">` con su `<Select>` de campos del modal.
4. Quitar `FieldId` de `WoFormModel` y del DTO enviado al crear (o enviar `Guid.Empty`/null y manejar en backend).

**Cambios en `WorkOrdersController.cs` / dominio**:
- Si `FieldId` era requerido en la entidad `WorkOrder`, hacerlo nullable (`Guid? FieldId`).
- Actualizar la migración de EF si corresponde.

---

## OT-02: Renombrar "Fecha Límite" → "Fecha" en el modal

**Cambios en `OrdenesTrabajos.razor`**:
```razor
<!-- Antes -->
<FormItem Label="Fecha Límite">
<!-- Después -->
<FormItem Label="Fecha">
```
Actualizar también el título de columna en la tabla:
```razor
<!-- Antes -->
<PropertyColumn Property="c => c.DueDate" Title="Fecha Límite" Sortable>
<!-- Después -->
<PropertyColumn Property="c => c.DueDate" Title="Fecha" Sortable>
```

---

## OT-03: Lógica de "Múltiples Personas" y "Múltiples Fechas"

**Comportamiento esperado**:
- Si `AcceptsMultiplePeople = true` → ocultar/deshabilitar el campo "Responsable" (vivirá en cada Labor).
- Si `AcceptsMultipleDates = true` → ocultar/deshabilitar el campo "Fecha" (vivirá en cada Labor).

**Cambios en `OrdenesTrabajos.razor`**:
```razor
@if (!_formModel.AcceptsMultiplePeople)
{
    <FormItem Label="Responsable">
        <Select ... @bind-Value="_formModel.ContactId" ... />
    </FormItem>
}
@if (!_formModel.AcceptsMultipleDates)
{
    <FormItem Label="Fecha">
        <DatePicker @bind-Value="_formModel.DueDate" ... />
    </FormItem>
}
```
Los checkboxes deben disparar `StateHasChanged()` al cambiar para que el form se re-renderice.

---

## OT-04: Agregar columna "Nombre" al inicio de la tabla de OTs

**Contexto**: Actualmente la tabla arranca con "Descripción". El nombre (auto-generado o manual) debe estar al principio.

**Cambios en `OrdenesTrabajos.razor`**:
- Agregar antes de la columna `Descripción`:
```razor
<PropertyColumn Property="c => c.Name" Title="Nombre" Sortable>
    <span style="font-weight: 700; color: #fff;">@(context.Name ?? context.OTNumber)</span>
</PropertyColumn>
```
- Verificar que `WorkOrderDto` exponga el campo `Name` y `OTNumber`.

---

## OT-05: Estado de OT no se refleja correctamente en el modal de edición

**Síntoma**: Al abrir una OT existente para editar, el selector de Estado no muestra el estado actual.

**Archivos a revisar**: `OrdenesTrabajos.razor` → `OpenEditModal()`

**Qué hacer**:
1. En `OpenEditModal`, asegurarse de que `_formModel.Status = wo.Status` esté seteado.
2. El `<Select>` del estado usa `ValueName="@nameof(WorkOrderStatusDto.Name)"` y `@bind-Value="_formModel.Status"`. Verificar que el valor del DTO coincida con los `Name` de `_workOrderStatuses`.
3. Si los estados vienen como IDs en el DTO pero como nombres en el select, alinear la comparación.
