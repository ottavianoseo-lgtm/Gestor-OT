# Prompt para DeepSeek V4Pro — Corrección de Estado Inicial y FK de Orden de Trabajo

## Rol

Eres **DeepSeek V4Pro**, especialista senior en **C# .NET 10, ASP.NET Core, EF Core, Blazor y diseño de estados de dominio**.

## Requisito obligatorio de Context7 MCP

Antes de tocar código, debes usar obligatoriamente el **MCP de Context7** para consultar documentación actualizada de:

- ASP.NET Core Web API controllers y model binding
- EF Core 10: relaciones FK, tracking, `AsNoTracking`, validaciones
- Blazor Forms y AntDesign Blazor `Select`, `Modal`, `Form`

Incluye al final una sección llamada **“Documentación consultada vía Context7”**.

## Rama y objetivo

Repositorio: `ottavianoseo-lgtm/Gestor-OT`  
Rama de trabajo: `fix/bugs-revision-gestor-ot`

Objetivo: corregir la creación/edición de Órdenes de Trabajo para que el estado seleccionado por el usuario quede consistente en `Status` y `WorkOrderStatusId`.

## Archivos a revisar obligatoriamente

- `src/GestorOT.Client/Pages/OrdenesTrabajos.razor`
- `src/GestorOT.Api/Controllers/WorkOrdersController.cs`
- `src/GestorOT.Api/Controllers/WorkOrderStatusesController.cs`
- `src/GestorOT.Domain/Entities/WorkOrder.cs`
- `src/GestorOT.Domain/Entities/WorkOrderStatus.cs`
- `src/GestorOT.Shared/Dtos/WorkOrderDto.cs`
- `src/GestorOT.Shared/Dtos/WorkOrderStatusDto.cs`
- `src/GestorOT.Infrastructure/Services/WorkOrderQueryService.cs`
- Tests en `src/GestorOT.Tests`

## Bug principal

En `WorkOrdersController.CreateWorkOrder`, se calcula `statusName` desde `dto.Status`, pero se asigna:

```csharp
WorkOrderStatusId = defaultStatus?.Id
```

Esto es incorrecto si el usuario selecciona un estado distinto del default.

## Riesgo funcional

- La columna string `Status` puede decir “InProgress” o un estado personalizado.
- La FK `WorkOrderStatusId` queda apuntando al estado default.
- Las reglas de edición/bloqueo que dependen de `WorkOrderStatus.IsEditable` pueden aplicarse mal.
- Una OT podría quedar editable o bloqueada incorrectamente.

## Implementación requerida

1. Al crear OT:
   - Si `dto.Status` viene informado, buscar `WorkOrderStatus` por `Name == dto.Status` dentro del tenant actual.
   - Si no viene informado, usar el estado default.
   - Si no existe estado con ese nombre y no hay default, usar fallback seguro `Draft` solo si el dominio lo permite.
   - Setear ambos campos de forma consistente:
     - `workOrder.Status = selectedStatus.Name`
     - `workOrder.WorkOrderStatusId = selectedStatus.Id`
2. Al editar OT:
   - Si cambia `dto.Status`, actualizar también `WorkOrderStatusId` al estado correspondiente.
   - Si se envía estado inválido, devolver `400 BadRequest` con mensaje claro.
3. Revisar `WorkOrderDto`:
   - Evaluar si conviene transportar `WorkOrderStatusId` además de `Status` string para evitar ambigüedad.
   - Si se agrega DTO, mantener compatibilidad con UI existente.
4. Revisar `OrdenesTrabajos.razor`:
   - El `Select` debe usar estados reales de `api/workorder-statuses`.
   - Si no hay estados, fallback controlado.
   - Revisar que `LoadData` no intente deserializar `api/campaigns` como `ListCampaignSummaryDto` si el endpoint devuelve `CampaignDto`. Si `_availableCampaigns` no se usa, eliminarlo para evitar ruido.

## Criterios de aceptación funcional

- Crear OT con estado default: `Status` y `WorkOrderStatusId` apuntan al default.
- Crear OT seleccionando estado no default: `Status` y `WorkOrderStatusId` apuntan al estado elegido.
- Crear OT con estado inválido: responde 400 con mensaje claro.
- Editar OT y cambiar estado: actualiza string y FK.
- Una OT en estado no editable no puede modificarse si `WorkOrderStatus.IsEditable == false`.
- Botón “Nueva Orden” abre modal si hay campaña activa seleccionada y no bloqueada.
- Si no hay campaña seleccionada, no abre modal y muestra advertencia.

## Tests obligatorios

Agregar tests para:

1. `CreateWorkOrder_UsesDefaultStatus_WhenStatusMissing`.
2. `CreateWorkOrder_UsesSelectedStatus_WhenStatusProvided`.
3. `CreateWorkOrder_ReturnsBadRequest_WhenStatusInvalid`.
4. `UpdateWorkOrder_UpdatesStatusAndWorkOrderStatusId`.
5. `UpdateWorkOrder_Blocked_WhenCurrentStatusIsNotEditable`.
6. `CreateWorkOrder_Blocked_WhenCampaignLocked`.

## Restricciones técnicas

- No duplicar estados hardcodeados si existe tabla `WorkOrderStatuses`.
- No usar strings mágicos dispersos; centralizar fallback si es necesario.
- Mantener queries bajo filtro tenant.
- No usar `FindAsync` si puede saltar filtros de tenant en entidades sensibles.

## Entregable esperado

- Código corregido.
- Tests agregados.
- `dotnet build` y `dotnet test` ejecutados.
- Informe breve con archivos modificados y documentación consultada vía Context7.
