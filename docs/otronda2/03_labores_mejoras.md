# Sub-plan 03 — Labores: Modal y Flujo

**Prioridad**: 🔴 ALTO  
**Área**: `src/GestorOT.Client/Components/LaborEditorForm.razor`, `src/GestorOT.Client/Pages/LaboresSueltas.razor`, `src/GestorOT.Client/Pages/WorkPlanner.razor`

---

## LAB-01: Unificar modal de Labor (mismo componente desde todos los accesos)

**Síntoma**: El modal de Labor se ve diferente si se abre desde Labores, desde OT, o desde WorkPlanner.

**Qué hacer**:
- Hay un único componente `LaborEditorForm.razor`. Verificar que **todos** los puntos de entrada lo usen:
  - `LaboresSueltas.razor`
  - `WorkOrderDetail.razor`
  - `WorkPlanner.razor`
- Si alguna página tiene su propio modal inline de Labor, reemplazarlo con `<LaborEditorForm>`.
- Parámetros del componente deben incluir: `LaborId` (nullable para creación), `WorkOrderId` (nullable), `OnSaved` (EventCallback), `OnCancelled` (EventCallback).

---

## LAB-02: Advertencia (no bloqueo) cuando Ha supera el área del lote

**Contexto**: Si se ingresa una cantidad de Ha mayor a las del área productiva del lote en la campaña, debe advertir pero dejar continuar.

**Archivos**: `LaborEditorForm.razor`, lógica de validación

**Qué hacer**:
1. Quitar la validación que impide guardar si `Hectares > CampaignLot.ProductiveArea`.
2. Reemplazar por:
```csharp
if (_formModel.Hectares > _campaignLotProductiveArea)
{
    bool confirmed = await Modal.ConfirmAsync(new ConfirmOptions
    {
        Title = "Área superior a la del lote",
        Content = "La cantidad de Ha ingresada supera las del área del lote. ¿Está seguro que desea continuar?",
        OkText = "Sí, continuar",
        CancelText = "No, revisar"
    });
    if (!confirmed) return;
}
// continúa con el guardado
```

---

## LAB-03: Filtrar selector de Lotes por Campo dentro de una Labor

**Contexto**: En producción habrá muchos lotes; necesita un pre-filtro por Campo.

**Archivos**: `LaborEditorForm.razor`

**Qué hacer**:
1. Agregar un `Select` de Campo **antes** del selector de Lote en el form:
```razor
<FormItem Label="Campo (filtro)">
    <Select TItem="FieldDto" TItemValue="Guid" DataSource="_fields"
            @bind-Value="_filterFieldId" OnSelectedItemChanged="OnFieldFilterChanged"
            LabelName="@nameof(FieldDto.Name)" ValueName="@nameof(FieldDto.Id)"
            Placeholder="Filtrar por campo..." AllowClear />
</FormItem>
<FormItem Label="Lote">
    <Select TItem="LotDto" TItemValue="Guid" DataSource="FilteredLots"
            @bind-Value="_formModel.LotId" ... />
</FormItem>
```
2. `FilteredLots` es una propiedad computed:
```csharp
private IEnumerable<LotDto> FilteredLots =>
    _filterFieldId == Guid.Empty
        ? _allLots
        : _allLots.Where(l => l.FieldId == _filterFieldId);
```
3. Al cambiar de campo, resetear el `LotId` seleccionado para evitar inconsistencias.

---

## LAB-04: Eliminar sección "Mezcla de Tanque" del modal de Labor

**Contexto**: Solo se deben mostrar Insumos; la Mezcla de Tanque se gestiona en otro módulo.

**Archivos**: `LaborEditorForm.razor`

**Qué hacer**:
- Localizar y eliminar el `<TabPane>` o sección con título "Mezcla de Tanque" o `TankMix` dentro del modal.
- No eliminar la entidad `TankMixRule` del dominio (puede usarse en otro contexto).

---

## LAB-05: Agregar adjuntar archivos al modal de Labor

**Contexto**: El modal de Labor no tiene opción de adjuntar archivos, aunque ya existe `LaborAttachments.razor`.

**Archivos**: `LaborEditorForm.razor`, `LaborAttachments.razor`

**Qué hacer**:
1. Verificar que `LaborAttachments.razor` funcione como componente embebible con parámetro `LaborId`.
2. Agregar al final del modal (o en un Tab "Adjuntos"):
```razor
@if (_isEditing && _editingLaborId != Guid.Empty)
{
    <LaborAttachments LaborId="_editingLaborId" />
}
```
3. En modo creación, mostrar un mensaje "Podés adjuntar archivos una vez guardada la labor."

---

## LAB-06: "Ejecutar Labor Planeada" debe abrir modal completo con estado Realizado

**Síntoma**: El botón de ejecutar solo abre un mini-modal de dosis reales.

**Archivos**: `LaboresSueltas.razor` o `WorkOrderDetail.razor` — botón "Ejecutar"

**Qué hacer**:
1. El botón "Ejecutar Labor" debe abrir `LaborEditorForm` con la labor pre-cargada.
2. Pre-setear `Status = LaborStatus.Realized` en el form.
3. Todos los campos deben ser editables (la fecha real puede haber cambiado, el responsable también, etc).
4. Al guardar, el sistema registra la labor como Realizada con todos los datos actualizados.

---

## LAB-07: Aviso de "Labores sin OT" al crear desde OT

**Contexto**: Si el usuario crea una labor desde dentro de una OT, pero ya existen labores sueltas sin OT, avisarle para evitar duplicados.

**Archivos**: `WorkOrderDetail.razor` — botón "Nueva Labor"

**Qué hacer**:
1. Al hacer clic en "Nueva Labor" dentro de una OT, hacer una llamada rápida a `GET api/labors?withoutOT=true&count=true`.
2. Si `count > 0`, mostrar un modal de confirmación:
```
"Tenés X labores creadas sin OT. ¿Qué querés hacer?"
[Crear labor nueva]  [Revisar labores sin OT]
```
3. Si elige "Revisar labores sin OT", abrir un modal con tabla filtrable (Planeadas / Realizadas) con opción de asignar esas labores a la OT actual.
4. Si elige "Crear labor nueva", flujo normal.

**Backend**: Agregar parámetro `?unassigned=true` al endpoint `GET api/labors` si no existe.
