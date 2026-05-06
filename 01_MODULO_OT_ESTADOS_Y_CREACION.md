# Módulo 01 — Estados dinámicos y creación de Órdenes de Trabajo

Bugs incluidos: BUG-15 y BUG-16.

## Uso obligatorio de MCP Context7

Antes de editar código en este módulo, el agente debe consultar Context7 y registrar en su reporte final qué documentación usó.

Consultas mínimas obligatorias:

```text
/context7 .NET 10 ASP.NET Core Blazor
/context7 Entity Framework Core 10
/context7 AntDesign Blazor
```

Si el módulo toca GIS, agregar:

```text
/context7 Leaflet
/context7 Leaflet.draw
/context7 Blazor JS Interop
```

Si el agente no puede usar Context7, debe detener la implementación del módulo y reportar el bloqueo. No debe improvisar cambios de APIs, componentes ni patrones.


## Objetivo funcional

El usuario debe poder crear una OT completa usando estados configurados en la pantalla de Estados de OT. El sistema no debe depender de strings hardcodeados como `Draft`, `Pending`, `InProgress`, `Completed` o `Approved`.

## Archivos a revisar

- `src/GestorOT.Client/Pages/OrdenesTrabajos.razor`
- `src/GestorOT.Api/Controllers/WorkOrdersController.cs`
- `src/GestorOT.Api/Controllers/WorkOrderStatusesController.cs`
- `src/GestorOT.Domain/Entities/WorkOrder.cs`
- `src/GestorOT.Shared/Dtos/WorkOrderDto.cs`
- `src/GestorOT.Infrastructure/Services/WorkOrderQueryService.cs`
- Configuraciones EF y migraciones relacionadas a `WorkOrderStatusId`.

## Problemas detectados

1. El modal de OT carga estados desde `api/workorder-statuses`, pero el modelo del formulario usa `Status` string.
2. Si no hay estados o falla la carga, la UI vuelve a mostrar estados fijos.
3. Los KPIs de la pantalla cuentan estados por strings fijos.
4. Los badges visuales también dependen de strings fijos.
5. El backend crea OTs resolviendo por `dto.Status` y fallback a `Draft`.
6. La creación falla si el estado configurado por usuario no coincide con esos strings.

## Implementación requerida

### Frontend

1. Cambiar `WoFormModel.Status` por `Guid? WorkOrderStatusId`.
2. El selector de estado debe usar `WorkOrderStatusDto.Id` como `ValueName`.
3. Mostrar `Name` solo como label.
4. Al abrir modal de creación, seleccionar `IsDefault == true`. Si no existe, seleccionar el primer estado por `SortOrder`.
5. Si no hay estados configurados, mostrar error claro: “No hay estados de OT configurados. Cree al menos un estado inicial.”
6. Eliminar fallback UI a `Draft`, `Pending`, `InProgress`.
7. Actualizar badges para usar `WorkOrderStatusId`, `StatusName` o `Status` solo si llega desde el backend como display.
8. Los KPIs deben calcularse con metadata dinámica. Si no hay categorías funcionales, mostrar KPIs genéricos: Total, Editables, No editables, Sin estado.
9. Crear OT debe enviar `WorkOrderStatusId`.

### Backend

1. En `CreateWorkOrder`, resolver estado primero por `dto.WorkOrderStatusId`.
2. Si no llega `WorkOrderStatusId`, buscar estado `IsDefault`.
3. Si no hay estado válido, devolver `BadRequest` funcional.
4. No usar fallback obligatorio a `Draft`.
5. Persistir `WorkOrderStatusId` y copiar `Status = finalStatus.Name` solo por compatibilidad/display.
6. En `UpdateWorkOrder`, permitir cambio de estado por `WorkOrderStatusId`.
7. Mantener validación de campaña bloqueada.
8. Revisar aprobación: no comparar `Status == "Approved"` ni `Status == "Cancelled"` si esos estados son configurables. Usar flags/configuración o transitions si existen.

## Criterios de aceptación

- El dropdown de estado muestra únicamente estados creados por el usuario.
- No aparecen `Draft`, `Pending` o `InProgress` salvo que el usuario los haya creado.
- Se puede crear una OT con estado dinámico.
- El POST no falla por validar strings hardcodeados.
- La OT creada queda con `WorkOrderStatusId`.
- El listado muestra nombre y color configurados.
- Si no hay estados configurados, el usuario recibe un error accionable.
- No se rompe edición ni eliminación de OTs con estado no editable.

## Pruebas manuales

1. Crear estados: “Nueva”, “En ejecución”, “Cerrada”. Marcar “Nueva” como default.
2. Abrir Nueva OT.
3. Confirmar que el selector muestra esos estados, no los genéricos.
4. Crear OT con “Nueva”.
5. Editar OT y pasar a “En ejecución”.
6. Crear estado no editable y asignarlo a una OT.
7. Verificar que la OT no pueda modificarse si `IsEditable == false`.
8. Borrar o desactivar estados genéricos y repetir creación.
9. Probar campaña bloqueada: no debe permitir crear OT.

## Riesgos

- Si otros módulos esperan strings `Approved`/`Cancelled`, deben migrarse gradualmente.
- Si el DTO no expone nombre/color del estado, puede requerir ajuste en query service.
- Si hay datos existentes con `Status` pero sin `WorkOrderStatusId`, crear migración o script de normalización.

## Reporte final obligatorio

El agente debe informar:

- Archivos modificados.
- Estados consultados desde Context7.
- Decisión sobre compatibilidad `Status` vs `WorkOrderStatusId`.
- Pruebas ejecutadas.
- Casos no cubiertos.
