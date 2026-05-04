# Sprint 03 - Órdenes de Trabajo

## Objetivo

Corregir la creación de OT y asegurar que toda OT pertenezca a una campaña válida. La creación de OT no debe depender de un estado roto del selector de campañas.

## Rama sugerida

`fix/s03-ordenes-trabajo`

## Bugs cubiertos

- Botón `Nueva Orden` no abre modal.
- OT puede quedar sin campaña si el frontend falla.
- La UI no explica correctamente por qué no se puede crear.
- La OT no debe pedir campo a nivel encabezado.
- Debe respetar múltiples personas / múltiples fechas.

## Archivos principales

- `src/GestorOT.Client/Pages/OrdenesTrabajos.razor`
- `src/GestorOT.Api/Controllers/WorkOrdersController.cs`
- `src/GestorOT.Domain/Entities/WorkOrder.cs`
- `src/GestorOT.Shared/Dtos/WorkOrderDto.cs`
- `src/GestorOT.Client/Services/CampaignState.cs`
- `src/GestorOT.Api/Controllers/WorkOrderStatusesController.cs`
- `src/GestorOT.Shared/Dtos/WorkOrderStatusDto.cs`

## Regla funcional

Una OT es un encabezado operativo dentro de una campaña. Puede agrupar labores. No debe depender de un campo único obligatorio porque una OT puede contener labores de distintos lotes/campos.

## Tareas técnicas

### 1. Apertura de modal

1. Si no hay campaña seleccionada:
   - no abrir modal;
   - mostrar alerta persistente en la página;
   - el mensaje debe indicar "Seleccione una campaña activa para crear una OT".
2. Si hay campaña seleccionada activa:
   - abrir modal.
3. Si hay campaña seleccionada bloqueada:
   - no abrir modal;
   - mostrar mensaje "La campaña está bloqueada. Solo consulta".

### 2. Formulario

1. No agregar selector de campo.
2. Mantener:
   - nombre opcional;
   - descripción;
   - responsable/contacto;
   - fecha;
   - estado;
   - múltiples personas;
   - múltiples fechas.
3. Si `AcceptsMultiplePeople = false`:
   - exigir responsable/contacto;
   - exigir tipo Propio/Contratista si el modelo lo soporta.
4. Si `AcceptsMultipleDates = false`:
   - exigir fecha de encabezado.
5. Estado inicial:
   - tomar estado configurado por backend;
   - fallback: `Draft`.

### 3. Backend

En `WorkOrdersController.CreateWorkOrder`:

1. Rechazar `CampaignId == null` o `Guid.Empty`.
2. Validar que campaña exista.
3. Validar que campaña no esté bloqueada.
4. Validar estado inicial.
5. Guardar `CampaignId`.
6. No exigir `FieldId`.

### 4. Edición

1. Si la campaña de la OT está bloqueada, no permitir edición.
2. Si el estado de OT no permite edición, no permitir edición.
3. Mensajes de error claros.

## No hacer en este sprint

- No implementar creación de labores desde estrategia.
- No rediseñar detalle de OT.
- No tocar adjuntos.
- No tocar Planeamiento Original.

## Pruebas manuales

### Caso 1 - Crear OT con campaña activa

1. Seleccionar campaña activa.
2. Ir a Órdenes de Trabajo.
3. Click `Nueva Orden`.

Resultado esperado:

- Modal abre.
- No aparece selector de campo.
- `CampaignId` se asigna.
- Guardar crea OT.

### Caso 2 - Sin campaña

1. Limpiar campaña.
2. Click `Nueva Orden`.

Resultado esperado:

- No abre modal.
- Muestra alerta persistente.

### Caso 3 - Campaña bloqueada

1. Seleccionar campaña bloqueada.
2. Click `Nueva Orden`.

Resultado esperado:

- No abre modal.
- Mensaje de solo lectura.

### Caso 4 - Backend protegido

1. Enviar request directo sin `CampaignId`.

Resultado esperado:

- API devuelve error funcional.

## Criterios de aceptación

- `Nueva Orden` abre con campaña activa.
- Backend no permite OT sin campaña.
- Campaña bloqueada bloquea creación.
- No se pide campo a nivel OT.
- El código compila.

## Prompt corto para DeepSeek

Implementá solo Sprint 03. Corregí creación de OT: el modal debe abrir con campaña activa, no debe abrir sin campaña o con campaña bloqueada, no debe pedir campo y el backend debe rechazar cualquier OT sin campaña válida. No toques estrategias ni labores salvo lo mínimo para compilar.
