# Sub-plan 06 — Bloqueo de OT y Labores por Estado

**Prioridad**: 🟡 MEDIO  
**Área**: `src/GestorOT.Domain/Entities/WorkOrderStatus.cs`, `src/GestorOT.Api/Controllers/WorkOrdersController.cs`, `src/GestorOT.Client/Pages/WorkOrderDetail.razor`

---

## LOCK-01: Estados de OT que bloquean edición

**Contexto**: Ya existe la configuración de "Estado que bloquea", pero no está implementada en el servidor ni en el cliente.

**Entidad de dominio** — `WorkOrderStatus.cs`:
```csharp
public bool BlocksEditing { get; set; } // ya debe existir o agregar
```

**Backend** — `WorkOrdersController.cs`:
1. En el endpoint `PUT api/workorders/{id}`, antes de aplicar cambios:
```csharp
var current = await _db.WorkOrders
    .Include(w => w.WorkOrderStatus)
    .FirstOrDefaultAsync(w => w.Id == id);

if (current?.WorkOrderStatus?.BlocksEditing == true)
    return Conflict("La OT se encuentra en un estado que no permite modificaciones.");
```
2. Lo mismo en el endpoint `PUT api/labors/{id}` y `DELETE api/labors/{id}`:
```csharp
// Verificar que la OT asociada no esté bloqueada
var labor = await _db.Labors.Include(l => l.WorkOrder)
    .ThenInclude(wo => wo.WorkOrderStatus)
    .FirstOrDefaultAsync(l => l.Id == id);

if (labor?.WorkOrder?.WorkOrderStatus?.BlocksEditing == true)
    return Conflict("La labor pertenece a una OT bloqueada.");
```

**Frontend** — `WorkOrderDetail.razor`:
1. Cargar el DTO de la OT que incluya `IsLocked` (derivado de `WorkOrderStatus.BlocksEditing`).
2. Si `IsLocked = true`:
   - Deshabilitar botones de editar/eliminar en labores.
   - Deshabilitar el botón "Editar OT".
   - Mostrar un banner informativo:
```razor
@if (_workOrder.IsLocked)
{
    <Alert Type="@AlertType.Warning"
           Message="Esta OT está en un estado que no permite modificaciones."
           ShowIcon="true" Style="margin-bottom: 16px;" />
}
```

**DTO**:
- Agregar `bool IsLocked` a `WorkOrderDto`, calculado en el mapping a partir del estado asociado.

---

## LOCK-02: Desbloqueo de Campañas

**Contexto**: Existe la opción de bloquear una Campaña pero no de desbloquearla.

**Archivos**: `Campanias.razor`

**Qué hacer**:
1. En la lista de campañas, donde aparece el botón "Bloquear", mostrar condicionalmente "Desbloquear" si ya está bloqueada:
```razor
@if (context.Status == CampaignStatus.Locked)
{
    <Button Size="@ButtonSize.Small" OnClick="() => UnlockCampaign(context.Id)"
            Style="color: #2ECC71; border-color: #2ECC71;">
        <Icon Type="unlock" /> Desbloquear
    </Button>
}
else
{
    <Button Size="@ButtonSize.Small" OnClick="() => LockCampaign(context.Id)"
            Style="color: #E74C3C; border-color: #E74C3C;">
        <Icon Type="lock" /> Bloquear
    </Button>
}
```
2. Agregar endpoint `POST api/campaigns/{id}/unlock` en `CampaignsController.cs` que cambie el estado de vuelta a `Active`.
