Sub-Plan 03 — Módulo de Labores: Backend
Sprint 2 — Medio  ·  Semana 2  ·  Prioridad: ALTA

Este sub-plan cubre todos los cambios de API necesarios para el módulo de labores. Requiere que el Sub-Plan 02 esté completado y la migración aplicada.

⚠ Prerequisito
Sub-Plan 02 debe estar completado: LaborStatus enum creado, campos Priority y SupplyWithdrawalNotes en DB, LaborDto actualizado.

#	Tarea	Archivo	Est.
1	Endpoint submit-for-validation + Magic Link	LaborsController.cs, ShareController.cs	6h
2	Endpoint PATCH priority para labores sueltas	LaborsController.cs	1h
3	GET unassigned con sortBy query param	LaborsController.cs	1h
4	Labor ejecutada sin planificación	LaborsController.cs	2h
5	MapToDto — agregar nuevos campos	LaborsController.cs	0.5h


Tarea 1 — Endpoint Submit-for-Validation + Magic Link
Nuevo endpoint: POST api/labors/{id}/submit-for-validation
Flujo completo del proceso de validación:
•	1. Verificar que Mode = Planned y Status = Planned. Rechazar con 400 si no cumple.
•	2. Cambiar Status a AwaitingValidation.
•	3. Generar token en tabla SharedTokens con metadata: { laborId, action: 'validate' }
•	4. El token debe tener expiración de 72 horas (configurable).
•	5. El campo IsUsed en SharedTokens debe ser false (token de un solo uso).
•	6. Construir URL pública: /public/labor-execution/{token}
•	7. Retornar la URL en el response junto con el token.

Endpoint existente: POST api/labors/{id}/realize
•	Mantener el endpoint existente.
•	Agregar validación: Status debe ser AwaitingValidation antes de permitir realizar.
•	Si se llama desde el Magic Link público: IgnoreQueryFilters() para evitar el filtro de tenant.

⚠ Seguridad — Magic Link
El endpoint público /api/share/{token}/validate debe estar excluido de [Authorize] pero incluido en rate limiting para prevenir abuso. El tenant se extrae de los metadatos del token, NO del contexto HTTP.


Tarea 2 — PATCH Priority para Labores Sueltas
Nuevo endpoint: PATCH api/labors/{id}/priority
Body: { "priority": int }
•	Verificar que la labor exista y pertenezca al tenant activo.
•	Actualizar el campo Priority.
•	Retornar 204 No Content en éxito.


Tarea 3 — GET Unassigned con Ordenamiento
Endpoint existente: GET api/labors/unassigned
Agregar query param opcional: sortBy (valores: priority | date)
•	sortBy=priority → ORDER BY Priority ASC, EstimatedDate ASC
•	sortBy=date → ORDER BY EstimatedDate ASC, Priority ASC
•	Default (sin param) → mantener comportamiento actual


Tarea 4 — Labor Ejecutada Sin Planificación
Archivo: src/GestorOT.Api/Controllers/LaborsController.cs
El endpoint POST api/labors ya soporta Mode=Realized. La corrección es:
•	Confirmar que WorkOrderId es Guid? en el DTO de entrada — ya lo es.
•	Eliminar cualquier validación que rechace requests con WorkOrderId = null.
•	Si Mode=Realized y WorkOrderId=null → la labor es 'suelta ejecutada' directamente. Status = Realized.
•	Si Mode=Realized y PlannedLaborId existe → vincular con la labor planificada y cambiar su Status a Validated.


Tarea 5 — MapToDto con Nuevos Campos
En el método de mapeo (MapToDto o equivalente) de LaborsController.cs:
•	Incluir Priority en el DTO de respuesta
•	Incluir SupplyWithdrawalNotes en el DTO de respuesta
•	Incluir Status como string (nombre del enum) para compatibilidad con el cliente Blazor
•	Verificar que WeatherLogJson se serialice/deserialice correctamente con el nuevo campo WindDirection
